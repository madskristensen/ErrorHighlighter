using System;
using System.Collections.Generic;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Threading;
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

        public ErrorHighlighter(IWpfTextView view, ITextDocument document, IVsTaskList tasks)
        {
            _view = view;
            _document = document;
            _text = new Adornment();
            _tasks = tasks;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _adornmentLayer = view.GetAdornmentLayer("ErrorHighlighter");

            _view.ViewportHeightChanged += delegate { this.Update(); };
            _view.ViewportWidthChanged += delegate { this.Update(); };

            Timer timer = new Timer(500);
            timer.Elapsed += (s, e) =>
            {
                _dispatcher.Invoke(new Action(() =>
                {
                    Update();
                }), DispatcherPriority.ApplicationIdle, null);
            };
            timer.Start();
        }

        public void Update()
        {
            if (_processing)
                return;

            _processing = true;

            int errors = 0;
            int warnings = 0;
            int messages = 0;

            foreach (IVsTaskItem item in GetErrorListItems())
            {
                string file;
                if (item.Document(out file) == 0 && file != _document.FilePath)
                    continue;

                IVsErrorItem errorItem = item as IVsErrorItem;
                uint errorCategory;
                errorItem.GetCategory(out errorCategory);
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_ERROR) errors++;
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_WARNING) warnings++;
                if (errorCategory == (uint)__VSERRORCATEGORY.EC_MESSAGE) messages++;
            }

            _text.SetValues(errors, warnings, messages);

            Canvas.SetLeft(_text, _view.ViewportRight - 90);
            Canvas.SetTop(_text, _view.ViewportTop + 20);

            if (_adornmentLayer.IsEmpty)
                _adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, _text, null);

            _processing = false;
        }

        public List<IVsTaskItem> GetErrorListItems()
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
