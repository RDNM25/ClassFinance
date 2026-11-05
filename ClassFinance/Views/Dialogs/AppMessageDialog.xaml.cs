using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ClassFinance.Views.Dialogs
{
    public partial class AppMessageDialog : Window
    {
        private readonly MessageBoxButton _buttons;
        private readonly MessageBoxResult _primaryResult;
        private readonly MessageBoxResult _secondaryResult;

        public MessageBoxResult Result { get; private set; }

        public AppMessageDialog(string message, string caption, MessageBoxButton buttons, MessageBoxImage image,
            AppMessageIcon icon = AppMessageIcon.Default)
        {
            InitializeComponent();
            _buttons = buttons;
            TitleText.Text = caption;
            MessageText.Text = message;

            (_primaryResult, _secondaryResult) = buttons switch
            {
                MessageBoxButton.YesNo => (MessageBoxResult.Yes, MessageBoxResult.No),
                MessageBoxButton.OKCancel => (MessageBoxResult.OK, MessageBoxResult.Cancel),
                _ => (MessageBoxResult.OK, MessageBoxResult.Cancel)
            };

            if (buttons == MessageBoxButton.OK)
            {
                SecondaryButton.Visibility = Visibility.Collapsed;
                ConfirmButton.Content = "Mengerti";
            }
            else
            {
                SecondaryButton.Content = buttons == MessageBoxButton.OKCancel ? "Batal" : "Tidak";
                ConfirmButton.Content = buttons == MessageBoxButton.OKCancel ? "Oke" : "Ya";
            }

            ApplyIcon(image, icon);
        }

        private void ApplyIcon(MessageBoxImage image, AppMessageIcon iconKind)
        {
            if (iconKind == AppMessageIcon.Database)
            {
                IconText.Visibility = Visibility.Collapsed;
                DatabaseIcon.Visibility = Visibility.Visible;
                IconBadge.Background = (Brush)FindResource("PrimaryRedLight");
                IconBadge.BorderThickness = new Thickness(0);
                return;
            }

            if (image == MessageBoxImage.Warning)
            {
                IconText.Text = "!";
                IconText.FontSize = 24;
                IconText.FontWeight = FontWeights.Bold;
                IconText.Foreground = (Brush)FindResource("WarningBadgeForeground");
                IconBadge.Background = (Brush)FindResource("WarningBadgeBackground");
                IconBadge.BorderBrush = (Brush)FindResource("WarningBadgeBorder");
                IconBadge.BorderThickness = new Thickness(1);
                return;
            }

            var icon = image switch
            {
                MessageBoxImage.Error => ("×", "StatusRed"),
                MessageBoxImage.Question => ("?", "AccentText"),
                MessageBoxImage.Information => ("i", "AccentText"),
                _ => ("•", "AccentText")
            };

            IconText.Text = icon.Item1;
            IconText.FontSize = 22;
            IconText.FontWeight = FontWeights.SemiBold;
            IconBadge.Background = (Brush)FindResource("PrimaryRedLight");
            IconBadge.BorderThickness = new Thickness(0);
            if (TryFindResource(icon.Item2) is Brush brush)
                IconText.Foreground = brush;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Result = _primaryResult;
            DialogResult = true;
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            Result = _secondaryResult;
            DialogResult = false;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Dialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_buttons == MessageBoxButton.OK)
                    Result = MessageBoxResult.OK;
                else
                    Result = _secondaryResult;
                DialogResult = false;
                e.Handled = true;
            }
        }
    }
}
