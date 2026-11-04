using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class SiswaDisplayItem
    {
        public string Name { get; set; }
        public string Nis { get; set; }
        public int UnpaidCount { get; set; }
        public int TotalTagihanCount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string OutstandingText => TotalTagihanCount == 0 ? "Perlu tagihan" : $"Rp {OutstandingAmount.ToString("N0", IdId)}";
        public string FollowUpLabel => TotalTagihanCount == 0
            ? "Belum terdaftar"
            : $"{UnpaidCount} tagihan terbuka";

        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");
    }

    public class TransactionDisplayItem
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public Transaksi Transaction { get; set; }
        public string DateText => Transaction.Date.ToString("dd MMM yyyy", IdId);
        public string Description => string.IsNullOrWhiteSpace(Transaction.Description)
            ? "Transaksi"
            : Transaction.Description;
        public string TypeLabel => Transaction.Type == JenisTransaksi.Masuk ? "Pemasukan" : "Pengeluaran";
        public string AmountText => $"{(Transaction.Type == JenisTransaksi.Masuk ? "+" : "−")} Rp {Transaction.Amount.ToString("N0", IdId)}";
        public Brush AmountBrush => FindBrush(Transaction.Type == JenisTransaksi.Masuk ? "IncomeColor" : "ExpenseColor");
        public Brush TypeBrush => AmountBrush;

        private static Brush FindBrush(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    internal sealed class CashFlowPeriod
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public string AxisLabel { get; init; }
        public decimal Income { get; init; }
        public decimal Expense { get; init; }
    }

    internal sealed class CashFlowChartPoint
    {
        public Point Position { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal Amount { get; init; }
        public string SeriesName { get; init; }
    }

    internal sealed class CashFlowLineValue
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal Amount { get; init; }
        public string SeriesName { get; init; }
    }

    public partial class DashboardPage : Page
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private List<CashFlowPeriod> _cashFlow = new();
        private readonly List<CashFlowChartPoint> _incomeChartPoints = new();
        private readonly List<CashFlowChartPoint> _expenseChartPoints = new();
        private bool _responsiveLayoutInitialized;
        private bool _isCompactLayout;
        private bool _isReady;
        private bool _dataStoreSubscribed;
        private bool _useWeeklyCashFlow;

        public DashboardPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _isReady = true;
            UpdateCashFlowPeriodText();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = ResolveKelasId(currentUser);

            TitleText.Text = "Ringkasan";
            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
            bool canManage = currentUser is Bendahara;
            TarikKasButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;
            TambahSiswaButton.Visibility = (canManage || currentUser is WaliKelas)
                ? Visibility.Visible
                : Visibility.Collapsed;

            Refresh();
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            var store = DataStore.Instance;
            if (!_dataStoreSubscribed)
            {
                store.DataChanged += DataStore_DataChanged;
                _dataStoreSubscribed = true;
            }

            RefreshLatestBillCard();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_dataStoreSubscribed)
                return;

            DataStore.Instance.DataChanged -= DataStore_DataChanged;
            _dataStoreSubscribed = false;
        }

        private void DataStore_DataChanged(object sender, EventArgs e)
        {
            if (!IsLoaded)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshLatestBillCard));
                return;
            }

            RefreshLatestBillCard();
        }

        private void RefreshLatestBillCard()
        {
            var store = DataStore.Instance;
            var students = store.Users.OfType<Siswa>()
                .Where(student => student.KelasId == _kelasId)
                .ToList();
            RefreshActiveBill(store, students);
        }

        private static int ResolveKelasId(User user) => user switch
        {
            Siswa siswa => siswa.KelasId,
            WaliKelas waliKelas => waliKelas.KelasId,
            _ => DataStore.Instance.KelasList.FirstOrDefault()?.Id ?? 0
        };

        private void Refresh()
        {
            var store = DataStore.Instance;
            var kelas = store.KelasList.FirstOrDefault(k => k.Id == _kelasId);
            if (kelas == null)
            {
                SubtitleText.Text = "Data kelas tidak tersedia.";
                SaldoText.Text = "Rp 0";
                TotalSiswaText.Text = "0";
                BelumLunasText.Text = "Rp 0";
                SiswaItems.ItemsSource = null;
                TransaksiItems.ItemsSource = null;
                CashFlowEmptyText.Visibility = Visibility.Visible;
                NoSiswaText.Visibility = Visibility.Visible;
                NoTransaksiText.Visibility = Visibility.Visible;
                return;
            }

            SubtitleText.Text = $"Kelas {kelas.Name} · Tahun ajaran {kelas.AcademicYear} · {DateTime.Now.ToString("d MMMM yyyy", IdId)}";
            SaldoText.Text = $"Rp {kelas.HitungSaldo().ToString("N0", IdId)}";

            var students = store.Users.OfType<Siswa>()
                .Where(s => s.KelasId == _kelasId)
                .OrderBy(s => s.Name)
                .ToList();
            var studentIds = students.Select(s => s.Id).ToHashSet();
            TotalSiswaText.Text = students.Count.ToString(IdId);

            var classBillIds = store.TagihanList
                .Where(tagihan => tagihan.KelasId == _kelasId)
                .Select(tagihan => tagihan.Id)
                .ToHashSet();
            var classEntries = store.TagihanSiswaList
                .Where(entry => classBillIds.Contains(entry.TagihanId) && studentIds.Contains(entry.SiswaId))
                .ToList();
            var openEntries = classEntries
                .Where(entry => Outstanding(entry) > 0)
                .ToList();
            var outstandingTotal = openEntries.Sum(Outstanding);
            BelumLunasText.Text = $"Rp {outstandingTotal.ToString("N0", IdId)}";
            UnpaidCaptionText.Text = $"{openEntries.Count} tagihan siswa belum lunas";

            var displayStudents = students.Select(student =>
            {
                var entries = classEntries.Where(entry => entry.SiswaId == student.Id).ToList();
                var dueEntries = entries.Where(entry => Outstanding(entry) > 0).ToList();
                return new SiswaDisplayItem
                {
                    Name = student.Name,
                    Nis = student.Nis,
                    UnpaidCount = dueEntries.Count,
                    TotalTagihanCount = entries.Count,
                    OutstandingAmount = dueEntries.Sum(Outstanding)
                };
            })
            .Where(item => item.TotalTagihanCount == 0 || item.UnpaidCount > 0)
            .OrderByDescending(item => item.OutstandingAmount)
            .ThenBy(item => item.Name)
            .ToList();

            SiswaItems.ItemsSource = displayStudents.Take(4).ToList();
            NoSiswaText.Text = students.Count == 0
                ? "Belum ada siswa terdaftar."
                : "Semua siswa sudah melunasi tagihan mereka.";
            NoSiswaText.Visibility = displayStudents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            RefreshActiveBill(store, students);
            RefreshTransactions();
            BuildCashFlow(store);
            RenderCashFlow();
        }

        private static decimal Outstanding(TagihanSiswa entry) =>
            Math.Max(0, entry.AmountDue - entry.JumlahDibayar);

        private void RefreshActiveBill(DataStore store, List<Siswa> students)
        {
            var classBills = store.TagihanList
                .Where(tagihan => tagihan.KelasId == _kelasId)
                .OrderByDescending(tagihan => tagihan.Id)
                .ToList();

            if (classBills.Count == 0)
            {
                ActiveBillNameText.Text = "Belum ada tagihan";
                ActiveBillAmountText.Visibility = Visibility.Collapsed;
                ActiveBillMetaText.Visibility = Visibility.Collapsed;
                ActiveBillOpenButton.Visibility = Visibility.Collapsed;
                return;
            }

            var studentIds = students.Select(student => student.Id).ToHashSet();
            var latestOpenBill = classBills
                .Select(bill => new
                {
                    Bill = bill,
                    Entries = store.TagihanSiswaList
                        .Where(entry => entry.TagihanId == bill.Id && studentIds.Contains(entry.SiswaId))
                        .ToList()
                })
                .FirstOrDefault(item => item.Entries.Any(entry => Outstanding(entry) > 0));

            if (latestOpenBill == null)
            {
                ActiveBillNameText.Text = "Semua tagihan lunas";
                ActiveBillAmountText.Visibility = Visibility.Collapsed;
                ActiveBillMetaText.Visibility = Visibility.Collapsed;
                ActiveBillOpenButton.Visibility = Visibility.Collapsed;
                return;
            }

            var remaining = latestOpenBill.Entries.Sum(Outstanding);
            var openCount = latestOpenBill.Entries.Count(entry => Outstanding(entry) > 0);
            ActiveBillNameText.Text = latestOpenBill.Bill.Name;
            ActiveBillAmountText.Text = $"Rp {remaining.ToString("N0", IdId)}";
            ActiveBillMetaText.Text = $"Sisa tagihan · {openCount} siswa belum lunas";
            ActiveBillAmountText.Visibility = Visibility.Visible;
            ActiveBillMetaText.Visibility = Visibility.Visible;
            ActiveBillOpenButton.Visibility = Visibility.Visible;
        }

        private void RefreshTransactions()
        {
            var store = DataStore.Instance;
            IEnumerable<Transaksi> query = store.TransaksiList
                .Where(transaction => transaction.KelasId == _kelasId)
                .OrderByDescending(transaction => transaction.Date);

            if (TransactionFilterCombo.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string selectedType && selectedType != "All" &&
                Enum.TryParse(selectedType, out JenisTransaksi transactionType))
            {
                query = query.Where(transaction => transaction.Type == transactionType);
            }

            var results = query.Take(8)
                .Select(transaction => new TransactionDisplayItem { Transaction = transaction })
                .ToList();
            TransaksiItems.ItemsSource = results;
            NoTransaksiText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TransactionCountText.Text = results.Count == 0
                ? "Tidak ada transaksi"
                : $"Menampilkan {results.Count} transaksi terbaru";
        }

        private void BuildCashFlow(DataStore store)
        {
            if (_useWeeklyCashFlow)
            {
                var today = DateTime.Today;
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                var currentWeek = today.AddDays(-daysSinceMonday);
                var firstWeek = currentWeek.AddDays(-35);
                _cashFlow = Enumerable.Range(0, 6)
                    .Select(offset => firstWeek.AddDays(offset * 7))
                    .Select(start => CreateCashFlowPeriod(
                        store, start, start.AddDays(7), start.ToString("d MMM", IdId)))
                    .ToList();
                return;
            }

            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var firstMonth = currentMonth.AddMonths(-5);
            _cashFlow = Enumerable.Range(0, 6)
                .Select(offset => firstMonth.AddMonths(offset))
                .Select(start => CreateCashFlowPeriod(
                    store, start, start.AddMonths(1), start.ToString("MMM", IdId)))
                .ToList();
        }

        private CashFlowPeriod CreateCashFlowPeriod(DataStore store, DateTime start, DateTime endExclusive, string axisLabel)
        {
            var transactions = store.TransaksiList.Where(transaction =>
                transaction.KelasId == _kelasId &&
                transaction.AffectsKas &&
                transaction.Date >= start &&
                transaction.Date < endExclusive);

            return new CashFlowPeriod
            {
                StartDate = start,
                EndDate = endExclusive.AddDays(-1),
                AxisLabel = axisLabel,
                Income = transactions
                    .Where(transaction => transaction.Type == JenisTransaksi.Masuk)
                    .Sum(transaction => transaction.Amount),
                Expense = transactions
                    .Where(transaction => transaction.Type == JenisTransaksi.Keluar)
                    .Sum(transaction => transaction.Amount)
            };
        }

        private void RenderCashFlow()
        {
            if (CashFlowCanvas == null)
                return;

            if (CashFlowCanvas.ActualWidth < 120 || CashFlowCanvas.ActualHeight < 100)
            {
                CashFlowPointTooltip.Visibility = Visibility.Collapsed;
                return;
            }

            CashFlowCanvas.Children.Clear();
            CashFlowCanvas.Children.Add(CashFlowPointTooltip);
            CashFlowPointTooltip.Visibility = Visibility.Collapsed;
            _incomeChartPoints.Clear();
            _expenseChartPoints.Clear();
            var hasTransactions = _cashFlow.Any(period => period.Income > 0 || period.Expense > 0);
            CashFlowEmptyText.Visibility = hasTransactions ? Visibility.Collapsed : Visibility.Visible;
            if (!hasTransactions)
                return;

            double width = CashFlowCanvas.ActualWidth;
            double height = CashFlowCanvas.ActualHeight;
            const double left = 64;
            const double right = 10;
            const double top = 10;
            const double bottom = 29;
            double plotWidth = Math.Max(1, width - left - right);
            double plotHeight = Math.Max(1, height - top - bottom);
            decimal maxValue = _cashFlow.Max(period => Math.Max(period.Income, period.Expense));
            decimal chartMax = NiceChartMaximum(maxValue);

            var gridBrush = FindBrush("BorderColor", Brushes.Gray);
            var textBrush = FindBrush("TextGray", Brushes.Gray);
            var incomeBrush = FindBrush("CashFlowPrimaryColor", Brushes.IndianRed);
            var expenseBrush = FindBrush("CashFlowSecondaryColor", Brushes.DarkSeaGreen);
            var incomeFill = FindBrush("CashFlowIncomeFill", Brushes.Transparent);
            var incomePoints = new PointCollection();
            var expensePoints = new PointCollection();
            var baseline = top + plotHeight;

            for (int tick = 0; tick <= 4; tick++)
            {
                decimal value = chartMax * tick / 4m;
                double y = top + plotHeight - (double)(value / chartMax) * plotHeight;
                var gridLine = new Line
                {
                    X1 = left,
                    X2 = width - right,
                    Y1 = y,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 1,
                    Opacity = 0.55
                };
                CashFlowCanvas.Children.Add(gridLine);
                AddChartText(CompactRupiah(value), 0, y - 7, 54, 14, textBrush, 9, TextAlignment.Right);
            }

            for (int index = 0; index < _cashFlow.Count; index++)
            {
                var period = _cashFlow[index];
                double x = left + (plotWidth * index / Math.Max(1, _cashFlow.Count - 1));
                double incomeY = top + plotHeight - (double)(period.Income / chartMax) * plotHeight;
                double expenseY = top + plotHeight - (double)(period.Expense / chartMax) * plotHeight;
                incomePoints.Add(new Point(x, incomeY));
                expensePoints.Add(new Point(x, expenseY));
                _incomeChartPoints.Add(new CashFlowChartPoint
                {
                    Position = incomePoints[^1],
                    StartDate = period.StartDate,
                    EndDate = period.EndDate,
                    Amount = period.Income,
                    SeriesName = "Pemasukan"
                });
                _expenseChartPoints.Add(new CashFlowChartPoint
                {
                    Position = expensePoints[^1],
                    StartDate = period.StartDate,
                    EndDate = period.EndDate,
                    Amount = period.Expense,
                    SeriesName = "Pengeluaran"
                });
                AddChartText(period.AxisLabel, x - 24, height - bottom + 8, 48, 16, textBrush, 10, TextAlignment.Center);
            }

            var areaPoints = new PointCollection { new Point(incomePoints[0].X, baseline) };
            foreach (var point in incomePoints)
                areaPoints.Add(point);
            areaPoints.Add(new Point(incomePoints[^1].X, baseline));
            CashFlowCanvas.Children.Add(new Polygon
            {
                Points = areaPoints,
                Fill = incomeFill,
                Stroke = Brushes.Transparent
            });

            CashFlowCanvas.Children.Add(new Polyline
            {
                Points = incomePoints,
                Stroke = incomeBrush,
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round
            });
            CashFlowCanvas.Children.Add(new Polyline
            {
                Points = expensePoints,
                Stroke = expenseBrush,
                StrokeThickness = 1.8,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                StrokeLineJoin = PenLineJoin.Round
            });

            for (int index = 0; index < _cashFlow.Count; index++)
            {
                AddChartPoint(incomePoints[index], incomeBrush, 7);
                AddChartPoint(expensePoints[index], expenseBrush, 6);
            }
        }

        private void AddChartPoint(Point point, Brush fill, double diameter)
        {
            var marker = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = fill,
                Stroke = FindBrush("CardBackground", Brushes.Black),
                StrokeThickness = 1
            };
            Canvas.SetLeft(marker, point.X - diameter / 2);
            Canvas.SetTop(marker, point.Y - diameter / 2);
            CashFlowCanvas.Children.Add(marker);
        }

        private void CashFlowCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pointer = e.GetPosition(CashFlowCanvas);
            var value = FindHoveredCashFlowValue(pointer, 14);
            if (value == null)
            {
                CashFlowPointTooltip.Visibility = Visibility.Collapsed;
                return;
            }

            CashFlowPointLabel.Text = $"{value.SeriesName} · {FormatCashFlowRange(value.StartDate, value.EndDate)}";
            CashFlowPointAmount.Text = $"Rp {value.Amount.ToString("N0", IdId)}";
            CashFlowPointTooltip.Visibility = Visibility.Visible;
            CashFlowPointTooltip.Measure(new Size(240, 90));

            double tooltipWidth = CashFlowPointTooltip.DesiredSize.Width;
            double tooltipHeight = CashFlowPointTooltip.DesiredSize.Height;
            double left = pointer.X + 14;
            if (left + tooltipWidth > CashFlowCanvas.ActualWidth - 4)
                left = pointer.X - tooltipWidth - 14;
            left = Math.Clamp(left, 4, Math.Max(4, CashFlowCanvas.ActualWidth - tooltipWidth - 4));

            double top = pointer.Y - tooltipHeight - 10;
            if (top < 4)
                top = pointer.Y + 12;
            top = Math.Clamp(top, 4, Math.Max(4, CashFlowCanvas.ActualHeight - tooltipHeight - 4));

            Canvas.SetLeft(CashFlowPointTooltip, left);
            Canvas.SetTop(CashFlowPointTooltip, top);
        }

        private CashFlowLineValue FindHoveredCashFlowValue(Point pointer, double hitDistance)
        {
            CashFlowLineValue nearestValue = null;
            double nearestDistanceSquared = hitDistance * hitDistance;
            FindNearestValueOnSeries(_incomeChartPoints, pointer, ref nearestValue, ref nearestDistanceSquared);
            FindNearestValueOnSeries(_expenseChartPoints, pointer, ref nearestValue, ref nearestDistanceSquared);
            return nearestValue;
        }

        private static void FindNearestValueOnSeries(IReadOnlyList<CashFlowChartPoint> points, Point pointer,
            ref CashFlowLineValue nearestValue, ref double nearestDistanceSquared)
        {
            for (int index = 1; index < points.Count; index++)
            {
                var start = points[index - 1];
                var end = points[index];
                double deltaX = end.Position.X - start.Position.X;
                double deltaY = end.Position.Y - start.Position.Y;
                double lengthSquared = deltaX * deltaX + deltaY * deltaY;
                double t = lengthSquared == 0
                    ? 0
                    : Math.Clamp(((pointer.X - start.Position.X) * deltaX + (pointer.Y - start.Position.Y) * deltaY) / lengthSquared, 0, 1);
                double nearestX = start.Position.X + t * deltaX;
                double nearestY = start.Position.Y + t * deltaY;
                double distanceX = pointer.X - nearestX;
                double distanceY = pointer.Y - nearestY;
                double distanceSquared = distanceX * distanceX + distanceY * distanceY;

                if (distanceSquared >= nearestDistanceSquared)
                    continue;

                nearestDistanceSquared = distanceSquared;
                nearestValue = new CashFlowLineValue
                {
                    StartDate = start.StartDate,
                    EndDate = end.EndDate,
                    Amount = start.Amount + (end.Amount - start.Amount) * (decimal)t,
                    SeriesName = start.SeriesName
                };
            }
        }

        private void CashFlowCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            CashFlowPointTooltip.Visibility = Visibility.Collapsed;
        }

        private void AddChartText(string value, double x, double y, double width, double height, Brush foreground,
            double fontSize, TextAlignment alignment)
        {
            var text = new TextBlock
            {
                Text = value,
                Width = width,
                Height = height,
                FontSize = fontSize,
                Foreground = foreground,
                TextAlignment = alignment,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, y);
            CashFlowCanvas.Children.Add(text);
        }

        private static decimal NiceChartMaximum(decimal value)
        {
            if (value <= 0)
                return 1;
            decimal rawStep = value / 4m;
            decimal magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)rawStep)));
            decimal normalized = rawStep / magnitude;
            decimal step = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
            return decimal.Ceiling(value / (step * magnitude)) * step * magnitude;
        }

        private static string CompactRupiah(decimal value)
        {
            if (value >= 1_000_000)
                return $"Rp {((double)value / 1_000_000).ToString("0.#", IdId)} jt";
            if (value >= 1_000)
                return $"Rp {(value / 1_000).ToString("0", IdId)} rb";
            return $"Rp {value.ToString("N0", IdId)}";
        }

        private string FormatCashFlowRange(DateTime start, DateTime end)
        {
            return _useWeeklyCashFlow
                ? $"{start.ToString("d MMM", IdId)}–{end.ToString("d MMM yyyy", IdId)}"
                : $"{start.ToString("MMM", IdId)}–{end.ToString("MMM yyyy", IdId)}";
        }

        private void UpdateCashFlowPeriodText()
        {
            string period = _useWeeklyCashFlow ? "6 minggu terakhir" : "6 bulan terakhir";
            CashFlowSubtitleText.Text = $"Pemasukan dan pengeluaran, {period}";
            CashFlowEmptyText.Text = $"Belum ada transaksi kas dalam {period}.";
        }

        private void CashFlowPeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady || CashFlowPeriodCombo.SelectedItem is not ComboBoxItem selected)
                return;

            _useWeeklyCashFlow = string.Equals(selected.Tag?.ToString(), "Weeks", StringComparison.Ordinal);
            UpdateCashFlowPeriodText();
            BuildCashFlow(DataStore.Instance);
            RenderCashFlow();
        }

        private static Brush FindBrush(string resourceKey, Brush fallback) =>
            Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;

        private void CashFlowCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderCashFlow();

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0)
                return;

            bool compact = e.NewSize.Width < 940;
            if (_responsiveLayoutInitialized && compact == _isCompactLayout)
                return;

            _responsiveLayoutInitialized = true;
            _isCompactLayout = compact;
            SummaryCardsGrid.ColumnDefinitions.Clear();
            SummaryCardsGrid.RowDefinitions.Clear();
            AnalyticsGrid.ColumnDefinitions.Clear();
            AnalyticsGrid.RowDefinitions.Clear();

            if (compact)
            {
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int row = 0; row < 3; row++)
                    SummaryCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(BalanceCard, 0);
                Grid.SetColumn(StudentCountCard, 0);
                Grid.SetColumn(UnpaidCountCard, 0);
                Grid.SetRow(BalanceCard, 0);
                Grid.SetRow(StudentCountCard, 1);
                Grid.SetRow(UnpaidCountCard, 2);
                BalanceCard.Margin = new Thickness(0, 0, 0, 10);
                StudentCountCard.Margin = new Thickness(0, 0, 0, 10);
                UnpaidCountCard.Margin = new Thickness(0);

                AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AnalyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AnalyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(CashFlowCard, 0);
                Grid.SetRow(CashFlowCard, 0);
                Grid.SetColumn(RightRail, 0);
                Grid.SetRow(RightRail, 1);
                CashFlowCard.Margin = new Thickness(0, 0, 0, 14);
            }
            else
            {
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(BalanceCard, 0);
                Grid.SetColumn(StudentCountCard, 2);
                Grid.SetColumn(UnpaidCountCard, 4);
                Grid.SetRow(BalanceCard, 0);
                Grid.SetRow(StudentCountCard, 0);
                Grid.SetRow(UnpaidCountCard, 0);
                BalanceCard.Margin = new Thickness(0);
                StudentCountCard.Margin = new Thickness(0);
                UnpaidCountCard.Margin = new Thickness(0);

                AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.1, GridUnitType.Star) });
                AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                AnalyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AnalyticsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(CashFlowCard, 0);
                Grid.SetColumn(RightRail, 2);
                Grid.SetRow(CashFlowCard, 0);
                Grid.SetRow(RightRail, 0);
                CashFlowCard.Margin = new Thickness(0);
            }
        }

        private void TarikKasButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TarikKasDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void TambahSiswaButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahSiswaDialog(_kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void ActiveBillOpenButton_Click(object sender, RoutedEventArgs e) =>
            _mainWindow.NavigateTo(new TagihanPage(_currentUser, _mainWindow));

        private void LihatSiswaButton_Click(object sender, RoutedEventArgs e) =>
            _mainWindow.NavigateTo(new SiswaPage(_currentUser, _mainWindow));

        private void LihatSemuaButton_Click(object sender, RoutedEventArgs e) =>
            _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow));

        private void TransactionFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isReady)
                RefreshTransactions();
        }

        private void SiswaRow_Click(object sender, MouseButtonEventArgs e) => OpenStudentHistory(sender);

        private void SiswaRow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                OpenStudentHistory(sender);
                e.Handled = true;
            }
        }

        private void OpenStudentHistory(object sender)
        {
            if (sender is FrameworkElement element && element.Tag is SiswaDisplayItem item)
                _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow, item.Nis));
        }
    }
}
