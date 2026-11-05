using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;
using System.Windows.Threading;

namespace ClassFinance.Views
{
    public class SiswaRosterItem
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public Siswa Siswa { get; init; }
        public decimal TotalDibayar { get; init; }
        public int UnpaidCount { get; init; }
        public int ProsesCount { get; init; }
        public int TotalTagihanCount { get; init; }
        public int LastWeekNumber { get; init; }
        public DateTime? LastPaymentDate { get; init; }
        public MetodeTransaksi? LastPaymentMethod { get; init; }
        public bool CanManage { get; init; }
        public decimal OutstandingAmount { get; init; }
        public string Initials => MakeInitials(Siswa?.Name);
        public string LastPaymentDateText => LastPaymentDate?.ToString("dd MMM yyyy", IdId) ?? "Belum ada pembayaran";
        public string LastPaymentMethodText => !LastPaymentMethod.HasValue
            ? string.Empty
            : LastWeekNumber > 0
                ? $"{LastPaymentMethod.Value.ToDisplayLabel()} · Minggu ke-{LastWeekNumber}"
                : LastPaymentMethod.Value.ToDisplayLabel();
        public string StatusText => TotalTagihanCount == 0
            ? "Belum ada tagihan"
            : UnpaidCount > 0
                ? $"{UnpaidCount} minggu tertunggak"
                : ProsesCount > 0
                    ? "Refund diproses"
                    : $"Lunas s/d Mg {LastWeekNumber}";
        public Brush StatusForeground => FindBrush(
            TotalTagihanCount == 0 ? "TextGray" : UnpaidCount > 0 ? "StatusRed" : ProsesCount > 0 ? "StatusAmber" : "StatusGreen");
        public Brush StatusBackground => FindBrush(
            TotalTagihanCount == 0 ? "ElevatedCardBackground" : UnpaidCount > 0 ? "WarningBadgeBackground" : ProsesCount > 0 ? "WarningBadgeBackground" : "CashFlowIncomeFill");

