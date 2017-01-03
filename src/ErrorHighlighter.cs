using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Threading;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SVsServiceProvider = Microsoft.VisualStudio.Shell.SVsServiceProvider;

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
			IVsEnumTaskItems itemsEnum;
			_tasks.EnumTaskItems(out itemsEnum);

			IVsTaskItem[] rgelt = new IVsTaskItem[short.MaxValue];
			uint[] pceltFetched = new uint[1];
			int result;
			BufferBlock<IVsTaskItem> items = new BufferBlock<IVsTaskItem>();
			Task<TaskListCount> task = Task.Factory.StartNew(ProcessItems, items).Unwrap();
			do
			{
				result = itemsEnum.Next((uint)rgelt.Length, rgelt, pceltFetched);
				for (int i = 0; i < pceltFetched[0]; i++)
				{
					await items.SendAsync(rgelt[i]);
				}
				if (result == VSConstants.S_OK)
				{
					await Task.Delay(100).ConfigureAwait(true);
				}
			} while (result == VSConstants.S_OK);
			items.Complete();
			TaskListCount taskListCount = await task.ConfigureAwait(true);

			_text.SetValues(taskListCount.Errors, taskListCount.Warnings, taskListCount.Messages);

			if (highlight)
				await _text.Highlight();
		}

		private struct TaskListCount
		{
			public int Errors
			{
				get;
			}

			public int Warnings
			{
				get;
			}

			public int Messages
			{
				get;
			}

			public TaskListCount(int errors, int warnings, int messages)
			{
				Errors = errors;
				Warnings = warnings;
				Messages = messages;
			}
		}

		private string GetDocumentFilePath() => _document.FilePath;

		private class ErrorCategoryClosure
		{
			private readonly Dispatcher dispatcher;
			private readonly Func<uint> getCategoryInner;

			public ErrorCategoryClosure(Dispatcher dispatcher)
			{
				this.dispatcher = dispatcher;
				getCategoryInner = new Func<uint>(GetCategoryInner);
			}

			public IVsErrorItem ErrorItem
			{
				get;
				set;
			}

			public async System.Threading.Tasks.Task<__VSERRORCATEGORY> GetCategory()
			{
				return (__VSERRORCATEGORY)await dispatcher.InvokeAsync(getCategoryInner, DispatcherPriority.ApplicationIdle);
			}

			private uint GetCategoryInner()
			{
				uint category;
				ErrorItem.GetCategory(out category);
				return category;
			}
		}

		private async Task<TaskListCount> ProcessItems(object state)
		{
			BufferBlock<IVsTaskItem> items = (BufferBlock<IVsTaskItem>)state;
			int localErrors = 0;
			int localWarnings = 0;
			int localMessages = 0;
			string documentFilePath = await _dispatcher.InvokeAsync(GetDocumentFilePath, DispatcherPriority.ApplicationIdle);
			ErrorCategoryClosure errorCategoryClosure = new ErrorCategoryClosure(_dispatcher);
			while (!items.Completion.IsCompleted && items.Count > 0)
			{
				IVsTaskItem item = await items.ReceiveAsync();
				string file;
				item.Document(out file);
				if (string.IsNullOrEmpty(file) || !string.Equals(file, documentFilePath, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				IVsErrorItem errorItem = item as IVsErrorItem;
				errorCategoryClosure.ErrorItem = errorItem;
				__VSERRORCATEGORY errorCategory = await errorCategoryClosure.GetCategory();
				switch (errorCategory)
				{
					case __VSERRORCATEGORY.EC_ERROR:
						localErrors++;
						break;
					case __VSERRORCATEGORY.EC_WARNING:
						localWarnings++;
						break;
					case __VSERRORCATEGORY.EC_MESSAGE:
						localMessages++;
						break;
				}
			}
			return new TaskListCount(localErrors, localWarnings, localMessages);
		}

	}
}
