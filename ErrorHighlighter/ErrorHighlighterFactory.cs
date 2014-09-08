using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace ErrorHighlighter
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class ErrorHighlighterFactory : IWpfTextViewCreationListener
    {
        public const string LayerName = "ErrorHighlighter";
        [Import]
        public SVsServiceProvider serviceProvider { get; set; }

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            IVsTaskList tasks = serviceProvider.GetService(typeof(SVsErrorList)) as IVsTaskList;
            DTE2 dte = serviceProvider.GetService(typeof(DTE)) as DTE2;

            ITextDocument document;
            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                var highlighter = new ErrorHighlighter(textView, document, tasks, dte);

                // On file save
                document.FileActionOccurred += (s, e) =>
                {
                    if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                        highlighter.Update(true);
                };

                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    highlighter.Update(false);
                }), DispatcherPriority.ApplicationIdle, null);
            }
        }
    }
}