        private static string MakeInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "S",
                1 => parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant(),
                _ => $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}"
            };
        }

        private static Brush FindBrush(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public sealed class StudentRosterPageNumber
    {
        public int PageIndex { get; init; }
        public string Label => (PageIndex + 1).ToString(CultureInfo.InvariantCulture);
        public bool IsCurrent { get; init; }
    }

    public partial class SiswaPage : Page
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");
        private const int StudentsPerPage = 5;
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private DateTime _selectedWeekStart;
        private bool _isUpdatingWeekSelector;
        private int _currentPageIndex;
        private ContextMenu _openStudentMenu;
        private Button _pendingStudentMenuToggle;

        public SiswaPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = currentUser switch
            {
                Siswa siswa => siswa.KelasId,
                WaliKelas waliKelas => waliKelas.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };
            _selectedWeekStart = JadwalTagihanMingguanService.MondayOf(DateTime.Today);

            bool canManage = currentUser is Bendahara || currentUser is WaliKelas;
            TambahButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void Refresh()
        {
            var store = DataStore.Instance;
            var students = store.Users.OfType<Siswa>()
                .Where(student => student.KelasId == _kelasId)
                .OrderBy(student => student.Name)
                .ToList();
            StudentCountText.Text = students.Count.ToString(IdId);

            var schedule = store.JadwalTagihanMingguanList.FirstOrDefault(item => item.KelasId == _kelasId);
            var currentWeekStart = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            UpdateWeekSelector(schedule, currentWeekStart);
            var weekNumber = JadwalTagihanMingguanService.GetWeekNumber(schedule, _selectedWeekStart);
            bool viewingCurrentWeek = _selectedWeekStart == currentWeekStart;
            PreviousWeekButton.IsEnabled = schedule != null &&
                _selectedWeekStart.AddDays(-7) >= JadwalTagihanMingguanService.MondayOf(schedule.StartsOn);
            NextWeekButton.IsEnabled = !viewingCurrentWeek && _selectedWeekStart < currentWeekStart;

            var selectedWeekBill = store.TagihanList
                .Where(bill => bill.KelasId == _kelasId && bill.IsWeeklyOccurrence && bill.PeriodStart?.Date == _selectedWeekStart)
                .OrderByDescending(bill => bill.Id)
                .FirstOrDefault();
            if (selectedWeekBill == null)
            {
                WeekPaidText.Inlines.Clear();
                WeekPaidText.Inlines.Add(new Run("—"));
                WeekPaidText.Foreground = FindBrush("TextGray");
                WeekOverdueText.Text = schedule == null
                    ? "Jadwal belum diatur"
                    : weekNumber == 0
                        ? "Jadwal belum dimulai"
                        : "Belum ada tagihan";
                WeekOverdueBadge.Background = FindBrush("SubtleBackground");
                WeekOverdueText.Foreground = FindBrush("TextGray");
                WeekOutstandingText.Text = "Rp 0";
                WeekOutstandingText.Foreground = FindBrush("TextGray");
            }
            else
            {
                var studentIds = students.Select(student => student.Id).ToHashSet();
                var currentEntries = store.TagihanSiswaList
                    .Where(entry => entry.TagihanId == selectedWeekBill.Id && studentIds.Contains(entry.SiswaId))
                    .ToList();
                var paidCount = currentEntries.Count(entry => entry.Status == StatusTagihan.Lunas);
                var unpaidEntries = currentEntries
                    .Where(entry => entry.Status != StatusTagihan.Proses && entry.AmountDue > 0)
                    .ToList();
                var outstanding = unpaidEntries.Sum(entry => entry.AmountDue);
                var overdueStudentCount = unpaidEntries.Select(entry => entry.SiswaId).Distinct().Count();
                SetWeekPaidCount(paidCount, students.Count);
                WeekPaidText.Foreground = FindBrush("TextDark");
                WeekOverdueText.Text = overdueStudentCount == 0
                    ? "Semua lunas"
                    : $"{overdueStudentCount} Tertunggak";
                WeekOverdueBadge.Background = FindBrush(overdueStudentCount == 0 ? "CashFlowIncomeFill" : "WarningBadgeBackground");
                WeekOverdueText.Foreground = FindBrush(overdueStudentCount == 0 ? "StatusGreen" : "StatusRed");
                WeekOutstandingText.Text = outstanding > 0
                    ? $"−Rp {outstanding.ToString("N0", IdId)}"
                    : "Rp 0";
                WeekOutstandingText.Foreground = FindBrush(outstanding > 0 ? "StatusRed" : "StatusGreen");
            }

            ApplyFilter();
        }

        private void UpdateWeekSelector(JadwalTagihanMingguan schedule, DateTime currentWeekStart)
        {
            _isUpdatingWeekSelector = true;
            WeekSelectorComboBox.Items.Clear();

            if (schedule == null)
            {
                WeekSelectorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "Jadwal belum diatur",
                    IsEnabled = false
                });
                WeekSelectorComboBox.SelectedIndex = 0;
                WeekSelectorComboBox.IsEnabled = false;
                _isUpdatingWeekSelector = false;
                return;
            }

            var firstWeek = JadwalTagihanMingguanService.MondayOf(schedule.StartsOn);
            var lastWeek = currentWeekStart < firstWeek ? firstWeek : currentWeekStart;
            var options = new List<ComboBoxItem>();
            for (var weekStart = lastWeek; weekStart >= firstWeek; weekStart = weekStart.AddDays(-7))
            {
                var weekEnd = weekStart.AddDays(6);
                var weekNumber = JadwalTagihanMingguanService.GetWeekNumber(schedule, weekStart);
                options.Add(new ComboBoxItem
                {
                    Content = $"Kas Minggu Ke-{weekNumber}",
                    Tag = weekStart,
                    ToolTip = $"{weekStart.ToString("dd MMM yyyy", IdId)} – {weekEnd.ToString("dd MMM yyyy", IdId)}"
                });
            }

            var selected = options.FirstOrDefault(option => option.Tag is DateTime week && week == _selectedWeekStart);
            if (selected == null)
            {
                selected = options.FirstOrDefault();
                if (selected?.Tag is DateTime latestWeek)
                    _selectedWeekStart = latestWeek;
            }

            foreach (var option in options)
                WeekSelectorComboBox.Items.Add(option);

            WeekSelectorComboBox.SelectedItem = selected;
            WeekSelectorComboBox.IsEnabled = options.Count > 0;
            _isUpdatingWeekSelector = false;
        }

        private void WeekSelectorComboBox_DropDownOpened(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ResizeWeekSelectorPopup));
        }

        private void ResizeWeekSelectorPopup()
        {
            if (WeekSelectorComboBox.Template.FindName("dropDownScrollViewer", WeekSelectorComboBox) is not ScrollViewer scrollViewer)
                return;

            WeekSelectorComboBox.UpdateLayout();
            var contentHeight = 0d;
            for (var index = 0; index < WeekSelectorComboBox.Items.Count; index++)
            {
                if (WeekSelectorComboBox.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement item)
                    continue;

                var itemHeight = item.ActualHeight > 0 ? item.ActualHeight : item.DesiredSize.Height;
                contentHeight += itemHeight + item.Margin.Top + item.Margin.Bottom;
            }

            if (contentHeight <= 0)
                contentHeight = WeekSelectorComboBox.Items.Count * 36d;

            const double maximumViewportHeight = 194;
            scrollViewer.Height = Math.Min(maximumViewportHeight, contentHeight);
        }

        private void WeekSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingWeekSelector || WeekSelectorComboBox.SelectedItem is not ComboBoxItem selected ||
                selected.Tag is not DateTime weekStart || weekStart == _selectedWeekStart)
                return;

            _selectedWeekStart = weekStart;
            Refresh();
        }

        private void SetWeekPaidCount(int paidCount, int totalStudents)
        {
            WeekPaidText.Inlines.Clear();
            WeekPaidText.Inlines.Add(new Run(paidCount.ToString(IdId))
            {
                FontSize = 25,
                FontWeight = FontWeights.Medium
            });
            WeekPaidText.Inlines.Add(new Run(" / ")
            {
                FontSize = 25,
                FontWeight = FontWeights.Medium
            });
            WeekPaidText.Inlines.Add(new Run(totalStudents.ToString(IdId))
            {
                FontSize = 25,
                FontWeight = FontWeights.Medium
            });
            WeekPaidText.Inlines.Add(new Run(" lunas"));
        }

        private SiswaRosterItem ToRosterItem(Siswa student)
        {
            var store = DataStore.Instance;
            var weeklyBills = store.TagihanList
                .Where(bill => bill.KelasId == student.KelasId && bill.IsWeeklyOccurrence)
                .ToDictionary(bill => bill.Id);
            var entries = store.TagihanSiswaList
                .Where(entry => entry.SiswaId == student.Id && weeklyBills.ContainsKey(entry.TagihanId))
                .ToList();
            var unpaidEntries = entries
                .Where(entry => entry.Status != StatusTagihan.Proses && entry.AmountDue > 0)
                .ToList();
            var relatedEntryIds = entries.Select(entry => entry.Id).ToHashSet();
            var lastPayment = store.TransaksiList
                .Where(transaction => transaction.Type == JenisTransaksi.Masuk &&
                                      (transaction.SiswaId == student.Id ||
                                       transaction.TagihanSiswaId.HasValue && relatedEntryIds.Contains(transaction.TagihanSiswaId.Value)))
                .OrderByDescending(transaction => transaction.Date)
                .ThenByDescending(transaction => transaction.Id)
                .FirstOrDefault();
            var lastEntry = lastPayment?.TagihanSiswaId is int entryId
                ? entries.FirstOrDefault(entry => entry.Id == entryId)
                : null;
            var lastWeekNumber = lastEntry != null && weeklyBills.TryGetValue(lastEntry.TagihanId, out var lastBill)
                ? JadwalTagihanMingguanService.GetWeekNumber(lastBill)
                : 0;

            return new SiswaRosterItem
            {
                Siswa = student,
                TotalDibayar = entries.Sum(entry => entry.JumlahDibayar),
                UnpaidCount = unpaidEntries.Count,
                ProsesCount = entries.Count(entry => entry.Status == StatusTagihan.Proses),
                TotalTagihanCount = entries.Count,
                OutstandingAmount = unpaidEntries.Sum(entry => entry.AmountDue),
                LastPaymentDate = lastPayment?.Date,
                LastPaymentMethod = lastPayment?.Metode,
                LastWeekNumber = lastWeekNumber,
                CanManage = _currentUser is Bendahara
            };
        }

        private void ApplyFilter()
        {
            // XAML events can fire while InitializeComponent is still creating
            // controls. Ignore those early events until all filter/list controls exist.
            if (SearchBox == null || FilterCombo == null || SortCombo == null || SiswaItems == null || EmptyText == null ||
                StudentPagination == null || PreviousPageButton == null || NextPageButton == null || PaginationPages == null)
                return;

            var store = DataStore.Instance;
            var keyword = SearchBox.Text?.Trim() ?? string.Empty;
            var rows = store.Users.OfType<Siswa>()
                .Where(student => student.KelasId == _kelasId)
                .Select(ToRosterItem)
                .Where(item => string.IsNullOrWhiteSpace(keyword) ||
                               (item.Siswa.Name?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                               (item.Siswa.Nis?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ?? false));

            var filter = (FilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
            rows = filter switch
            {
                "Unpaid" => rows.Where(item => item.UnpaidCount > 0),
                "Paid" => rows.Where(item => item.TotalTagihanCount > 0 && item.UnpaidCount == 0 && item.ProsesCount == 0),
                "NoBill" => rows.Where(item => item.TotalTagihanCount == 0),
                _ => rows
            };

            var sort = (SortCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Unpaid";
            var sortedRows = sort == "Name"
                ? rows.OrderBy(item => item.Siswa.Name).ToList()
                : rows.OrderByDescending(item => item.UnpaidCount)
                    .ThenByDescending(item => item.OutstandingAmount)
                    .ThenBy(item => item.Siswa.Name)
                    .ToList();

            int pageCount = Math.Max(1, (int)Math.Ceiling(sortedRows.Count / (double)StudentsPerPage));
            _currentPageIndex = Math.Clamp(_currentPageIndex, 0, pageCount - 1);
            SiswaItems.ItemsSource = sortedRows
                .Skip(_currentPageIndex * StudentsPerPage)
                .Take(StudentsPerPage)
                .ToList();
            StudentPagination.Visibility = sortedRows.Count > StudentsPerPage ? Visibility.Visible : Visibility.Collapsed;
            PreviousPageButton.IsEnabled = _currentPageIndex > 0;
            NextPageButton.IsEnabled = _currentPageIndex < pageCount - 1;

            int visiblePageCount = Math.Min(5, pageCount);
            int firstVisiblePage = Math.Max(0, Math.Min(_currentPageIndex - visiblePageCount / 2, pageCount - visiblePageCount));
            PaginationPages.ItemsSource = Enumerable.Range(firstVisiblePage, visiblePageCount)
                .Select(pageIndex => new StudentRosterPageNumber
                {
                    PageIndex = pageIndex,
                    IsCurrent = pageIndex == _currentPageIndex
                })
                .ToList();

            EmptyText.Visibility = sortedRows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyText.Text = string.IsNullOrWhiteSpace(keyword) && store.Users.OfType<Siswa>().All(student => student.KelasId != _kelasId)
                ? "Belum ada siswa terdaftar di kelas ini."
                : "Siswa tidak ditemukan untuk filter ini.";
        }

        private static Brush FindBrush(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentPageIndex = 0;
            ApplyFilter();
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentPageIndex = 0;
            ApplyFilter();
        }

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentPageIndex = 0;
            ApplyFilter();
        }

        private void PreviousWeekButton_Click(object sender, RoutedEventArgs e)
        {
            var schedule = DataStore.Instance.JadwalTagihanMingguanList.FirstOrDefault(item => item.KelasId == _kelasId);
            if (schedule == null)
                return;

            var previousWeek = _selectedWeekStart.AddDays(-7);
            if (previousWeek < JadwalTagihanMingguanService.MondayOf(schedule.StartsOn))
                return;

            _selectedWeekStart = previousWeek;
            Refresh();
        }

        private void NextWeekButton_Click(object sender, RoutedEventArgs e)
        {
            var currentWeekStart = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            if (_selectedWeekStart >= currentWeekStart)
                return;

            _selectedWeekStart = _selectedWeekStart.AddDays(7);
            if (_selectedWeekStart > currentWeekStart)
                _selectedWeekStart = currentWeekStart;
            Refresh();
        }

        private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex <= 0)
                return;

            _currentPageIndex--;
            ApplyFilter();
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPageIndex++;
            ApplyFilter();
        }

        private void PageNumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not int pageIndex || pageIndex < 0)
                return;

            _currentPageIndex = pageIndex;
            ApplyFilter();
        }

        private void TambahButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahSiswaDialog(_kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void Hapus_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara || sender is not FrameworkElement element || element.Tag is not Siswa siswa)
                return;

            var confirm = AppMessageBox.Show(_mainWindow, $"Hapus data siswa '{siswa.Name}'?", "Hapus siswa",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                DataStore.Instance.HapusSiswa(siswa.Id);
                Refresh();
            }
        }

        private void EditSiswa_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara && sender is FrameworkElement element && element.Tag is Siswa siswa)
                _mainWindow.ShowModal(new EditSiswaDialog(siswa, _mainWindow, Refresh));
        }

        private void ShowStudentActions_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara || sender is not Button button ||
                button.DataContext is not SiswaRosterItem rosterItem)
                return;

            if (CloseOpenStudentMenuIfSameButton(button))
                return;

            if (_openStudentMenu?.IsOpen == true)
                _openStudentMenu.IsOpen = false;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("PopupActionMenu"),
                Placement = PlacementMode.Bottom
            };
            menu.Items.Add(CreateStudentMenuItem("Ubah data siswa", rosterItem.Siswa, EditSiswa_Click));
            menu.Items.Add(CreateStudentMenuItem("Hapus siswa", rosterItem.Siswa, Hapus_Click));
            menu.PlacementTarget = button;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_pendingStudentMenuToggle, button)) return;
                if (ReferenceEquals(_openStudentMenu, menu))
                    _openStudentMenu = null;
            };

            _openStudentMenu = menu;
            menu.IsOpen = menu.Items.Count > 0;
        }

        private bool CloseOpenStudentMenuIfSameButton(Button button)
        {
            bool togglingCurrent = ReferenceEquals(_pendingStudentMenuToggle, button) ||
                ReferenceEquals(_openStudentMenu?.PlacementTarget, button);
            _pendingStudentMenuToggle = null;
            if (!togglingCurrent)
                return false;

            if (_openStudentMenu != null)
                _openStudentMenu.IsOpen = false;
            _openStudentMenu = null;
            return true;
        }

        private void StudentActionMenuButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _pendingStudentMenuToggle = sender is Button button &&
                ReferenceEquals(_openStudentMenu?.PlacementTarget, button) ? button : null;
        }

        private static MenuItem CreateStudentMenuItem(string header, Siswa siswa, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = siswa };
            item.Click += onClick;
            return item;
        }

        private void SiswaRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Siswa siswa)
            {
                _mainWindow.ShowModal(new StudentPaymentHistoryDialog(_currentUser, _mainWindow, siswa, Refresh));
                e.Handled = true;
            }
        }

        private void SiswaRow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space) || sender is not FrameworkElement element || element.Tag is not Siswa siswa)
                return;

            _mainWindow.ShowModal(new StudentPaymentHistoryDialog(_currentUser, _mainWindow, siswa, Refresh));
            e.Handled = true;
        }
    }
}
