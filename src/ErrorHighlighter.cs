using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ErrorHighlighter
{
    class ErrorHighlighter
    {
        private Adornment _text;
        private IWpfTextView _view;
        private IAdornmentLayer _adornmentLayer;
        private ITextDocument _document;
        private IVsTaskList _tasks;
        private Dispatcher _dispatcher;
        private bool _processing;
        private Timer _timer;
        private SVsServiceProvider _serviceProvider;

        public ErrorHighlighter(IWpfTextView view, ITextDocument document, IVsTaskList tasks, DTE2 dte, SVsServiceProvider serviceProvider)
        {
            _view = view;
            _document = document;
            _text = new Adornment();
            _tasks = tasks;
            _serviceProvider = serviceProvider;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _adornmentLayer = view.GetAdornmentLayer(ErrorHighlighterFactory.LayerName);

            _view.ViewportHeightChanged += SetAdornmentLocation;
            _view.ViewportWidthChanged += SetAdornmentLocation;

            _text.MouseUp += (s, e) => { dte.ExecuteCommand("View.ErrorList"); };

            _timer = new Timer(750);
            _timer.Elapsed += (s, e) =>
            {
                _timer.Stop();
                System.Threading.Tasks.Task.Run(() =>
                {
                    _dispatcher.Invoke(new Action(() =>
                    {
                        Update(false);
                    }), DispatcherPriority.ApplicationIdle, null);
                });
            };
            _timer.Start();
        }

        void SetAdornmentLocation(object sender, EventArgs e)
        {
            Canvas.SetLeft(_text, _view.ViewportRight - 130);
            Canvas.SetTop(_text, _view.ViewportTop + 20);
        }

        public void Update(bool highlight)
        {
            if (!highlight && _processing)
                return;

            _processing = true;

            UpdateAdornment(highlight);

            if (_adornmentLayer.IsEmpty)
                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _text, null);

            _processing = false;
            _timer.Start();
        }

        private async void UpdateAdornment(bool highlight)
        {
            int errors = 0;
            int warnings = 0;
            int messages = 0;

            foreach (IVsTaskItem item in GetErrorListItems())
            {
                string file;
                item.Document(out file);
                if (string.IsNullOrEmpty(file) || file != _document.FilePath)
                    continue;

                IVsErrorItem errorItem = item as IVsErrorItem;
                uint errorCategory;
                errorItem.GetCategory(out errorCategory);
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_ERROR) errors++;
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_WARNING) warnings++;
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_MESSAGE) messages++;
            }

            _text.SetValues(errors, warnings, messages);

            if (highlight)
                await _text.Highlight();
        }

        public IEnumerable<IVsTaskItem> GetErrorListItems()
        {
            IVsEnumTaskItems itemsEnum;
            _tasks.EnumTaskItems(out itemsEnum);

            IVsTaskItem[] oneItem = new IVsTaskItem[1];
            List<IVsTaskItem> items = new List<IVsTaskItem>();
            int result = 0; //S_OK == 0, S_FALSE == 1
            do
             {
                result = itemsEnum.Next(1, oneItem, null);
                if (result == 0)
                {
                    items.Add(oneItem[0]);
                }
            } while (result == 0);

            return items;
        }

    }
}
