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
        private Page _currentPage;
        private User _currentUser;

        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        public void ShowLogin()
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarBorder.Visibility = Visibility.Collapsed;

            _currentUser = null;
            _currentPage = new LoginPage(this);

            ContentFrame.Navigate(_currentPage);
            ClearNavigationHistory();
            UpdateBackButton();
        }

        public void ShowShell(User user)
        {
            // Save the logged-in user so we can pass it to the Dashboard later
            _currentUser = user;

            SidebarColumn.Width = new GridLength(230);
            SidebarBorder.Visibility = Visibility.Visible;
            SidebarUserText.Text = $"{user.Name} \u2022 {user.Role}";
            BuildNav(user);

            _currentPage = new DashboardPage(user, this);

            ContentFrame.Navigate(_currentPage);
            ClearNavigationHistory();
            UpdateBackButton();
        }

        public void NavigateTo(Page page, bool recordHistory = true)
        {
            _currentPage = page;
            ContentFrame.Navigate(_currentPage);
            UpdateBackButton();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser != null)
            {
                NavigateTo(new DashboardPage(_currentUser, this));
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateBackButton();
        }

        private void UpdateBackButton()
        {
            if (BackButton == null) return;

            // Hide if the sidebar is gone (e.g. login layout)
            if (SidebarBorder.Visibility == Visibility.Collapsed)
            {
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            // Hide if we are already on the Dashboard page
            if (_currentPage is DashboardPage)
            {
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            // Otherwise, show the button
            BackButton.Visibility = Visibility.Visible;
        }

        private void ClearNavigationHistory()
        {
            // Clears the built-in frame history so the native mouse back-buttons don't mess up our custom logic
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