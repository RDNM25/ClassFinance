using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Globalization;
using System.IO;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views;
using ClassFinance.Views.Dialogs;
using ClassFinance.Behaviors;

namespace ClassFinance
{
    public sealed class TopBarNotificationDisplay
    {
        public string Title { get; init; }
        public string StudentName { get; init; }
        public string Message { get; init; }
        public string DateText { get; init; }
        public string ReadStatusText { get; init; }
        public Brush ReadStatusBackground { get; init; }
        public Brush ReadStatusForeground { get; init; }
    }

    public partial class MainWindow : Window
    {
        private Page _currentPage;
        private User _currentUser;
        private bool _isDarkMode;
        private bool _suppressNotificationToggle;
        private bool _suppressProfileToggle;
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public MainWindow()
        {
            ButtonHoverAnimationBehavior.Register();
            InitializeComponent();
            Deactivated += MainWindow_Deactivated;
            NotificationPopup.CustomPopupPlacementCallback = PlacePopupAlignedRight;
            ProfilePopup.CustomPopupPlacementCallback = PlacePopupAlignedRight;
            _isDarkMode = LoadDarkModePreference();
            ApplyTheme();
            try
            {
                DataStore.Instance.Initialize();
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(this,
                    $"Tidak dapat terhubung ke database kasmate. Pastikan MySQL/MariaDB XAMPP aktif dan database sudah dimigrasikan.\n\n{exception.Message}",
                    "Koneksi Database Gagal", MessageBoxButton.OK, MessageBoxImage.Error, AppMessageIcon.Database);
                Close();
                return;
            }
            DataStore.Instance.DataChanged += DataStore_DataChanged;
            Closed += MainWindow_Closed;
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

        private void TopBarThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ProfilePopup.IsOpen = false;
            ThemeToggleButton_Click(sender, e);
        }

