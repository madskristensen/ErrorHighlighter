using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ErrorHighlighter
{
    class Adornment : StackPanel
    {
        private TextBlock _errors = CreateBlocks(Colors.Red);
        private TextBlock _warnings = CreateBlocks(Colors.DarkOrange);
        private TextBlock _messages = CreateBlocks(Colors.CornflowerBlue);

        public Adornment()
        {
            this.Children.Add(_errors);
            this.Children.Add(_warnings);
            this.Children.Add(_messages);
        }

        private static TextBlock CreateBlocks(Color color)
        {
            return new TextBlock
            {
                FontSize = 14,
                Foreground = new SolidColorBrush(color),
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
            };
        }

        public void SetValues(int errors, int warnings, int messages)
        {
            SetValue(_errors, errors, "error", "errors");
            SetValue(_warnings, warnings, "warning", "warnings");
            //SetValue(_messages, messages, "message", "messages");
        }

        private void SetValue(TextBlock block, int count, string singular, string plural)
        {
            block.Opacity = count == 0 ? 0 : 1;

            string text = count == 0 ? singular : plural;
            block.Text = count + " " + text;
        }
    }
}
