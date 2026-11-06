using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Globalization;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class TagihanSiswaDisplay
    {
        public TagihanSiswa Entry { get; set; }
        public string SiswaName { get; set; }
        public string SiswaNis { get; set; }
        public string Initials => MakeInitials(SiswaName);
        public string SiswaNisLabel => string.IsNullOrWhiteSpace(SiswaNis) ? "NIS belum tersedia" : $"NIS {SiswaNis}";
        public string LastPaymentDateText { get; set; } = "Belum ada pembayaran";
        public string LastPaymentMethodText { get; set; } = string.Empty;
        public bool CanMakePayment { get; set; }
        public bool HasPendingRefund { get; set; }
        public List<TagihanTransactionDisplay> Transactions { get; set; }

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
    }

    public class TagihanTransactionDisplay
    {
        public Transaksi Transaction { get; set; }
        public string PaymentMethodLabel => Transaction?.Metode.ToDisplayLabel() ?? "Tunai";
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShowActions => CanEdit || CanDelete;
    }

    public class TagihanDisplayItem
    {
        public Tagihan Tagihan { get; set; }
        public bool IsFullyPaid { get; set; }
        public string PeriodTitle { get; set; }
        public string PeriodRange { get; set; }
        public string PaymentSummary { get; set; }
        public double PaymentProgress { get; set; }
        public string PaymentProgressText { get; set; }
        public string AmountPerStudentLabel { get; set; }
        public string OutstandingLabel { get; set; }
        public bool CanSendReminder { get; set; }
    }

    public partial class TagihanPage : UserControl
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private Tagihan _selectedTagihan;
        private int? _initialTagihanId;
        private ContextMenu _openActionMenu;
        private Button _pendingActionMenuToggle;
        private int _metricColumnCount;
        private bool _isUpdatingMonthFilter;

        public TagihanPage(User currentUser, MainWindow mainWindow, int? initialTagihanId = null)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _initialTagihanId = initialTagihanId;
            _kelasId = currentUser switch
            {
                Siswa s => s.KelasId,
                WaliKelas w => w.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };

            AturJadwalButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMetricLayout(e.NewSize.Width);

        private void NestedScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PageScrollViewer == null || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                return;

            PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void UpdateMetricLayout(double availableWidth)
        {
            if (MetricGrid == null || availableWidth <= 0)
                return;

            var columns = availableWidth < 610 ? 1 : availableWidth < 920 ? 2 : 4;
            if (_metricColumnCount == columns && MetricGrid.RowDefinitions.Count > 0)
                return;

            _metricColumnCount = columns;
            MetricGrid.ColumnDefinitions.Clear();
            MetricGrid.RowDefinitions.Clear();
            for (var column = 0; column < columns; column++)
                MetricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cards = MetricGrid.Children.OfType<Border>().ToList();
            var rowCount = (cards.Count + columns - 1) / columns;
            for (var row = 0; row < rowCount; row++)
                MetricGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var index = 0; index < cards.Count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                Grid.SetRow(cards[index], row);
                Grid.SetColumn(cards[index], column);
                cards[index].Margin = new Thickness(0, 0, column == columns - 1 ? 0 : 10, row == rowCount - 1 ? 0 : 10);
            }
        }

        public void RefreshAfterWeeklyBillingUpdate() => Refresh();

        public void RefreshForNavigation() => Refresh();

        private void Refresh()
        {
            if (TagihanItems == null) return;

            var selectedTagihanId = _selectedTagihan?.Id ?? (TagihanItems.SelectedItem as TagihanDisplayItem)?.Tagihan.Id;
            var store = DataStore.Instance;
            var currentWeek = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            var schedule = store.JadwalTagihanMingguanList.FirstOrDefault(x => x.KelasId == _kelasId);
            var currentWeekNumber = JadwalTagihanMingguanService.GetWeekNumber(schedule, currentWeek);
            var currentWeekLabel = schedule == null
                ? "Jadwal belum diatur"
                : currentWeekNumber == 0
                    ? $"Jadwal dimulai {schedule.StartsOn.ToString("dd MMM yyyy", IdId)}"
                    : $"Minggu ke-{currentWeekNumber}";
            CurrentWeekText.Text = $"Minggu ini · {currentWeekLabel} · {FormatWeekRange(currentWeek, currentWeek.AddDays(6))}";
            ScheduleSummaryText.Text = schedule == null
                ? "Jadwal mingguan belum diatur."
                : !schedule.IsActive
                    ? $"{schedule.Name} · Rp {schedule.Amount.ToString("N0", IdId)} per siswa · dijeda"
                    : schedule.EndsOn.HasValue && schedule.EndsOn.Value.Date < DateTime.Today
                        ? $"{schedule.Name} · Rp {schedule.Amount.ToString("N0", IdId)} per siswa · berakhir {schedule.EndsOn.Value.ToString("dd MMM yyyy", IdId)}"
                    : $"{schedule.Name} · Rp {schedule.Amount.ToString("N0", IdId)} per siswa · aktif";

            WeeklyAmountMetricText.Text = schedule == null
                ? "Belum diatur"
                : $"Rp {schedule.Amount.ToString("N0", IdId)}";
            CurrentWeekMetricText.Text = schedule == null
                ? "—"
                : currentWeekNumber > 0 ? $"Minggu ke-{currentWeekNumber}" : "Belum dimulai";
            CurrentWeekMetricDetailText.Text = FormatWeekRange(currentWeek, currentWeek.AddDays(6));

            var currentWeekBill = store.TagihanList.FirstOrDefault(t => t.KelasId == _kelasId &&
                t.IsWeeklyOccurrence && t.PeriodStart?.Date == currentWeek);
            var currentWeekEntries = currentWeekBill == null
                ? new List<TagihanSiswa>()
                : store.TagihanSiswaList.Where(entry => entry.TagihanId == currentWeekBill.Id).ToList();
            var paidCount = currentWeekEntries.Count(entry => entry.Status == StatusTagihan.Lunas);
            var outstandingEntries = currentWeekEntries
                .Where(entry => entry.Status != StatusTagihan.Proses && entry.AmountDue > 0)
                .ToList();
            var outstanding = outstandingEntries.Sum(entry => entry.AmountDue);
            PaidMetricText.Text = currentWeekBill == null
                ? "Belum dibuat"
                : $"{paidCount} / {currentWeekEntries.Count}";
            PaidMetricDetailText.Text = currentWeekBill == null
                ? "Belum ada data pembayaran"
                : $"{paidCount} dari {currentWeekEntries.Count} siswa lunas";
            PaidProgressBar.Value = currentWeekEntries.Count == 0
                ? 0
                : (double)paidCount / currentWeekEntries.Count * 100;
            OutstandingMetricText.Text = $"Rp {outstanding.ToString("N0", IdId)}";
            OutstandingMetricDetailText.Text = currentWeekBill == null
                ? "Belum ada tagihan minggu ini"
                : $"{outstandingEntries.Count} siswa belum lunas";

            var weeklyBills = store.TagihanList
                .Where(t => t.KelasId == _kelasId && t.IsWeeklyOccurrence)
                .ToList();
            UpdateMonthFilter(weeklyBills);

            var entriesByBill = store.TagihanSiswaList
                .GroupBy(entry => entry.TagihanId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var query = weeklyBills
                .OrderByDescending(t => t.PeriodStart)
                .ThenByDescending(t => t.Id)
                .Select(t =>
                {
                    var entries = entriesByBill.TryGetValue(t.Id, out var billEntries)
                        ? billEntries
                        : new List<TagihanSiswa>();
                    var paidStudents = entries.Count(ts => ts.Status == StatusTagihan.Lunas);
                    var dueEntries = entries.Where(ts => ts.Status != StatusTagihan.Proses && ts.AmountDue > 0)
                        .ToList();
                    var outstanding = entries.Where(ts => ts.Status != StatusTagihan.Proses)
                        .Sum(ts => Math.Max(0, ts.AmountDue));
                    return new TagihanDisplayItem
                    {
                        Tagihan = t,
                        IsFullyPaid = entries.Any() && entries.All(ts => ts.Status == StatusTagihan.Lunas),
                        CanSendReminder = _currentUser is not Siswa && dueEntries.Count > 0,
                        PeriodTitle = $"Minggu ke-{JadwalTagihanMingguanService.GetWeekNumber(t)}",
                        PeriodRange = FormatWeekRange(t.PeriodStart.Value, t.PeriodEnd.Value),
                        PaymentProgress = entries.Count == 0 ? 0 : (double)paidStudents / entries.Count * 100,
                        PaymentProgressText = entries.Count == 0
                            ? "Belum ada data pembayaran"
                            : $"{paidStudents} dari {entries.Count} siswa lunas",
                        PaymentSummary = entries.Count == 0
                            ? "Belum ada data siswa untuk minggu ini"
                            : $"{paidStudents} dari {entries.Count} siswa lunas · sisa Rp {outstanding.ToString("N0", IdId)}",
                        AmountPerStudentLabel = $"Rp {t.Amount.ToString("N0", IdId)} / siswa",
                        OutstandingLabel = $"Sisa Rp {outstanding.ToString("N0", IdId)}"
                    };
                });

            if (TagihanFilterComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag)
            {
                query = filterTag switch
                {
                    "Selesai" => query.Where(x => x.IsFullyPaid),
                    "BelumSelesai" => query.Where(x => !x.IsFullyPaid),
                    "MingguIni" => query.Where(x => x.Tagihan.PeriodStart?.Date == currentWeek),
                    _ => query
                };
            }

            if (MonthFilterComboBox?.SelectedItem is ComboBoxItem monthItem &&
                monthItem.Tag is string monthKey && monthKey != "All")
            {
                query = query.Where(item => item.Tagihan.PeriodStart.HasValue &&
                    item.Tagihan.PeriodStart.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture) == monthKey);
            }

            var tagihanList = query.ToList();
            TagihanItems.ItemsSource = tagihanList;
            EmptyText.Visibility = tagihanList.Any() ? Visibility.Collapsed : Visibility.Visible;
            EmptyText.Text = store.TagihanList.Any(t => t.KelasId == _kelasId && t.IsWeeklyOccurrence)
                ? "Tidak ada tagihan yang sesuai dengan filter."
                : schedule == null
                    ? "Atur jadwal untuk membuat tagihan minggu pertama."
                    : !schedule.IsActive
                        ? "Jadwal mingguan sedang dijeda. Aktifkan kembali untuk membuat tagihan."
                        : "Belum ada minggu tagihan yang dibuat.";

            TagihanDisplayItem target = null;
            if (_initialTagihanId.HasValue)
            {
                target = tagihanList.FirstOrDefault(x => x.Tagihan.Id == _initialTagihanId.Value);
                target ??= tagihanList.FirstOrDefault(x => x.Tagihan.PeriodStart?.Date == currentWeek)
                    ?? tagihanList.FirstOrDefault();
                _initialTagihanId = null;
            }
            else if (selectedTagihanId.HasValue)
            {
                target = tagihanList.FirstOrDefault(x => x.Tagihan.Id == selectedTagihanId.Value)
                    ?? tagihanList.FirstOrDefault(x => x.Tagihan.PeriodStart?.Date == currentWeek)
                    ?? tagihanList.FirstOrDefault();
            }
            else
                target = tagihanList.FirstOrDefault(x => x.Tagihan.PeriodStart?.Date == currentWeek)
                    ?? tagihanList.FirstOrDefault();

            _selectedTagihan = target?.Tagihan;
            TagihanItems.SelectedItem = target;
            if (target != null)
                TagihanItems.ScrollIntoView(target);
            ApplyFilter();
            _mainWindow.MarkPageDataCurrent(this);
        }

        private void TagihanFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagihanItems != null) Refresh();
        }

        private void MonthFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdatingMonthFilter && TagihanItems != null)
                Refresh();
        }

        private void UpdateMonthFilter(IReadOnlyCollection<Tagihan> weeklyBills)
        {
            if (MonthFilterComboBox == null)
                return;

            var selectedMonthKey = (MonthFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
            var monthOptions = weeklyBills
                .Where(bill => bill.PeriodStart.HasValue)
                .Select(bill => new DateTime(bill.PeriodStart.Value.Year, bill.PeriodStart.Value.Month, 1))
                .Distinct()
                .OrderByDescending(month => month)
                .ToList();

            _isUpdatingMonthFilter = true;
            MonthFilterComboBox.Items.Clear();
            var allMonthsOption = new ComboBoxItem { Content = "Semua bulan", Tag = "All" };
            MonthFilterComboBox.Items.Add(allMonthsOption);

            ComboBoxItem selectedOption = allMonthsOption;
            foreach (var month in monthOptions)
            {
                var monthKey = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var option = new ComboBoxItem
                {
                    Content = month.ToString("MMMM yyyy", IdId),
                    Tag = monthKey
                };
                MonthFilterComboBox.Items.Add(option);
                if (monthKey == selectedMonthKey)
                    selectedOption = option;
            }

            MonthFilterComboBox.SelectedItem = selectedOption;
            _isUpdatingMonthFilter = false;
        }

        private void TagihanItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagihanItems.SelectedItem is TagihanDisplayItem displayItem)
            {
                _selectedTagihan = displayItem.Tagihan;
                DetailTitle.Text = $"Pembayaran · {GetPeriodTitle(_selectedTagihan)}";
                ApplyFilter();
            }
            else
            {
                _selectedTagihan = null;
                DetailTitle.Text = "Status pembayaran";
                ApplyFilter();
            }
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The initial IsSelected item can raise this event while InitializeComponent
            // is still building the rest of the page, before DetailItems exists.
            if (DetailItems == null) return;
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DetailItems == null) return;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_selectedTagihan == null)
            {
                DetailItems.ItemsSource = null;
                PaymentTableScrollViewer.Visibility = Visibility.Collapsed;
                DetailScrollViewer.Visibility = Visibility.Collapsed;
                NoSelectedTagihanText.Visibility = Visibility.Visible;
                NoMatchingPaymentsText.Visibility = Visibility.Collapsed;
                ReminderButton.Visibility = Visibility.Collapsed;
                SelectedWeekText.Text = string.Empty;
                return;
            }

            SelectedWeekText.Text = FormatWeekRange(_selectedTagihan.PeriodStart.Value, _selectedTagihan.PeriodEnd.Value);
            ReminderButton.Visibility = _currentUser is Siswa ? Visibility.Collapsed : Visibility.Visible;

            var store = DataStore.Instance;
            var students = store.Users.OfType<Siswa>().ToDictionary(student => student.Id);
            var transactionsByEntry = store.TransaksiList
                .Where(transaction => transaction.TagihanSiswaId.HasValue)
                .GroupBy(transaction => transaction.TagihanSiswaId.Value)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(transaction => transaction.Date).ToList());
            var billAmount = _selectedTagihan.Amount;
            bool canManage = _currentUser is Bendahara;
            var query = store.TagihanSiswaList
                .Where(ts => ts.TagihanId == _selectedTagihan.Id)
                .Select(ts =>
                {
                    students.TryGetValue(ts.SiswaId, out var student);
                    var transactions = transactionsByEntry.TryGetValue(ts.Id, out var entryTransactions)
                        ? entryTransactions
                        : new List<Transaksi>();
                    var lastPayment = transactions.FirstOrDefault(t => t.Type == JenisTransaksi.Masuk &&
                        !t.IsRefund && !t.IsPendingRefund);
                    var hasRefund = transactions.Any(t => t.IsRefund);
                    var totalPaid = transactions
                        .Where(t => t.Type == JenisTransaksi.Masuk)
                        .Sum(t => t.Amount);
                    var canEditPayment = canManage && !hasRefund && totalPaid <= billAmount;
                    return new TagihanSiswaDisplay
                    {
                        Entry = ts,
                        SiswaName = student?.Name ?? "?",
                        SiswaNis = student?.Nis,
                        LastPaymentDateText = lastPayment?.Date.ToString("dd MMM yyyy", IdId) ?? "Belum ada pembayaran",
                        LastPaymentMethodText = lastPayment?.Metode.ToDisplayLabel() ?? string.Empty,
                        CanMakePayment = ts.Status != StatusTagihan.Lunas && ts.Status != StatusTagihan.Proses,
                        HasPendingRefund = _currentUser is Bendahara && transactions.Any(t => t.IsPendingRefund),
                        Transactions = transactions
                        .Select(t => new TagihanTransactionDisplay
                        {
                            Transaction = t,
                            CanEdit = canEditPayment && t.Type == JenisTransaksi.Masuk && !t.IsRefund,
                            CanDelete = canManage && (t.Type != JenisTransaksi.Masuk ||
                                !hasRefund && totalPaid <= billAmount)
                        })
                        .ToList()
                    };
                });

            if (StatusFilterComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag)
            {
                query = filterTag switch
                {
                    "Lunas" => query.Where(x => x.Entry.Status == StatusTagihan.Lunas),
                    "BelumLunas" => query.Where(x => x.Entry.Status == StatusTagihan.BelumBayar),
                    "Sebagian" => query.Where(x => x.Entry.Status == StatusTagihan.Sebagian),
                    "Proses" => query.Where(x => x.Entry.Status == StatusTagihan.Proses),
                    _ => query
                };
            }

            var keyword = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(student =>
                    student.SiswaName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                    (student.SiswaNis?.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ?? false));
            }

            var details = query.ToList();
            DetailItems.ItemsSource = details;
            if (details.Any())
            {
                PaymentTableScrollViewer.Visibility = Visibility.Visible;
                DetailScrollViewer.Visibility = Visibility.Visible;
                DetailScrollViewer.Opacity = 0;
                DetailScrollViewer.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    }, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                PaymentTableScrollViewer.Visibility = Visibility.Collapsed;
                DetailScrollViewer.BeginAnimation(UIElement.OpacityProperty, null);
                DetailScrollViewer.Opacity = 1;
                DetailScrollViewer.Visibility = Visibility.Collapsed;
            }
            NoSelectedTagihanText.Visibility = Visibility.Collapsed;
            NoMatchingPaymentsText.Visibility = details.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AturJadwalButton_Click(object sender, RoutedEventArgs e)
        {
            var schedule = DataStore.Instance.JadwalTagihanMingguanList.FirstOrDefault(x => x.KelasId == _kelasId);
            var dialog = new JadwalTagihanMingguanDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh, schedule);
            _mainWindow.ShowModal(dialog);
        }

        private async void KirimPengingat_Click(object sender, RoutedEventArgs e)
        {
            await SendReminderForBillAsync(_selectedTagihan);
        }

        private async void WeekCardReminder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TagihanDisplayItem displayItem)
                await SendReminderForBillAsync(displayItem.Tagihan);
        }

        private async Task SendReminderForBillAsync(Tagihan tagihan)
        {
            if (tagihan == null || _currentUser is Siswa)
                return;

            var dueEntries = DataStore.Instance.TagihanSiswaList
                .Where(x => x.TagihanId == tagihan.Id && x.AmountDue > 0 &&
                            x.Status != StatusTagihan.Proses && x.Status != StatusTagihan.Lunas)
                .ToList();
            if (dueEntries.Count == 0)
            {
                AppMessageBox.Show(_mainWindow, "Tidak ada siswa dengan sisa tagihan pada periode ini.", "Semua lunas", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var total = dueEntries.Sum(x => x.AmountDue);
            var period = $"{GetPeriodTitle(tagihan)} ({FormatWeekRange(tagihan.PeriodStart.Value, tagihan.PeriodEnd.Value)})";
            if (AppMessageBox.Show(_mainWindow,
                    $"Kirim pengingat kepada {dueEntries.Count} siswa untuk {period}?\n\nTotal sisa yang perlu dibayar: Rp {total.ToString("N0", IdId)}.",
                    "Kirim pengingat", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                var result = await Notifikasi.KirimPengingatTagihanAsync(tagihan.Id);
                var summary = result.JumlahTerkirim > 0
                    ? $"Pengingat dibuat untuk {result.JumlahTerkirim} siswa. {result.SudahDiingatkanHariIni} siswa lain sudah diingatkan hari ini. Total sisa tagihan Rp {result.TotalSisa.ToString("N0", IdId)}."
                    : $"Pengingat untuk siswa yang masih memiliki tagihan sudah dikirim hari ini ({result.SudahDiingatkanHariIni} siswa).";
                AppMessageBox.Show(_mainWindow, summary, "Pengingat tagihan", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(_mainWindow, exception.Message, "Pengingat tidak dapat dikirim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetPeriodTitle(Tagihan tagihan) =>
            $"Minggu ke-{JadwalTagihanMingguanService.GetWeekNumber(tagihan)}";

        private static string FormatWeekRange(DateTime start, DateTime end)
        {
            if (start.Year == end.Year && start.Month == end.Month)
                return $"{start.Day}–{end.Day} {end.ToString("MMM yyyy", IdId)}";
            if (start.Year == end.Year)
                return $"{start.ToString("dd MMM", IdId)}–{end.ToString("dd MMM yyyy", IdId)}";
            return $"{start.ToString("dd MMM yyyy", IdId)}–{end.ToString("dd MMM yyyy", IdId)}";
        }

        private void TandaiLunas_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
            {
                AppMessageBox.Show(_mainWindow, "Hanya Bendahara yang dapat mencatat pembayaran.", "Akses ditolak");
                return;
            }

            if (sender is Button btn && btn.Tag is TagihanSiswaDisplay display &&
                display.Entry.Status != StatusTagihan.Lunas && display.Entry.Status != StatusTagihan.Proses)
            {
                var siswa = DataStore.Instance.Users.OfType<Siswa>()
                    .FirstOrDefault(item => item.Id == display.Entry.SiswaId);
                if (siswa == null)
                    return;

                _mainWindow.ShowModal(new BayarManualDialog(bendahara, siswa, display.Entry, _mainWindow, Refresh));
            }
        }

        private async void TandaiRefundLunas_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara ||
                sender is not Button button || button.Tag is not TagihanSiswaDisplay display)
                return;

            var pendingRefunds = DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == display.Entry.Id && t.IsPendingRefund)
                .ToList();

            if (pendingRefunds.Count == 0)
            {
                Refresh();
                return;
            }

            var totalRefund = pendingRefunds.Sum(t => t.Amount);
            if (AppMessageBox.Show(_mainWindow,
                    $"Selesaikan {pendingRefunds.Count} refund untuk {display.SiswaName} dengan total Rp {totalRefund:N0}?\n\nSaldo kas akan berkurang sebesar jumlah tersebut dan status refund berubah menjadi Lunas.",
                    "Konfirmasi penyelesaian refund", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await bendahara.TandaiRefundDibayarAsync(pendingRefunds.Select(refund => refund.Id));
                Refresh();
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(_mainWindow, exception.Message, "Refund tidak dapat diselesaikan", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanEditTransaction(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara || transaksi.Type != JenisTransaksi.Masuk || transaksi.IsRefund)
                return false;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == transaksi.TagihanSiswaId))
                return false;

            var tagihan = DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == _selectedTagihan?.Id);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == transaksi.TagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private bool CanDeleteTransaction(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara) return false;
            if (transaksi.Type != JenisTransaksi.Masuk) return true;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == transaksi.TagihanSiswaId))
                return false;

            var tagihan = DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == _selectedTagihan?.Id);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == transaksi.TagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara bendahara && sender is FrameworkElement element && element.Tag is Transaksi transaksi)
                _mainWindow.ShowModal(new EditPembayaranDialog(bendahara, transaksi, _mainWindow, Refresh));
        }

        private async void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara || sender is not FrameworkElement element || element.Tag is not Transaksi transaksi)
                return;

            if (AppMessageBox.Show(_mainWindow, $"Hapus transaksi ini sebesar Rp {transaksi.Amount:N0}? Total kas akan disesuaikan.",
                    "Hapus transaksi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await bendahara.HapusTransaksiAsync(transaksi.Id);
                Refresh();
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(_mainWindow, exception.Message, "Transaksi tidak dapat dihapus", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowPaymentActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not TagihanTransactionDisplay payment)
                return;

            if (CloseOpenMenuIfSameButton(button))
            {
                return;
            }

            var menu = CreatePopupActionMenu();
            if (payment.CanEdit)
                menu.Items.Add(CreatePaymentMenuItem("Ubah nominal", payment.Transaction, EditPayment_Click));
            if (payment.CanDelete)
                menu.Items.Add(CreatePaymentMenuItem("Hapus transaksi", payment.Transaction, DeletePayment_Click));
            OpenActionMenu(button, menu);
        }

        private static MenuItem CreatePaymentMenuItem(string header, Transaksi transaction, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = transaction };
            item.Click += onClick;
            return item;
        }

        private ContextMenu CreatePopupActionMenu() => new()
        {
            Style = (Style)FindResource("PopupActionMenu"),
            Placement = PlacementMode.Bottom
        };

        private bool CloseOpenMenuIfSameButton(Button button)
        {
            bool togglingCurrent = ReferenceEquals(_pendingActionMenuToggle, button) ||
                ReferenceEquals(_openActionMenu?.PlacementTarget, button);
            _pendingActionMenuToggle = null;
            if (!togglingCurrent)
                return false;

            if (_openActionMenu != null)
                _openActionMenu.IsOpen = false;
            _openActionMenu = null;
            return true;
        }

        private void ActionMenuButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _pendingActionMenuToggle = sender is Button button &&
                ReferenceEquals(_openActionMenu?.PlacementTarget, button) ? button : null;
        }

        private void OpenActionMenu(Button button, ContextMenu menu)
        {
            if (_openActionMenu?.IsOpen == true)
                _openActionMenu.IsOpen = false;

            menu.PlacementTarget = button;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_pendingActionMenuToggle, button)) return;
                if (ReferenceEquals(_openActionMenu, menu))
                    _openActionMenu = null;
            };
            _openActionMenu = menu;
            menu.IsOpen = menu.Items.Count > 0;
        }

    }
}
