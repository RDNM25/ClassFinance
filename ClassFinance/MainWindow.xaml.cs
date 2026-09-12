using System.Collections.Generic;
using System.Linq;
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
        private readonly Stack<Page> _backStack = new();
        private bool _isNavigatingBack;

        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        public void ShowLogin()
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarBorder.Visibility = Visibility.Collapsed;
            _backStack.Clear();
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
            _backStack.Clear();
            NavigateTo(new DashboardPage(user, this), recordHistory: false);
            ClearNavigationHistory();
            UpdateBackButton();
        }

        public void NavigateTo(Page page, bool recordHistory = true)
        {
            if (recordHistory && !_isNavigatingBack && ContentFrame.Content is Page current && current is not LoginPage)
                _backStack.Push(current);

            ContentFrame.Navigate(page);
            _isNavigatingBack = false;
            UpdateBackButton();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var previous = PopSafePreviousPage();
            if (previous == null) return;

            _isNavigatingBack = true;
            ContentFrame.Navigate(previous);
            UpdateBackButton();
        }

        private Page PopSafePreviousPage()
        {
            while (_backStack.Count > 0)
            {
                var previous = _backStack.Pop();
                if (previous is not LoginPage)
                    return previous;
            }

            return null;
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e) => UpdateBackButton();

        private void UpdateBackButton()
        {
            var canGoBack = SidebarBorder.Visibility == Visibility.Visible &&
                            _backStack.Any(page => page is not LoginPage);
            BackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
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
