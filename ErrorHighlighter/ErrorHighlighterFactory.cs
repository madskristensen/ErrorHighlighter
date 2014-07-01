using System.ComponentModel.Composition;
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
    internal sealed class PurpleBoxAdornmentFactory : IWpfTextViewCreationListener
    {
        [Import]
        public SVsServiceProvider serviceProvider { get; set; }

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ErrorHighlighter")]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            IVsTaskList tasks = serviceProvider.GetService(typeof(SVsErrorList)) as IVsTaskList;

            ITextDocument document;
            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                var highlighter = new ErrorHighlighter(textView, document, tasks);
                //document.FileActionOccurred += (s, e) =>
                //{
                //    if (e.FileActionType == FileActionTypes.ContentSavedToDisk)
                //       highlighter.Check = true;
                //};

                //textView.TextBuffer.PostChanged += (s, e) =>
                //{
                //    highlighter.Check = true;
                //};
            }
        }
    }
}
