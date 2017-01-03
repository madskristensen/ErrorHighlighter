using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace ErrorHighlighter
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
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

            ITextDocument document;
            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                var highlighter = new ErrorHighlighter(textView, document, tasks, serviceProvider);

                // On file save
                document.FileActionOccurred += (s, e) =>
                {
                    if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                        highlighter.Update(true);
                };
            }
        }
    }
}
