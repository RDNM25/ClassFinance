using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views;
using ClassFinance.Views.Dialogs;
using ClassFinance.Behaviors;

namespace ClassFinance
{
    internal sealed class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
            nameof(From), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));
        public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
            nameof(To), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));
        public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
            nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue,
            AnimationClock animationClock)
        {
            var progress = animationClock.CurrentProgress ?? 1d;
            progress = EasingFunction?.Ease(progress) ?? progress;
            var value = From.Value + (To.Value - From.Value) * progress;
            return new GridLength(value, GridUnitType.Pixel);
        }
    }

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
        private UserControl _currentPage;
        private readonly Dictionary<string, UserControl> _pageCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _pageCacheRevisions = new(StringComparer.Ordinal);
        private User _currentUser;
        private bool _isDarkMode;
        private bool _isSidebarCollapsed;
        private bool _sidebarExpandedContentPending;
        private int _sidebarTransitionVersion;
        private bool _suppressNotificationToggle;
        private bool _suppressProfileToggle;
        private bool _suppressDateCalendarToggle;
        private readonly DispatcherTimer _weeklyBillingTimer = new() { Interval = TimeSpan.FromMinutes(1) };
        private readonly DispatcherTimer _topBarDateTimer = new();
        private DateTime? _weeklyBillsCheckedForWeek;
        private bool _isCheckingWeeklyBills;
        private CancellationTokenSource _pageNavigationCts;
        private DateTime _pageLoadingStartedAt;
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public MainWindow()
        {
            ButtonHoverAnimationBehavior.Register();
            InitializeComponent();
            _weeklyBillingTimer.Tick += WeeklyBillingTimer_Tick;
            _topBarDateTimer.Tick += TopBarDateTimer_Tick;
            UpdateTopBarDate();
            ScheduleNextTopBarDateRefresh();
            Deactivated += MainWindow_Deactivated;
            DateCalendarPopup.CustomPopupPlacementCallback = PlaceDateCalendarPopup;
            NotificationPopup.CustomPopupPlacementCallback = PlacePopupAlignedRight;
            ProfilePopup.CustomPopupPlacementCallback = PlaceProfilePopup;
            _isDarkMode = LoadDarkModePreference();
            ApplyTheme();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            Closed += MainWindow_Closed;
            try
            {
                // Paint the startup screen before checking the API and MariaDB connection.
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                await KasMateApiClient.Instance.CheckHealthAsync();
            }
            catch (Exception exception)
            {
                StartupLoadingOverlay.Visibility = Visibility.Collapsed;
                AppMessageBox.Show(this,
                    $"Tidak dapat terhubung ke backend KasMate. Pastikan Apache dan MySQL/MariaDB XAMPP aktif, lalu periksa URL API dan migrasi database.\n\n{exception.Message}",
                    "Koneksi Database Gagal", MessageBoxButton.OK, MessageBoxImage.Error, AppMessageIcon.Database);
                Close();
                return;
            }

            DataStore.Instance.DataChanged += DataStore_DataChanged;
            StartupLoadingOverlay.Visibility = Visibility.Collapsed;
            ShowLogin();
        }

        private static string ThemePreferencePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassFinance", "dark-mode.pref");

        private static string ProfileImagePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassFinance", "ProfileImages", $"user-{userId}.png");

        internal bool IsDarkMode => _isDarkMode;
        internal string CurrentProfileImagePath => _currentUser == null ? null : ProfileImagePath(_currentUser.Id);

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
            SetThemeMode(!_isDarkMode);
        }

        internal void SetThemeMode(bool darkMode)
        {
            _isDarkMode = darkMode;
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
            CancelPageNavigation();
            _pageCache.Clear();
            _pageCacheRevisions.Clear();
            _weeklyBillingTimer.Stop();
            _weeklyBillsCheckedForWeek = null;
            _sidebarTransitionVersion++;
            _sidebarExpandedContentPending = false;
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            ProfileButton.BeginAnimation(FrameworkElement.WidthProperty, null);
            ResetSidebarContentOpacity();
            _isSidebarCollapsed = false;
            SidebarColumn.Width = new GridLength(0);
            SidebarBorder.Visibility = Visibility.Collapsed;
            TopBarBorder.Visibility = Visibility.Collapsed;
            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;

            _currentUser = null;
            _currentPage = new LoginPage(this);
            PresentPage(_currentPage);
        }

        public async Task ShowShellAsync(User user)
        {
            // Save the logged-in user so we can pass it to the Dashboard later
            _currentUser = user;

            SidebarBorder.Visibility = Visibility.Visible;
            TopBarBorder.Visibility = Visibility.Visible;
            SetSidebarCollapsed(false);
            SidebarUserText.Text = GetSidebarClassLabel(user);
            UpdateTopBar();
            BuildNav(user);

            await NavigateToPageAsync(() => new DashboardPage(user, this), typeof(DashboardPage), "dashboard");

            // Show the dashboard immediately, then reconcile the current week's bills
            // asynchronously so first login does not wait on several database writes.
            await EnsureCurrentWeekBillsAsync(showError: true);
            _weeklyBillingTimer.Start();
        }

        private async void WeeklyBillingTimer_Tick(object sender, EventArgs e) =>
            await EnsureCurrentWeekBillsAsync(showError: false);

        private async Task EnsureCurrentWeekBillsAsync(bool showError)
        {
            var currentWeek = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            if (_weeklyBillsCheckedForWeek == currentWeek || _isCheckingWeeklyBills)
                return;

            _isCheckingWeeklyBills = true;
            try
            {
                var createdCount = 0;
                foreach (var schedule in DataStore.Instance.JadwalTagihanMingguanList
                             .Where(item => item.IsActive).ToList())
                {
                    var createdBills = await JadwalTagihanMingguanService
                        .PastikanMingguTerbuatAsync(schedule.KelasId, DateTime.Today);
                    createdCount += createdBills.Count;
                }

                _weeklyBillsCheckedForWeek = currentWeek;
                if (createdCount == 0)
                    return;

                RefreshTopBarNotifications();
                switch (_currentPage)
                {
                    case DashboardPage dashboardPage:
                        dashboardPage.RefreshAfterWeeklyBillingUpdate();
                        break;
                    case SiswaPage siswaPage:
                        siswaPage.RefreshAfterWeeklyBillingUpdate();
                        break;
                    case TagihanPage tagihanPage:
                        tagihanPage.RefreshAfterWeeklyBillingUpdate();
                        break;
                }

                if (ModalHost.Content is StudentPaymentHistoryDialog paymentHistoryDialog)
                    paymentHistoryDialog.RefreshAfterWeeklyBillingUpdate();
            }
            catch (Exception exception)
            {
                if (showError)
                {
                    AppMessageBox.Show(this,
                        $"Jadwal tagihan mingguan tidak dapat diperbarui. Data yang sudah tersimpan tetap tersedia.\n\n{exception.Message}",
                        "Jadwal Mingguan", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Jadwal mingguan belum dapat diperbarui: {exception}");
                }
            }
            finally
            {
                _isCheckingWeeklyBills = false;
            }
        }

        public void NavigateTo<TControl>(Func<TControl> pageFactory, string cacheKey = null) where TControl : UserControl
        {
            _ = NavigateToPageAsync(() => pageFactory(), typeof(TControl), cacheKey);
        }

        private async Task NavigateToPageAsync(Func<UserControl> pageFactory, Type pageType, string cacheKey)
        {
            _pageNavigationCts?.Cancel();
            var navigationCts = new CancellationTokenSource();
            _pageNavigationCts = navigationCts;
            var canReuseImmediately = !string.IsNullOrWhiteSpace(cacheKey) &&
                _pageCache.ContainsKey(cacheKey) &&
                _pageCacheRevisions.TryGetValue(cacheKey, out var currentRevision) &&
                currentRevision == DataStore.Instance.DataRevision;
            if (canReuseImmediately)
                HidePageSkeleton();
            else
                ShowPageSkeleton(pageType);

            try
            {
                // Let WPF paint the placeholder before constructing the next page.
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                navigationCts.Token.ThrowIfCancellationRequested();
                SetPageLoadingProgress(24);

                UserControl page = null;
                var isCached = !string.IsNullOrWhiteSpace(cacheKey) && _pageCache.TryGetValue(cacheKey, out page);
                if (!isCached)
                {
                    page = pageFactory();
                    if (!string.IsNullOrWhiteSpace(cacheKey))
                    {
                        _pageCache[cacheKey] = page;
                        _pageCacheRevisions[cacheKey] = DataStore.Instance.DataRevision;
                    }
                }

                if (page == null)
                    throw new InvalidOperationException("Halaman tidak tersedia.");

                if (ReferenceEquals(_currentPage, page) && page.IsLoaded)
                {
                    SetPageLoadingProgress(100);
                    return;
                }

                if (isCached && (!_pageCacheRevisions.TryGetValue(cacheKey, out var cachedRevision) ||
                                 cachedRevision != DataStore.Instance.DataRevision))
                {
                    RefreshCachedPage(page);
                    _pageCacheRevisions[cacheKey] = DataStore.Instance.DataRevision;
                }
                SetPageLoadingProgress(52);

                var pageLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                RoutedEventHandler loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    page.Loaded -= loadedHandler;
                    pageLoaded.TrySetResult(true);
                };
                page.Loaded += loadedHandler;

                _currentPage = page;
                PresentPage(page);
                UpdateNavHighlight();
                SetPageLoadingProgress(76);

                await pageLoaded.Task.WaitAsync(navigationCts.Token);
                SetPageLoadingProgress(100);
            }
            catch (OperationCanceledException) when (navigationCts.IsCancellationRequested)
            {
                // A newer navigation replaced this one.
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(_pageNavigationCts, navigationCts))
                    AppMessageBox.Show(this, exception.Message, "Halaman tidak dapat dimuat",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (ReferenceEquals(_pageNavigationCts, navigationCts))
                {
                    if (!canReuseImmediately)
                    {
                        var minimumVisibleTime = TimeSpan.FromMilliseconds(300);
                        var elapsed = DateTime.UtcNow - _pageLoadingStartedAt;
                        if (elapsed < minimumVisibleTime)
                            await Task.Delay(minimumVisibleTime - elapsed);
                    }

                    if (ReferenceEquals(_pageNavigationCts, navigationCts))
                    {
                        HidePageSkeleton();
                        _pageNavigationCts = null;
                    }
                }
                navigationCts.Dispose();
            }
        }

        private void ShowPageSkeleton(Type pageType)
        {
            _pageLoadingStartedAt = DateTime.UtcNow;
            PageLoadingOverlay.BeginAnimation(OpacityProperty, null);
            PageLoadingOverlay.Opacity = 1;
            PageSkeletonContent.ContentTemplate = pageType == typeof(SiswaPage)
                ? (DataTemplate)FindResource("StudentsSkeletonTemplate")
                : pageType == typeof(RiwayatTransaksiPage)
                    ? (DataTemplate)FindResource("HistorySkeletonTemplate")
                    : pageType == typeof(TagihanPage)
                        ? (DataTemplate)FindResource("TagihanSkeletonTemplate")
                        : (DataTemplate)FindResource("DashboardSkeletonTemplate");
            PageLoadingOverlay.Visibility = Visibility.Visible;
            PageLoadingBar.Value = 6;
            PageLoadingBar.Visibility = Visibility.Visible;
            StartSkeletonPulse(PageSkeletonContent);
        }

        private void SetPageLoadingProgress(double progress)
        {
            if (PageLoadingBar.Visibility == Visibility.Visible)
                PageLoadingBar.Value = Math.Clamp(progress, PageLoadingBar.Minimum, PageLoadingBar.Maximum);
        }

        private void HidePageSkeleton()
        {
            StopSkeletonPulse(PageSkeletonContent);
            PageLoadingOverlay.Visibility = Visibility.Collapsed;
            PageLoadingBar.Visibility = Visibility.Collapsed;
        }

        private static void StartSkeletonPulse(UIElement skeletonContent)
        {
            var pulse = new DoubleAnimation(0.68, 0.96, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            skeletonContent.BeginAnimation(OpacityProperty, pulse);
        }

        private static void StopSkeletonPulse(UIElement skeletonContent)
        {
            skeletonContent.BeginAnimation(OpacityProperty, null);
            skeletonContent.Opacity = 1;
        }

        private void CancelPageNavigation()
        {
            var pending = _pageNavigationCts;
            _pageNavigationCts = null;
            pending?.Cancel();
            HidePageSkeleton();
        }

        private void PresentPage(UserControl page)
        {
            page.BeginAnimation(UIElement.OpacityProperty, null);
            page.Opacity = 0;
            ContentHost.Content = page;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            page.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        public void MarkPageDataCurrent(UserControl page)
        {
            if (page == null || !ReferenceEquals(_currentPage, page))
                return;

            foreach (var cachedPage in _pageCache)
            {
                if (!ReferenceEquals(cachedPage.Value, page))
                    continue;

                _pageCacheRevisions[cachedPage.Key] = DataStore.Instance.DataRevision;
                return;
            }
        }


        private void UpdateTopBar()
        {
            if (_currentUser == null)
                return;

            UpdateTopBarDate();
            TopBarProfileNameText.Text = string.IsNullOrWhiteSpace(_currentUser.Name)
                ? _currentUser.Username
                : _currentUser.Name;
            TopBarProfileRoleText.Text = GetRoleLabel(_currentUser.Role);
            ProfileMenuNameText.Text = TopBarProfileNameText.Text;
            var username = string.IsNullOrWhiteSpace(_currentUser.Username) ? string.Empty : _currentUser.Username;
            ProfileMenuUsernameText.Text = string.IsNullOrEmpty(username)
                ? GetRoleLabel(_currentUser.Role)
                : $"{username} · {GetRoleLabel(_currentUser.Role)}";
            TopBarProfileInitialsText.Text = GetProfileInitials(_currentUser.Name, _currentUser.Role);
            ProfileMenuInitialsText.Text = TopBarProfileInitialsText.Text;
            RefreshProfileAvatar();
            RefreshTopBarNotifications();
        }

        private void UpdateTopBarDate() =>
            TopBarDateText.Text = DateTime.Today.ToString("dddd, d MMM yyyy", IdId);

        private void ScheduleNextTopBarDateRefresh()
        {
            var now = DateTime.Now;
            _topBarDateTimer.Stop();
            _topBarDateTimer.Interval = now.Date.AddDays(1) - now;
            _topBarDateTimer.Start();
        }

        private void TopBarDateTimer_Tick(object sender, EventArgs e)
        {
            UpdateTopBarDate();
            ScheduleNextTopBarDateRefresh();
        }

        private static string GetRoleLabel(Role role) => role switch
        {
            Role.Bendahara => "Bendahara",
            Role.WaliKelas => "Wali Kelas",
            _ => "Siswa"
        };

        private static string GetSidebarClassLabel(User user)
        {
            var classId = user switch
            {
                Siswa student => student.KelasId,
                WaliKelas waliKelas => waliKelas.KelasId,
                _ => DataStore.Instance.KelasList.FirstOrDefault()?.Id ?? 0
            };
            var kelas = DataStore.Instance.KelasList.FirstOrDefault(item => item.Id == classId);
            return kelas == null
                ? "Data kelas tidak tersedia"
                : $"Kelas {kelas.Name}\nTahun ajaran {kelas.AcademicYear}";
        }

        internal static string GetProfileInitials(string name, Role role)
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

        internal void ApplyProfileUsername(string username)
        {
            if (_currentUser == null || string.IsNullOrWhiteSpace(username))
                return;

            _currentUser.Username = username;
            var storedUser = DataStore.Instance.Users.FirstOrDefault(user =>
                user.Id == _currentUser.Id && user.Role == _currentUser.Role);
            if (storedUser != null)
                storedUser.Username = username;

            UpdateTopBar();
        }

        internal void SaveProfilePicture(string sourcePath)
        {
            if (_currentUser == null)
                throw new InvalidOperationException("Sign in before changing the profile photo.");
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The selected profile photo could not be found.", sourcePath);

            var image = LoadProfileBitmap(sourcePath, 512);
            var longestSide = Math.Max(image.PixelWidth, image.PixelHeight);
            BitmapSource imageToSave = image;
            if (longestSide > 256)
            {
                var scale = 256d / longestSide;
                var resized = new TransformedBitmap(image, new ScaleTransform(scale, scale));
                resized.Freeze();
                imageToSave = resized;
            }

            var destination = ProfileImagePath(_currentUser.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(imageToSave));
            using (var stream = File.Create(destination))
                encoder.Save(stream);

            RefreshProfileAvatar();
        }

        private static BitmapImage LoadProfileBitmap(string path, int decodePixelWidth)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void RefreshProfileAvatar()
        {
            BitmapImage image = null;
            var path = CurrentProfileImagePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    image = LoadProfileBitmap(path, 256);
                }
                catch (Exception exception) when (exception is IOException or NotSupportedException or FormatException)
                {
                    image = null;
                }
            }

            var hasImage = image != null;
            TopBarProfileImage.Source = image;
            TopBarProfileImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
            TopBarProfileInitialsText.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
            ProfileMenuImage.Source = image;
            ProfileMenuImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
            ProfileMenuInitialsText.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
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

            DateCalendarPopup.IsOpen = false;
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

            DateCalendarPopup.IsOpen = false;
            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = !ProfilePopup.IsOpen;
        }

        private void TopBarDateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressDateCalendarToggle)
            {
                _suppressDateCalendarToggle = false;
                e.Handled = true;
                return;
            }

            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;
            TopBarCalendar.DisplayDate = DateTime.Today;
            TopBarCalendar.DisplayMode = CalendarMode.Month;
            DateCalendarPopup.IsOpen = !DateCalendarPopup.IsOpen;
        }

        private void ProfileSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ProfilePopup.IsOpen = false;
            if (_currentUser != null)
                ShowModal(new ProfileSettingsDialog(_currentUser, this));
        }

        private void ProfilePopup_Opened(object sender, EventArgs e) => AnimateProfileChevron(180);

        private void ProfilePopup_Closed(object sender, EventArgs e) => AnimateProfileChevron(0);

        private void AnimateProfileChevron(double angle)
        {
            if (ProfileChevron.RenderTransform is not RotateTransform rotation)
                return;

            var animation = new DoubleAnimation(rotation.Angle, angle, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            rotation.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var source = e.OriginalSource as DependencyObject;
            var onDateCalendarPopup = IsWithin(source, DateCalendarPopup.Child);
            var onNotificationPopup = IsWithin(source, NotificationPopup.Child);
            var onProfilePopup = IsWithin(source, ProfilePopup.Child);
            if (onDateCalendarPopup || onNotificationPopup || onProfilePopup)
                return;

            var onDateButton = IsWithin(source, TopBarDateButton);
            var onNotificationButton = IsWithin(source, NotificationButton);
            var onProfileButton = IsWithin(source, ProfileButton);

            if (DateCalendarPopup.IsOpen && onDateButton)
            {
                DateCalendarPopup.IsOpen = false;
                SuppressNextDateCalendarToggle();
                e.Handled = true;
                return;
            }

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

            if (!onDateButton)
                DateCalendarPopup.IsOpen = false;
            if (!onNotificationButton)
                NotificationPopup.IsOpen = false;
            if (!onProfileButton)
                ProfilePopup.IsOpen = false;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || (!DateCalendarPopup.IsOpen && !NotificationPopup.IsOpen && !ProfilePopup.IsOpen))
                return;

            DateCalendarPopup.IsOpen = false;
            NotificationPopup.IsOpen = false;
            ProfilePopup.IsOpen = false;
            e.Handled = true;
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            DateCalendarPopup.IsOpen = false;
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

        private void SuppressNextDateCalendarToggle()
        {
            _suppressDateCalendarToggle = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _suppressDateCalendarToggle = false));
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

        private static CustomPopupPlacement[] PlaceDateCalendarPopup(Size popupSize, Size targetSize, Point offset) =>
        [
            new CustomPopupPlacement(
                new Point(0, targetSize.Height + 7),
                PopupPrimaryAxis.Vertical)
        ];

        private static CustomPopupPlacement[] PlaceProfilePopup(Size popupSize, Size targetSize, Point offset) =>
        [
            new CustomPopupPlacement(
                new Point(-10, -popupSize.Height - 7),
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

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _weeklyBillingTimer.Stop();
            _topBarDateTimer.Stop();
            DataStore.Instance.DataChanged -= DataStore_DataChanged;
        }

        private static void RefreshCachedPage(UserControl page)
        {
            switch (page)
            {
                case DashboardPage dashboard:
                    dashboard.RefreshForNavigation();
                    break;
                case SiswaPage students:
                    students.RefreshForNavigation();
                    break;
                case TagihanPage bills:
                    bills.RefreshForNavigation();
                    break;
                case RiwayatTransaksiPage history:
                    history.RefreshForNavigation();
                    break;
            }
        }

        private void BuildNav(User user)
        {
            NavPanel.Children.Clear();

            // Separate the icon and text into two distinct strings
            AddNavButton("\uE80F", "Ringkasan", typeof(DashboardPage), () => NavigateTo(() => new DashboardPage(user, this), "dashboard"));
            AddNavButton("\uE716", "Data Siswa", typeof(SiswaPage), () => NavigateTo(() => new SiswaPage(user, this), "students"));
            AddNavButton("\uE81C", "Riwayat Transaksi", typeof(RiwayatTransaksiPage), () => NavigateTo(() => new RiwayatTransaksiPage(user, this), "history"));
            AddNavButton("\uE8A5", "Tagihan Mingguan", typeof(TagihanPage), () => NavigateTo(() => new TagihanPage(user, this), "bills"));

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
                    if (_isSidebarCollapsed || _sidebarExpandedContentPending)
                    {
                        button.ContentTemplate = (DataTemplate)FindResource("SidebarIconOnlyTemplate");
                        button.HorizontalContentAlignment = HorizontalAlignment.Center;
                        button.Padding = new Thickness(0);
                        button.MinHeight = 44;
                        if (button.Content is string[] labels && labels.Length > 1)
                            button.ToolTip = labels[1];
                    }
                    else
                    {
                        button.ClearValue(ContentControl.ContentTemplateProperty);
                        button.ClearValue(Control.HorizontalContentAlignmentProperty);
                        button.ClearValue(Control.PaddingProperty);
                        button.ClearValue(Control.MinHeightProperty);
                        button.ClearValue(FrameworkElement.ToolTipProperty);
                    }
                }
            }
        }

        private void SidebarLogo_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            SetSidebarCollapsed(!_isSidebarCollapsed);
            e.Handled = true;
        }

        private void SidebarLogo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space))
                return;

            SetSidebarCollapsed(!_isSidebarCollapsed);
            e.Handled = true;
        }

        private void SetSidebarCollapsed(bool collapsed)
        {
            var currentSidebarWidth = SidebarColumn.ActualWidth;
            var currentProfileWidth = ProfileButton.ActualWidth;
            var targetSidebarWidth = collapsed ? 76d : 230d;
            var animateSidebar = SidebarBorder.IsVisible && Math.Abs(currentSidebarWidth - targetSidebarWidth) > 0.5;
            var deferExpandedContent = !collapsed && animateSidebar;
            var transitionVersion = ++_sidebarTransitionVersion;

            _isSidebarCollapsed = collapsed;
            _sidebarExpandedContentPending = deferExpandedContent;
            ResetSidebarContentOpacity();
            SidebarColumn.Width = new GridLength(targetSidebarWidth);
            if (animateSidebar)
            {
                var sidebarAnimation = new GridLengthAnimation
                {
                    From = new GridLength(currentSidebarWidth),
                    To = new GridLength(targetSidebarWidth),
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                if (deferExpandedContent)
                    sidebarAnimation.Completed += (_, _) => CompleteSidebarExpand(transitionVersion);
                SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, sidebarAnimation);
            }
            else
            {
                SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            }

            var logoSize = collapsed ? 48 : 64;
            SidebarBrandHeader.Margin = collapsed ? new Thickness(0, 0, 0, 14) : new Thickness(16, 0, 12, 18);
            SidebarLogoColumn.Width = collapsed
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(64);
            SidebarLabelColumn.Width = collapsed
                ? new GridLength(0)
                : new GridLength(1, GridUnitType.Star);
            SidebarLogoBorder.Width = logoSize;
            SidebarLogoBorder.Height = logoSize;
            SidebarLogoBorder.Margin = new Thickness(0);
            SidebarLogoBorder.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            SidebarLogoClip.Rect = new Rect(0, 0, logoSize, logoSize);
            SidebarLogoClip.RadiusX = collapsed ? 13 : 16;
            SidebarLogoClip.RadiusY = collapsed ? 13 : 16;
            SidebarLogoImage.Width = collapsed ? 68 : 90;
            SidebarLogoImage.Height = collapsed ? 68 : 90;
            SidebarBrandText.Visibility = collapsed || deferExpandedContent ? Visibility.Collapsed : Visibility.Visible;
            SidebarUserText.Visibility = collapsed || deferExpandedContent ? Visibility.Collapsed : Visibility.Visible;

            var targetProfileWidth = collapsed ? 48d : targetSidebarWidth - 20d;
            ProfileButton.Width = collapsed ? targetProfileWidth : double.NaN;
            if (SidebarBorder.IsVisible && currentProfileWidth > 0 &&
                Math.Abs(currentProfileWidth - targetProfileWidth) > 0.5)
            {
                ProfileButton.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(
                    currentProfileWidth, targetProfileWidth, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
            }
            else
            {
                ProfileButton.BeginAnimation(FrameworkElement.WidthProperty, null);
            }
            ProfileButton.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            ProfileButton.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            ProfileButton.Margin = collapsed ? new Thickness(14, 12, 14, 8) : new Thickness(10, 12, 10, 8);
            ProfileButton.Padding = collapsed ? new Thickness(0) : new Thickness(10, 6, 10, 6);
            ProfileAvatarColumn.Width = new GridLength(collapsed ? 48 : 34);
            ProfileTextColumn.Width = collapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            ProfileChevronColumn.Width = new GridLength(collapsed ? 0 : 14);
            ProfileAvatarBorder.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            TopBarProfileNameText.Visibility = collapsed || deferExpandedContent ? Visibility.Collapsed : Visibility.Visible;
            TopBarProfileRoleText.Visibility = collapsed || deferExpandedContent ? Visibility.Collapsed : Visibility.Visible;
            ProfileChevron.Visibility = collapsed || deferExpandedContent ? Visibility.Collapsed : Visibility.Visible;

            var toggleLabel = collapsed ? "Luaskan sidebar" : "Ciutkan sidebar";
            SidebarLogoBorder.ToolTip = toggleLabel;
            UpdateNavHighlight();
        }

        private void CompleteSidebarExpand(int transitionVersion)
        {
            if (transitionVersion != _sidebarTransitionVersion || _isSidebarCollapsed || !SidebarBorder.IsVisible)
                return;

            _sidebarExpandedContentPending = false;
            SidebarBrandText.Visibility = Visibility.Visible;
            SidebarUserText.Visibility = Visibility.Visible;
            TopBarProfileNameText.Visibility = Visibility.Visible;
            TopBarProfileRoleText.Visibility = Visibility.Visible;
            ProfileChevron.Visibility = Visibility.Visible;
            UpdateNavHighlight();
            FadeInSidebarContent();
        }

        private void ResetSidebarContentOpacity()
        {
            UIElement[] elements =
            {
                SidebarBrandText, SidebarUserText, NavPanel,
                TopBarProfileNameText, TopBarProfileRoleText, ProfileChevron
            };

            foreach (var element in elements)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
            }
        }

        private void FadeInSidebarContent()
        {
            UIElement[] elements =
            {
                SidebarBrandText, SidebarUserText, NavPanel,
                TopBarProfileNameText, TopBarProfileRoleText, ProfileChevron
            };

            foreach (var element in elements)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 0;
                element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd
                });
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
