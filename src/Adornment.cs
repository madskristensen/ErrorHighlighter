using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ErrorHighlighter
{
    class Adornment : Border
    {
        private TextBlock _errors = CreateBlocks(Colors.Red);
        private TextBlock _warnings = CreateBlocks(Colors.DarkOrange);
        private TextBlock _messages = CreateBlocks(Colors.CornflowerBlue);
        private StackPanel _panel = new StackPanel();

        public Adornment()
        {
            BorderThickness = new Thickness(0, 0, 0, 2);
            Padding = new Thickness(0, 0, 0, 3);
            Child = _panel;

            _panel.Children.Add(_errors);
            _panel.Children.Add(_warnings);
            _panel.Children.Add(_messages);

            Cursor = Cursors.Hand;
            ToolTip = "Click to open the Error List";
        }

        public void SetValues(int errors, int warnings, int messages)
        {
            if (errors > 0 || warnings > 0 || messages > 0)
            {
                SetValue(_errors, errors, "error", "errors");
                SetValue(_warnings, warnings, "warning", "warnings");
                SetValue(_messages, messages, "message", "messages");
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void SetValue(TextBlock block, int count, string singular, string plural)
        {
            block.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;

            if (count > 0)
            {
                string text = count == 1 ? singular : plural;
                block.Text = count.ToString().PadLeft(3, ' ') + " " + text + " ";
            }
        }

        private static TextBlock CreateBlocks(Color color)
        {
            return new TextBlock
            {
                FontSize = 16,
                Foreground = new SolidColorBrush(color),
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Left,
                FontFamily = new FontFamily("Consolas"),
            };
        }

        public async Task Highlight()
        {
            await Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
            {
                if (Visibility == System.Windows.Visibility.Visible)
                {
                    BorderBrush = new SolidColorBrush(Colors.Red);
                    BorderBrush.Opacity = .5;
                    await Task.Delay(500);
                    BorderBrush = null;
                }

            }), DispatcherPriority.ApplicationIdle, null);
        }
    }
}