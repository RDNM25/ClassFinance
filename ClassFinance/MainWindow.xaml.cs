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
            SetThemeBrush("AppBackground", _isDarkMode ? Color.FromRgb(24, 24, 28) : Color.FromRgb(245, 245, 247));
            SetThemeBrush("SurfaceBackground", _isDarkMode ? Color.FromRgb(36, 36, 42) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("SubtleBackground", _isDarkMode ? Color.FromRgb(46, 46, 53) : Color.FromRgb(245, 245, 247));
            SetThemeBrush("CardBackground", _isDarkMode ? Color.FromRgb(43, 40, 48) : Color.FromRgb(234, 230, 239));
            SetThemeBrush("ElevatedCardBackground", _isDarkMode ? Color.FromRgb(54, 51, 61) : Color.FromRgb(226, 220, 234));
            SetThemeColor("CardShadowColor", _isDarkMode ? Color.FromRgb(156, 160, 174) : Color.FromRgb(0, 0, 0));
            SetThemeBrush("SidebarBackground", _isDarkMode ? Color.FromRgb(36, 27, 31) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("TextDark", _isDarkMode ? Color.FromRgb(243, 244, 246) : Color.FromRgb(33, 33, 33));
            SetThemeBrush("TextGray", _isDarkMode ? Color.FromRgb(180, 180, 190) : Color.FromRgb(95, 99, 104));
            SetThemeBrush("BorderColor", _isDarkMode ? Color.FromRgb(66, 66, 75) : Color.FromRgb(224, 224, 224));
            SetThemeBrush("PrimaryRedLight", _isDarkMode ? Color.FromRgb(70, 37, 42) : Color.FromRgb(255, 235, 238));
            SetThemeBrush("AccentText", _isDarkMode ? Color.FromRgb(255, 77, 77) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("TagihanActionForeground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("StatusGreen", _isDarkMode ? Color.FromRgb(129, 199, 132) : Color.FromRgb(27, 94, 32));
            SetThemeBrush("StatusAmber", _isDarkMode ? Color.FromRgb(255, 183, 77) : Color.FromRgb(141, 75, 0));
            SetThemeBrush("StatusRed", _isDarkMode ? Color.FromRgb(255, 114, 114) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("IncomeColor", _isDarkMode ? Color.FromRgb(255, 77, 77) : Color.FromRgb(198, 40, 40));
            SetThemeBrush("ExpenseColor", _isDarkMode ? Color.FromRgb(196, 198, 207) : Color.FromRgb(84, 84, 84));
            SetThemeBrush("ErrorColor", _isDarkMode ? Color.FromRgb(255, 77, 77) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("DisabledButtonBackground", _isDarkMode ? Color.FromRgb(69, 69, 76) : Color.FromRgb(226, 226, 226));
            SetThemeBrush("DisabledButtonText", _isDarkMode ? Color.FromRgb(224, 224, 229) : Color.FromRgb(87, 87, 87));
            SetThemeBrush("DisabledText", _isDarkMode ? Color.FromRgb(164, 164, 174) : Color.FromRgb(112, 112, 120));
            SetThemeBrush("TransactionDateBackground", _isDarkMode ? Color.FromRgb(101, 68, 20) : Color.FromRgb(255, 224, 178));
            SetThemeBrush("TransactionDateForeground", _isDarkMode ? Color.FromRgb(255, 224, 163) : Color.FromRgb(109, 60, 0));
            SetThemeBrush("TransactionDateBorder", _isDarkMode ? Color.FromRgb(255, 194, 102) : Color.FromRgb(173, 92, 0));
            SetThemeBrush("ActionButtonBackground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("ActionButtonForeground", _isDarkMode ? Color.FromRgb(17, 17, 17) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("ActionButtonBorder", Color.FromRgb(211, 47, 47));
            SetThemeBrush("ActionButtonHoverBackground", Color.FromRgb(243, 244, 246));
            SetThemeBrush("ActionButtonPressedBackground", Color.FromRgb(229, 231, 235));
            SetThemeBrush("CalendarNavBackground", _isDarkMode ? Color.FromRgb(58, 58, 67) : Color.FromRgb(244, 244, 246));
            SetThemeBrush("CalendarNavForeground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(33, 33, 33));
            SetThemeBrush("CalendarNavBorder", _isDarkMode ? Color.FromRgb(112, 112, 125) : Color.FromRgb(199, 199, 208));
            SetThemeBrush("CalendarNavHoverBackground", _isDarkMode ? Color.FromRgb(211, 47, 47) : Color.FromRgb(255, 235, 238));
            SetThemeBrush("CalendarNavHoverForeground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(33, 33, 33));
            SetThemeBrush("CalendarNavPressedBackground", _isDarkMode ? Color.FromRgb(183, 28, 28) : Color.FromRgb(255, 205, 210));
            SetThemeBrush("CalendarCurrentMonthBackground", _isDarkMode ? Color.FromRgb(70, 37, 42) : Color.FromRgb(255, 235, 238));
            SetThemeBrush("CalendarCurrentMonthBorder", _isDarkMode ? Color.FromRgb(255, 77, 77) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("CalendarSelectedMonthBackground", Color.FromRgb(211, 47, 47));
            SetThemeBrush("CalendarSelectedMonthForeground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("CalendarSelectedMonthBorder", Color.FromRgb(138, 28, 28));
            SetThemeBrush("CalendarNavFocusBorder", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(33, 33, 33));
            SetThemeBrush("CalendarNavArrowBackground", _isDarkMode ? Color.FromRgb(243, 244, 246) : Color.FromRgb(245, 245, 247));
            SetThemeBrush("CalendarNavArrowForeground", Color.FromRgb(17, 17, 17));
            SetThemeBrush("CalendarNavArrowBorder", _isDarkMode ? Color.FromRgb(255, 114, 114) : Color.FromRgb(224, 224, 224));
            SetThemeBrush("CalendarNavArrowHoverBackground", _isDarkMode ? Color.FromRgb(229, 231, 235) : Color.FromRgb(255, 235, 238));
            SetThemeBrush("CalendarNavArrowPressedBackground", _isDarkMode ? Color.FromRgb(209, 213, 219) : Color.FromRgb(255, 205, 210));
            SetThemeBrush("StudentAddBackground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(211, 47, 47));
            SetThemeBrush("StudentAddForeground", _isDarkMode ? Color.FromRgb(17, 17, 17) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("StudentAddBorder", _isDarkMode ? Color.FromRgb(211, 47, 47) : Color.FromRgb(211, 47, 47));
            SetThemeBrush("StudentAddHoverBackground", _isDarkMode ? Color.FromRgb(243, 244, 246) : Color.FromRgb(183, 28, 28));
            SetThemeBrush("StudentAddPressedBackground", _isDarkMode ? Color.FromRgb(229, 231, 235) : Color.FromRgb(183, 28, 28));
            SetThemeColor("CardCornerTint", _isDarkMode ? Color.FromRgb(53, 39, 44) : Color.FromRgb(255, 241, 241));
            SetThemeColor("CardCornerMidTint", _isDarkMode ? Color.FromRgb(43, 37, 42) : Color.FromRgb(255, 249, 249));
            SetThemeColor("CardSurfaceTint", _isDarkMode ? Color.FromRgb(36, 36, 42) : Color.FromRgb(255, 255, 255));

            ThemeToggleIcon.Text = _isDarkMode ? "☀" : "☾";
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
            AddNavButton("\uD83D\uDCCA", "Dashboard", typeof(DashboardPage), () => NavigateTo(new DashboardPage(user, this)));
            AddNavButton("\uD83D\uDC65", "Data Siswa", typeof(SiswaPage), () => NavigateTo(new SiswaPage(user, this)));
            AddNavButton("\uD83D\uDCC4", "Riwayat Transaksi", typeof(RiwayatTransaksiPage), () => NavigateTo(new RiwayatTransaksiPage(user, this)));
            AddNavButton("\uD83D\uDCCC", "Tagihan", typeof(TagihanPage), () => NavigateTo(new TagihanPage(user, this)));

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