        private void ApplyTheme()
        {
            var primary = Color.FromRgb(0xF4, 0x3F, 0x5E);
            var primaryHover = Color.FromRgb(0xFB, 0x71, 0x85);
            var primaryPressed = Color.FromRgb(0xE1, 0x1D, 0x48);
            var textAccent = _isDarkMode ? primaryHover : Color.FromRgb(0xBE, 0x12, 0x3C);
            var green = _isDarkMode ? Color.FromRgb(0x10, 0xB9, 0x81) : Color.FromRgb(0x04, 0x78, 0x57);
            var amber = _isDarkMode ? Color.FromRgb(0xF5, 0x9E, 0x0B) : Color.FromRgb(0xB4, 0x53, 0x09);

            SetThemeBrush("AppBackground", _isDarkMode ? Color.FromRgb(15, 17, 23) : Color.FromRgb(245, 246, 250));
            SetPageBackground(_isDarkMode ? Color.FromRgb(34, 21, 28) : Color.FromRgb(255, 241, 243),
                _isDarkMode ? Color.FromRgb(15, 17, 23) : Color.FromRgb(245, 246, 250));
            SetThemeBrush("SurfaceBackground", _isDarkMode ? Color.FromRgb(24, 26, 34) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("SubtleBackground", _isDarkMode ? Color.FromRgb(32, 34, 43) : Color.FromRgb(240, 241, 245));
            SetThemeBrush("CardBackground", _isDarkMode ? Color.FromRgb(25, 27, 36) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("ElevatedCardBackground", _isDarkMode ? Color.FromRgb(34, 36, 46) : Color.FromRgb(238, 239, 243));
            SetThemeColor("CardShadowColor", Color.FromRgb(0, 0, 0));
            SetThemeBrush("SidebarBackground", Color.FromRgb(11, 13, 18));
            SetThemeBrush("TextDark", _isDarkMode ? Color.FromRgb(244, 244, 247) : Color.FromRgb(24, 24, 27));
            SetThemeBrush("TextGray", _isDarkMode ? Color.FromRgb(161, 161, 170) : Color.FromRgb(113, 113, 122));
            SetThemeBrush("BorderColor", _isDarkMode ? Color.FromRgb(52, 54, 65) : Color.FromRgb(228, 228, 231));
            SetThemeBrush("PrimaryRed", primary);
            SetThemeBrush("PrimaryRedDark", primaryPressed);
            SetThemeBrush("PrimaryRedLight", _isDarkMode ? Color.FromRgb(54, 23, 32) : Color.FromRgb(255, 228, 233));
            SetThemeBrush("AccentText", textAccent);
            SetThemeBrush("TagihanActionForeground", textAccent);
            SetThemeBrush("StatusGreen", green);
            SetThemeBrush("StatusAmber", amber);
            SetThemeBrush("StatusRed", textAccent);
            SetThemeBrush("WarningBadgeBackground", _isDarkMode ? Color.FromRgb(59, 25, 34) : Color.FromRgb(255, 228, 230));
            SetThemeBrush("WarningBadgeForeground", _isDarkMode ? Color.FromRgb(253, 164, 175) : Color.FromRgb(190, 18, 60));
            SetThemeBrush("WarningBadgeBorder", _isDarkMode ? primary : Color.FromRgb(225, 29, 72));
            SetThemeBrush("IncomeColor", green);
            SetThemeBrush("ExpenseColor", _isDarkMode ? primary : Color.FromRgb(190, 18, 60));
            SetThemeBrush("ErrorColor", _isDarkMode ? primaryHover : Color.FromRgb(190, 18, 60));
            SetThemeBrush("CashFlowPrimaryColor", green);
            SetThemeBrush("CashFlowSecondaryColor", amber);
            SetThemeBrush("CashFlowIncomeFill", _isDarkMode ? Color.FromArgb(54, 16, 185, 129) : Color.FromArgb(36, 4, 120, 87));
            SetThemeBrush("CashFlowExpenseFill", _isDarkMode ? Color.FromArgb(54, 245, 158, 11) : Color.FromArgb(36, 180, 83, 9));
            SetThemeBrush("DisabledButtonBackground", _isDarkMode ? Color.FromRgb(43, 45, 55) : Color.FromRgb(228, 228, 231));
            SetThemeBrush("DisabledButtonText", _isDarkMode ? Color.FromRgb(161, 161, 170) : Color.FromRgb(82, 82, 91));
            SetThemeBrush("DisabledText", _isDarkMode ? Color.FromRgb(113, 113, 122) : Color.FromRgb(113, 113, 122));
            SetThemeBrush("TransactionDateBackground", _isDarkMode ? Color.FromRgb(59, 25, 34) : Color.FromRgb(255, 228, 233));
            SetThemeBrush("TransactionDateForeground", textAccent);
            SetThemeBrush("TransactionDateBorder", _isDarkMode ? primaryHover : primary);
            SetThemeBrush("ActionButtonBackground", _isDarkMode ? Color.FromRgb(24, 26, 34) : Color.FromRgb(255, 255, 255));
            SetThemeBrush("ActionButtonForeground", _isDarkMode ? Color.FromRgb(244, 244, 247) : Color.FromRgb(190, 18, 60));
            SetThemeBrush("ActionButtonBorder", _isDarkMode ? primaryHover : primary);
            SetThemeBrush("ButtonHoverBackground", Color.FromArgb(0, 0, 0, 0));
            SetThemeBrush("ActionButtonHoverBackground", Color.FromArgb(0, 0, 0, 0));
            SetThemeBrush("ActionButtonPressedBackground", _isDarkMode ? Color.FromRgb(68, 27, 40) : Color.FromRgb(254, 205, 211));
            SetThemeBrush("CalendarNavBackground", _isDarkMode ? Color.FromRgb(34, 36, 46) : Color.FromRgb(238, 239, 243));
            SetThemeBrush("CalendarNavForeground", _isDarkMode ? Color.FromRgb(244, 244, 247) : Color.FromRgb(24, 24, 27));
            SetThemeBrush("CalendarNavBorder", _isDarkMode ? Color.FromRgb(52, 54, 65) : Color.FromRgb(212, 212, 216));
            SetThemeBrush("CalendarNavHoverBackground", Color.FromArgb(0, 0, 0, 0));
            SetThemeBrush("CalendarNavHoverForeground", _isDarkMode ? Color.FromRgb(255, 255, 255) : Color.FromRgb(24, 24, 27));
            SetThemeBrush("CalendarNavPressedBackground", _isDarkMode ? Color.FromRgb(68, 27, 40) : Color.FromRgb(254, 205, 211));
            SetThemeBrush("CalendarCurrentMonthBackground", _isDarkMode ? Color.FromRgb(59, 25, 34) : Color.FromRgb(255, 228, 233));
            SetThemeBrush("CalendarCurrentMonthBorder", _isDarkMode ? primaryHover : primary);
            SetThemeBrush("CalendarSelectedMonthBackground", primary);
            SetThemeBrush("CalendarSelectedMonthForeground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("CalendarSelectedMonthBorder", _isDarkMode ? primaryHover : Color.FromRgb(190, 18, 60));
            SetThemeBrush("CalendarNavFocusBorder", _isDarkMode ? primaryHover : Color.FromRgb(190, 18, 60));
            SetThemeBrush("CalendarNavArrowBackground", _isDarkMode ? Color.FromRgb(24, 26, 34) : Color.FromRgb(245, 246, 250));
            SetThemeBrush("CalendarNavArrowForeground", _isDarkMode ? Color.FromRgb(244, 244, 247) : Color.FromRgb(24, 24, 27));
            SetThemeBrush("CalendarNavArrowBorder", _isDarkMode ? Color.FromRgb(52, 54, 65) : Color.FromRgb(228, 228, 231));
            SetThemeBrush("CalendarNavArrowHoverBackground", Color.FromArgb(0, 0, 0, 0));
            SetThemeBrush("CalendarNavArrowPressedBackground", _isDarkMode ? Color.FromRgb(68, 27, 40) : Color.FromRgb(254, 205, 211));
            SetThemeBrush("StudentAddBackground", primary);
            SetThemeBrush("StudentAddForeground", Color.FromRgb(255, 255, 255));
            SetThemeBrush("StudentAddBorder", primaryHover);
            SetThemeBrush("StudentAddHoverBackground", Color.FromArgb(0, 0, 0, 0));
            SetThemeBrush("StudentAddPressedBackground", primaryPressed);
            TopBarThemeIcon.Text = _isDarkMode ? "\uE706" : "\uE708";
            TopBarThemeText.Text = _isDarkMode ? "Mode Terang" : "Mode Gelap";
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

        private static void SetPageBackground(Color topColor, Color bottomColor)
        {
            if (Application.Current.Resources["PageBackground"] is RadialGradientBrush brush &&
                !brush.IsFrozen && brush.GradientStops.Count >= 2)
            {
                brush.GradientStops[0].Color = topColor;
                brush.GradientStops[^1].Color = bottomColor;
                return;
            }

            var gradient = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                Center = new System.Windows.Point(0.5, 0),
                GradientOrigin = new System.Windows.Point(0.5, 0),
                RadiusX = 0.75,
                RadiusY = 0.4
            };
            gradient.GradientStops.Add(new GradientStop(topColor, 0));
            gradient.GradientStops.Add(new GradientStop(bottomColor, 1));
            Application.Current.Resources["PageBackground"] = gradient;
        }

        public void ShowLogin()
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarBorder.Visibility = Visibility.Collapsed;
            TopBarBorder.Visibility = Visibility.Collapsed;
            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;

            _currentUser = null;
            _currentPage = new LoginPage(this);

            ContentFrame.Navigate(_currentPage);
            ClearNavigationHistory();
        }

        public void ShowShell(User user)
        {
            // Save the logged-in user so we can pass it to the Dashboard later
            _currentUser = user;

            try
            {
                foreach (var schedule in DataStore.Instance.JadwalTagihanMingguanList.Where(x => x.IsActive).ToList())
                    JadwalTagihanMingguanService.PastikanMingguTerbuat(schedule.KelasId, DateTime.Today);
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(this,
                    $"Jadwal tagihan mingguan tidak dapat diperbarui. Data yang sudah tersimpan tetap tersedia.\n\n{exception.Message}",
                    "Jadwal Mingguan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            SidebarColumn.Width = new GridLength(230);
            SidebarBorder.Visibility = Visibility.Visible;
            TopBarBorder.Visibility = Visibility.Visible;
            SidebarUserText.Text = $"{user.Name} \u2022 {user.Role}";
            UpdateTopBar();
            BuildNav(user);

            _currentPage = new DashboardPage(user, this);

            ContentFrame.Navigate(_currentPage);
            ClearNavigationHistory();
            UpdateNavHighlight();
        }

        public void NavigateTo(Page page, bool recordHistory = true)
        {
            _currentPage = page;
            ContentFrame.Navigate(_currentPage);
            UpdateNavHighlight();
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is UIElement pageContent)
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                pageContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }


        private void UpdateTopBar()
        {
            if (_currentUser == null)
                return;

            TopBarDateText.Text = DateTime.Today.ToString("dddd, d MMM yyyy", IdId);
            TopBarProfileNameText.Text = string.IsNullOrWhiteSpace(_currentUser.Name)
                ? _currentUser.Username
                : _currentUser.Name;
            TopBarProfileRoleText.Text = GetRoleLabel(_currentUser.Role);
            ProfileMenuNameText.Text = TopBarProfileNameText.Text;
            ProfileMenuUsernameText.Text = GetRoleLabel(_currentUser.Role);
            TopBarProfileInitialsText.Text = GetProfileInitials(_currentUser.Name, _currentUser.Role);
            RefreshTopBarNotifications();
        }

        private static string GetRoleLabel(Role role) => role switch
        {
            Role.Bendahara => "Bendahara",
            Role.WaliKelas => "Wali Kelas",
            _ => "Siswa"
        };

        private static string GetProfileInitials(string name, Role role)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
            if (parts.Length == 1 && role == Role.Bendahara)
                return $"{char.ToUpperInvariant(parts[0][0])}B";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return "CF";
        }

        private void RefreshTopBarNotifications()
        {
            if (_currentUser == null)
                return;

            var store = DataStore.Instance;
            var studentIds = _currentUser switch
            {
                Siswa student => new HashSet<int> { student.Id },
                WaliKelas teacher => store.Users.OfType<Siswa>()
                    .Where(student => student.KelasId == teacher.KelasId).Select(student => student.Id).ToHashSet(),
                _ => store.Users.OfType<Siswa>()
                    .Where(student => student.KelasId == store.KelasList.FirstOrDefault()?.Id)
                    .Select(student => student.Id).ToHashSet()
            };

            var notifications = store.NotifikasiList
                .Where(item => studentIds.Contains(item.UserId))
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
            var studentNames = store.Users.OfType<Siswa>()
                .ToDictionary(student => student.Id, student => student.Name ?? "Siswa");
            var items = notifications.Take(8).Select(item => new TopBarNotificationDisplay
            {
                Title = item.Title ?? "Notifikasi",
                StudentName = studentNames.TryGetValue(item.UserId, out var studentName) ? studentName : "Siswa",
                Message = item.Message ?? string.Empty,
                DateText = item.CreatedAt.ToString("dd MMM yyyy · HH:mm", IdId),
                ReadStatusText = item.IsRead ? "Dibaca" : "Belum dibaca",
                ReadStatusBackground = FindBrush(item.IsRead ? "ElevatedCardBackground" : "WarningBadgeBackground"),
                ReadStatusForeground = FindBrush(item.IsRead ? "TextGray" : "WarningBadgeForeground")
            }).ToList();

            TopBarNotificationItems.ItemsSource = items;
            NotificationEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NotificationSummaryText.Text = notifications.Any(item => !item.IsRead)
                ? $"{notifications.Count(item => !item.IsRead)} belum dibaca"
                : "Terbaru";
            NotificationBadge.Visibility = notifications.Any(item => !item.IsRead)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static Brush FindBrush(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressNotificationToggle)
            {
                _suppressNotificationToggle = false;
                e.Handled = true;
                return;
            }

            ProfilePopup.IsOpen = false;
            RefreshTopBarNotifications();
            NotificationPopup.IsOpen = !NotificationPopup.IsOpen;
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressProfileToggle)
            {
                _suppressProfileToggle = false;
                e.Handled = true;
                return;
            }

            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var source = e.OriginalSource as DependencyObject;
            var onNotificationPopup = IsWithin(source, NotificationPopup.Child);
            var onProfilePopup = IsWithin(source, ProfilePopup.Child);
            if (onNotificationPopup || onProfilePopup)
                return;

            var onNotificationButton = IsWithin(source, NotificationButton);
            var onProfileButton = IsWithin(source, ProfileButton);

            if (NotificationPopup.IsOpen && onNotificationButton)
            {
                NotificationPopup.IsOpen = false;
                SuppressNextNotificationToggle();
                e.Handled = true;
                return;
            }

            if (ProfilePopup.IsOpen && onProfileButton)
            {
                ProfilePopup.IsOpen = false;
                SuppressNextProfileToggle();
                e.Handled = true;
                return;
            }

            if (!onNotificationButton)
                NotificationPopup.IsOpen = false;
            if (!onProfileButton)
                ProfilePopup.IsOpen = false;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || (!NotificationPopup.IsOpen && !ProfilePopup.IsOpen))
                return;

            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;
            e.Handled = true;
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;
        }

        private void SuppressNextNotificationToggle()
        {
            _suppressNotificationToggle = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _suppressNotificationToggle = false));
        }

        private void SuppressNextProfileToggle()
        {
            _suppressProfileToggle = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _suppressProfileToggle = false));
        }

        private static bool IsWithin(DependencyObject element, DependencyObject ancestor)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, ancestor))
                    return true;

                element = element is Visual || element is Visual3D
                    ? VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }

            return false;
        }

        private static CustomPopupPlacement[] PlacePopupAlignedRight(Size popupSize, Size targetSize, Point offset) =>
        [
            new CustomPopupPlacement(
                new Point(targetSize.Width - popupSize.Width, targetSize.Height + 7),
                PopupPrimaryAxis.Vertical)
        ];

        private void DataStore_DataChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshTopBarNotifications));
                return;
            }

            RefreshTopBarNotifications();
        }

        private void MainWindow_Closed(object sender, EventArgs e) =>
            DataStore.Instance.DataChanged -= DataStore_DataChanged;

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
            AddNavButton("\uE8A5", "Tagihan Mingguan", typeof(TagihanPage), () => NavigateTo(new TagihanPage(user, this)));

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
