using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Navigation;
using System.IO;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views;
using ClassFinance.Behaviors;

namespace ClassFinance
{
    public partial class MainWindow : Window
    {
        private Page _currentPage;
        private User _currentUser;
        private bool _isDarkMode;

        public MainWindow()
        {
            ButtonHoverAnimationBehavior.Register();
            InitializeComponent();
            try
            {
                DataStore.Instance.Initialize();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"Tidak dapat terhubung ke database kasmate. Pastikan MySQL/MariaDB XAMPP aktif dan database sudah dimigrasikan.\n\n{exception.Message}",
                    "Koneksi Database Gagal", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            _isDarkMode = LoadDarkModePreference();
            ApplyTheme();
            ShowLogin();
        }

        private static string ThemePreferencePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassFinance", "dark-mode.pref");

        private bool LoadDarkModePreference()
        {
            try
            {
                return File.Exists(ThemePreferencePath)
                    && bool.TryParse(File.ReadAllText(ThemePreferencePath), out var enabled)
                    && enabled;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void SaveDarkModePreference()
        {
            try
            {
                var directory = Path.GetDirectoryName(ThemePreferencePath);
                Directory.CreateDirectory(directory);
                File.WriteAllText(ThemePreferencePath, _isDarkMode.ToString());
            }
            catch (IOException)
            {
                // Keep the theme change for this session if the preference cannot be saved.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the theme change for this session if the preference cannot be saved.
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            SaveDarkModePreference();
        }

        private void ApplyTheme()
        {
            SetThemeBrush("AppBackground", _isDarkMode ? Color.FromRgb(7, 8, 10) : Color.FromRgb(244, 245, 247));
            SetThemeBrush("SurfaceBackground", _isDarkMode ? Color.FromRgb(16, 18, 20) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("SubtleBackground", _isDarkMode ? Color.FromRgb(25, 27, 30) : Color.FromRgb(241, 242, 243));
            SetThemeBrush("CardBackground", _isDarkMode ? Color.FromRgb(19, 21, 24) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("ElevatedCardBackground", _isDarkMode ? Color.FromRgb(28, 30, 34) : Color.FromRgb(242, 243, 245));
            SetThemeColor("CardShadowColor", Color.FromRgb(0, 0, 0));
            SetThemeBrush("SidebarBackground", Color.FromRgb(8, 9, 10));
            SetThemeBrush("TextDark", _isDarkMode ? Color.FromRgb(238, 240, 242) : Color.FromRgb(27, 27, 27));
            SetThemeBrush("TextGray", _isDarkMode ? Color.FromRgb(165, 169, 175) : Color.FromRgb(101, 106, 112));
            SetThemeBrush("BorderColor", _isDarkMode ? Color.FromRgb(51, 54, 59) : Color.FromRgb(215, 217, 220));
            SetThemeBrush("PrimaryRed", Color.FromRgb(225, 29, 46));
            SetThemeBrush("PrimaryRedDark", Color.FromRgb(185, 23, 42));
            SetThemeBrush("PrimaryRedLight", _isDarkMode ? Color.FromRgb(53, 18, 26) : Color.FromRgb(252, 232, 232));
            SetThemeBrush("AccentText", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("TagihanActionForeground", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("StatusGreen", _isDarkMode ? Color.FromRgb(123, 203, 152) : Color.FromRgb(46, 125, 50));
            SetThemeBrush("StatusAmber", _isDarkMode ? Color.FromRgb(231, 184, 94) : Color.FromRgb(141, 107, 47));
            SetThemeBrush("StatusRed", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(198, 40, 40));
            SetThemeBrush("IncomeColor", _isDarkMode ? Color.FromRgb(123, 203, 152) : Color.FromRgb(46, 125, 50));
            SetThemeBrush("ExpenseColor", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(198, 40, 40));
            SetThemeBrush("ErrorColor", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(198, 40, 40));
            SetThemeBrush("CashFlowPrimaryColor", _isDarkMode ? Color.FromRgb(255, 56, 76) : Color.FromRgb(225, 29, 46));
            SetThemeBrush("CashFlowSecondaryColor", _isDarkMode ? Color.FromRgb(161, 173, 165) : Color.FromRgb(130, 145, 137));
            SetThemeBrush("CashFlowIncomeFill", _isDarkMode ? Color.FromArgb(54, 255, 56, 76) : Color.FromArgb(36, 225, 29, 46));
            SetThemeBrush("DisabledButtonBackground", _isDarkMode ? Color.FromRgb(43, 46, 50) : Color.FromRgb(231, 233, 236));
            SetThemeBrush("DisabledButtonText", _isDarkMode ? Color.FromRgb(142, 148, 155) : Color.FromRgb(91, 96, 103));
            SetThemeBrush("DisabledText", _isDarkMode ? Color.FromRgb(117, 123, 130) : Color.FromRgb(119, 125, 133));
            SetThemeBrush("TransactionDateBackground", _isDarkMode ? Color.FromRgb(49, 23, 27) : Color.FromRgb(252, 232, 232));
            SetThemeBrush("TransactionDateForeground", _isDarkMode ? Color.FromRgb(255, 91, 101) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("TransactionDateBorder", _isDarkMode ? Color.FromRgb(153, 45, 57) : Color.FromRgb(240, 68, 82));
            SetThemeBrush("ActionButtonBackground", _isDarkMode ? Color.FromRgb(21, 23, 26) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("ActionButtonForeground", _isDarkMode ? Color.FromRgb(225, 227, 230) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("ActionButtonBorder", _isDarkMode ? Color.FromRgb(255, 74, 87) : Color.FromRgb(240, 68, 82));
            SetThemeBrush("ActionButtonHoverBackground", _isDarkMode ? Color.FromRgb(53, 18, 25) : Color.FromRgb(255, 240, 240));
            SetThemeBrush("ActionButtonPressedBackground", _isDarkMode ? Color.FromRgb(78, 23, 34) : Color.FromRgb(243, 196, 196));
            SetThemeBrush("CalendarNavBackground", _isDarkMode ? Color.FromRgb(29, 32, 36) : Color.FromRgb(242, 243, 245));
            SetThemeBrush("CalendarNavForeground", _isDarkMode ? Color.FromRgb(212, 214, 218) : Color.FromRgb(27, 27, 27));
            SetThemeBrush("CalendarNavBorder", _isDarkMode ? Color.FromRgb(62, 66, 72) : Color.FromRgb(196, 200, 204));
            SetThemeBrush("CalendarNavHoverBackground", _isDarkMode ? Color.FromRgb(57, 18, 25) : Color.FromRgb(252, 232, 232));
            SetThemeBrush("CalendarNavHoverForeground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(27, 27, 27));
            SetThemeBrush("CalendarNavPressedBackground", _isDarkMode ? Color.FromRgb(78, 23, 34) : Color.FromRgb(243, 196, 196));
            SetThemeBrush("CalendarCurrentMonthBackground", _isDarkMode ? Color.FromRgb(49, 18, 24) : Color.FromRgb(252, 232, 232));
            SetThemeBrush("CalendarCurrentMonthBorder", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(225, 29, 46));
            SetThemeBrush("CalendarSelectedMonthBackground", Color.FromRgb(225, 29, 46));
            SetThemeBrush("CalendarSelectedMonthForeground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("CalendarSelectedMonthBorder", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("CalendarNavFocusBorder", _isDarkMode ? Color.FromRgb(255, 77, 90) : Color.FromRgb(185, 28, 28));
            SetThemeBrush("CalendarNavArrowBackground", _isDarkMode ? Color.FromRgb(23, 26, 29) : Color.FromRgb(244, 245, 247));
            SetThemeBrush("CalendarNavArrowForeground", _isDarkMode ? Color.FromRgb(212, 214, 218) : Color.FromRgb(27, 27, 27));
            SetThemeBrush("CalendarNavArrowBorder", _isDarkMode ? Color.FromRgb(62, 66, 72) : Color.FromRgb(215, 217, 220));
            SetThemeBrush("CalendarNavArrowHoverBackground", _isDarkMode ? Color.FromRgb(57, 18, 25) : Color.FromRgb(252, 232, 232));
            SetThemeBrush("CalendarNavArrowPressedBackground", _isDarkMode ? Color.FromRgb(78, 23, 34) : Color.FromRgb(243, 196, 196));
            SetThemeBrush("StudentAddBackground", Color.FromRgb(225, 29, 46));
            SetThemeBrush("StudentAddForeground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("StudentAddBorder", Color.FromRgb(240, 68, 82));
            SetThemeBrush("StudentAddHoverBackground", Color.FromRgb(240, 68, 82));
            SetThemeBrush("StudentAddPressedBackground", Color.FromRgb(185, 23, 42));
            SetThemeColor("CardCornerTint", _isDarkMode ? Color.FromRgb(76, 19, 28) : Color.FromRgb(252, 231, 231));
            SetThemeColor("CardCornerMidTint", _isDarkMode ? Color.FromRgb(40, 22, 25) : Color.FromRgb(255, 247, 247));
            SetThemeColor("CardSurfaceTint", _isDarkMode ? Color.FromRgb(19, 21, 24) : Color.FromRgb(255, 255, 255));

            ThemeToggleIcon.Text = _isDarkMode ? "\uE706" : "\uE708";
            ThemeToggleText.Text = _isDarkMode ? "Mode Terang" : "Mode Gelap";
        }

        private static void SetThemeBrush(string resourceKey, Color color)
        {
            if (Application.Current.Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.Color = color;
                return;
            }

            Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
        }

        private static void SetThemeColor(string resourceKey, Color color)
        {
            Application.Current.Resources[resourceKey] = color;
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
            UpdateNavHighlight();
        }

        public void NavigateTo(Page page, bool recordHistory = true)
        {
            _currentPage = page;
            ContentFrame.Navigate(_currentPage);
            UpdateBackButton();
            UpdateNavHighlight();
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

            if (e.Content is UIElement pageContent)
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                pageContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
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

            // Separate the icon and text into two distinct strings
            AddNavButton("\uE80F", "Ringkasan", typeof(DashboardPage), () => NavigateTo(new DashboardPage(user, this)));
            AddNavButton("\uE716", "Data Siswa", typeof(SiswaPage), () => NavigateTo(new SiswaPage(user, this)));
            AddNavButton("\uE81C", "Riwayat Transaksi", typeof(RiwayatTransaksiPage), () => NavigateTo(new RiwayatTransaksiPage(user, this)));
            AddNavButton("\uE8A5", "Tagihan", typeof(TagihanPage), () => NavigateTo(new TagihanPage(user, this)));

            UpdateNavHighlight();
        }

        private void AddNavButton(string icon, string text, System.Type pageType, System.Action onClick)
        {
            var button = new Button
            {
                // Pass the strings as an array to bind them separately in XAML
                Content = new string[] { icon, text },
                Tag = pageType,
                Style = (Style)FindResource("SidebarButton")
            };
            button.Click += (s, e) => onClick();
            NavPanel.Children.Add(button);
        }

        /// <summary>Applies SidebarButtonActive to whichever nav button matches the current page's type.</summary>
        private void UpdateNavHighlight()
        {
            foreach (var child in NavPanel.Children)
            {
                if (child is Button button && button.Tag is System.Type pageType)
                {
                    bool isActive = _currentPage != null && _currentPage.GetType() == pageType;
                    button.Style = (Style)FindResource(isActive ? "SidebarButtonActive" : "SidebarButton");
                }
            }
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
