using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views;

namespace ClassFinance
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        public void ShowLogin()
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarBorder.Visibility = Visibility.Collapsed;
            ContentFrame.Navigate(new LoginPage(this));
            ClearNavigationHistory();
            UpdateBackButton();
        }

        public void ShowShell(User user)
        {
            SidebarColumn.Width = new GridLength(230);
            SidebarBorder.Visibility = Visibility.Visible;
            SidebarUserText.Text = $"{user.Name} \u2022 {user.Role}";
            BuildNav(user);
            NavigateTo(new DashboardPage(user, this));
            ClearNavigationHistory();
            UpdateBackButton();
        }

        public void NavigateTo(Page page)
        {
            ContentFrame.Navigate(page);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e) => UpdateBackButton();

        private void UpdateBackButton()
        {
            BackButton.Visibility = SidebarBorder.Visibility == Visibility.Visible && ContentFrame.CanGoBack
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ClearNavigationHistory()
        {
            while (ContentFrame.CanGoBack)
                ContentFrame.RemoveBackEntry();
        }

        private void BuildNav(User user)
        {
            NavPanel.Children.Clear();

            AddNavButton("\uD83D\uDCCA  Dashboard", () => NavigateTo(new DashboardPage(user, this)));
            AddNavButton("\uD83D\uDC65  Data Siswa", () => NavigateTo(new SiswaPage(user, this)));
            AddNavButton("\uD83D\uDCC4  Riwayat Transaksi", () => NavigateTo(new RiwayatTransaksiPage(user, this)));
            AddNavButton("\uD83D\uDCCC  Tagihan", () => NavigateTo(new TagihanPage(user, this)));
        }

        private void AddNavButton(string text, System.Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("SidebarButton")
            };
            button.Click += (s, e) => onClick();
            NavPanel.Children.Add(button);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            AuthService.Logout();
            ShowLogin();
        }

        /// <summary>Shows a dialog UserControl as an in-window modal overlay (no separate OS window).</summary>
        public void ShowModal(UIElement content)
        {
            ModalHost.Content = content;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        public void CloseModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalHost.Content = null;
        }

        private void ModalOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only dismiss when the backdrop itself was clicked, not the dialog card on top of it.
            if (e.OriginalSource == ModalOverlay)
                CloseModal();
        }
    }
}
