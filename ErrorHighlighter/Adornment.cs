using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
            this.BorderThickness = new Thickness(5);
            this.Padding = new Thickness(5);
            this.Child = _panel;
            _panel.Children.Add(_errors);
            _panel.Children.Add(_warnings);
            _panel.Children.Add(_messages);
        }

        public void SetValues(int errors, int warnings, int messages, bool hasPriority)
        {
            if (errors > 0 || hasPriority)
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
            //block.Opacity = count == 0 ? 0 : 1;
            block.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;

            string text = count == 0 ? singular : plural;
            block.Text = count.ToString().PadLeft(3, ' ') + " " + text;
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

        public async Task Blink()
        {
            await Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
            {
                if (Visibility == System.Windows.Visibility.Visible)
                {
                    BorderBrush = Brushes.Red;
                    Background = Brushes.Yellow;
                    await Task.Delay(500);
                    BorderBrush = null;
                    Background = null;
                }

            }), DispatcherPriority.ApplicationIdle, null);
        }
    }
}