using System;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using static System.Threading.Tasks.TaskExtensions;
using ThreadingTask = System.Threading.Tasks.Task;

namespace ErrorHighlighter
{
	class ErrorHighlighter
	{
		private readonly Adornment text;
		private readonly IWpfTextView view;
		private readonly IAdornmentLayer adornmentLayer;
		private readonly ITextDocument document;
		private readonly IVsTaskList tasks;
		private readonly Dispatcher dispatcher;
		private bool processing;
		private readonly Timer timer;
		private readonly DTE2 dte;
		private readonly Action dispatchRunner;
		private readonly Action update;

		public ErrorHighlighter(IWpfTextView view, ITextDocument document, IVsTaskList tasks, DTE2 dte)
		{
			this.view = view;
			this.document = document;
			text = new Adornment();
			this.tasks = tasks;
			dispatcher = Dispatcher.CurrentDispatcher;
			this.dte = dte;

			adornmentLayer = view.GetAdornmentLayer(ErrorHighlighterFactory.LayerName);

			this.view.ViewportHeightChanged += SetAdornmentLocation;
			this.view.ViewportWidthChanged += SetAdornmentLocation;

			text.MouseUp += text_MouseUp;

			dispatchRunner = new Action(DispatchRunner);
			update = new Action(Update);

			timer = new Timer(750);
			timer.Elapsed += timer_Elapsed;
			timer.Start();
		}

		private void text_MouseUp(object sender, MouseButtonEventArgs e) => dte.ExecuteCommand("View.ErrorList");

		private void timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			timer.Stop();
			ThreadingTask.Run(dispatchRunner);
		}

		private void DispatchRunner() => dispatcher.Invoke(update, DispatcherPriority.ApplicationIdle);

		private void Update() => Update(false);

		void SetAdornmentLocation(object sender, EventArgs e)
		{
			Canvas.SetLeft(text, view.ViewportRight - 130);
			Canvas.SetTop(text, view.ViewportTop + 20);
		}

		public void Update(bool highlight)
		{
			if (!highlight && processing)
			{
				return;
			}
			processing = true;

			UpdateAdornment(highlight);

			if (adornmentLayer.IsEmpty)
				adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, text, null);

			processing = false;
			timer.Start();
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

		private string GetDocumentFilePath() => document.FilePath;

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

		private async System.Threading.Tasks.Task<TaskListCount> ProcessItems(object state)
		{
			BufferBlock<IVsTaskItem> items = (BufferBlock<IVsTaskItem>)state;
			int localErrors = 0;
			int localWarnings = 0;
			int localMessages = 0;
			string documentFilePath = await dispatcher.InvokeAsync(GetDocumentFilePath, DispatcherPriority.ApplicationIdle);
			ErrorCategoryClosure errorCategoryClosure = new ErrorCategoryClosure(dispatcher);
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

		private async void UpdateAdornment(bool highlight)
		{
			IVsEnumTaskItems itemsEnum;
			tasks.EnumTaskItems(out itemsEnum);

			IVsTaskItem[] rgelt = new IVsTaskItem[short.MaxValue];
			uint[] pceltFetched = new uint[1];
			int result;
			BufferBlock<IVsTaskItem> items = new BufferBlock<IVsTaskItem>();
			System.Threading.Tasks.Task<TaskListCount> task = ThreadingTask.Factory.StartNew(ProcessItems, items).Unwrap();
			do
			{
				result = itemsEnum.Next((uint)rgelt.Length, rgelt, pceltFetched);
				for (int i = 0; i < pceltFetched[0]; i++)
				{
					await items.SendAsync(rgelt[i]);
				}
				if (result == VSConstants.S_OK)
				{
					await ThreadingTask.Delay(100).ConfigureAwait(true);
				}
			} while (result == VSConstants.S_OK);
			items.Complete();
			TaskListCount taskListCount = await task.ConfigureAwait(true);

			text.SetValues(taskListCount.Errors, taskListCount.Warnings, taskListCount.Messages);

			if (highlight)
				await text.Highlight();
		}

	}
}
