using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public string TimeText => Transaction.Date.ToString("HH:mm", IdId);
        public string Description => string.IsNullOrWhiteSpace(Transaction.Description)
            ? "Transaksi"
            : Transaction.Description;
        public string PaymentMethodText => Transaction.Metode.ToDisplayLabel();
        public string TypeLabel => Transaction.Type == JenisTransaksi.Masuk ? "Pemasukan" : "Pengeluaran";
        public string AmountText => $"{(Transaction.Type == JenisTransaksi.Masuk ? "+" : "−")} Rp {Transaction.Amount.ToString("N0", IdId)}";
        public Brush AmountBrush => FindBrush(Transaction.Type == JenisTransaksi.Masuk ? "IncomeColor" : "ExpenseColor");
        public Brush TypeBrush => AmountBrush;
        public Brush TypeBackground
        {
            get
            {
                var color = (AmountBrush as SolidColorBrush)?.Color ?? Colors.Gray;
                color.A = 34;
                return new SolidColorBrush(color);
            }
        }

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
    }

    internal sealed class CashFlowLineValue
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int SegmentIndex { get; init; }
        public double Interpolation { get; init; }
        public double X { get; init; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
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
        private bool _useWeeklyCashFlow = true;
        private bool _useYearlyCashFlow;
        private string _transactionFilterType = "All";
        private const double CashFlowPlotTop = 10;
        private const double CashFlowPlotBottom = 29;

        public DashboardPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _isReady = true;
            UpdateCashFlowPeriodText();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = ResolveKelasId(currentUser);

            TitleText.Text = "Ringkasan Keuangan Kas";
            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
            bool canManage = currentUser is Bendahara;
            TarikKasButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;
            TambahSiswaButton.Visibility = (canManage || currentUser is WaliKelas)
                ? Visibility.Visible
                : Visibility.Collapsed;
            BuatTagihanButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;

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

            RefreshWeeklyPaymentStatus();
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
                Dispatcher.BeginInvoke(new Action(Refresh));
                return;
            }

            Refresh();
        }

        private void RefreshWeeklyPaymentStatus()
        {
            var store = DataStore.Instance;
            var students = store.Users.OfType<Siswa>()
                .Where(student => student.KelasId == _kelasId)
                .ToList();
            RefreshWeeklyPaymentSummary(store, students);
        }

        private void RefreshWeeklyPaymentSummary(DataStore store, IReadOnlyCollection<Siswa> students)
        {
            var weekStart = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            var currentWeekBill = store.TagihanList
                .Where(tagihan => tagihan.KelasId == _kelasId && tagihan.IsWeeklyOccurrence &&
                                  tagihan.PeriodStart?.Date == weekStart)
                .OrderByDescending(tagihan => tagihan.Id)
                .FirstOrDefault();

            if (currentWeekBill == null)
            {
                WeeklyPaymentSummaryText.Text = "Belum ada tagihan minggu ini";
                WeeklyPaymentProgressTrack.Background = FindBrush("SubtleBackground", Brushes.DimGray);
                SetWeeklyPaidProgress(0);
                return;
            }

            var studentIds = students.Select(student => student.Id).ToHashSet();
            var paidStudentIds = store.TagihanSiswaList
                .Where(entry => entry.TagihanId == currentWeekBill.Id && studentIds.Contains(entry.SiswaId) &&
                                entry.Status == StatusTagihan.Lunas)
                .Select(entry => entry.SiswaId)
                .ToHashSet();
            var paidCount = students.Count(student => paidStudentIds.Contains(student.Id));
            var unpaidCount = Math.Max(0, students.Count - paidCount);

            WeeklyPaymentSummaryText.Text = $"{paidCount} lunas · {unpaidCount} belum bayar minggu ini";
            WeeklyPaymentProgressTrack.Background = FindBrush("WarningBadgeBackground", Brushes.DarkRed);
            SetWeeklyPaidProgress(students.Count == 0 ? 0 : paidCount / (double)students.Count);
        }

        private void SetWeeklyPaidProgress(double ratio)
        {
            if (WeeklyPaidProgressFill.RenderTransform is ScaleTransform scale)
                scale.ScaleX = Math.Clamp(ratio, 0, 1);
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
                ExpenseMonthText.Text = "Rp 0";
                ExpenseTransactionCountText.Text = "0 transaksi bulan ini";
                TotalStudentCountBadgeText.Text = "0 Siswa";
                AttentionStudentCountText.Text = "0 Belum Lunas";
                RefreshWeeklyPaymentSummary(store, Array.Empty<Siswa>());
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
            RefreshWeeklyPaymentSummary(store, students);

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var monthExpenses = store.TransaksiList
                .Where(transaction => transaction.KelasId == _kelasId &&
                                      transaction.Type == JenisTransaksi.Keluar &&
                                      transaction.AffectsKas &&
                                      transaction.Date >= monthStart && transaction.Date < nextMonth)
                .ToList();
            ExpenseMonthText.Text = $"Rp {monthExpenses.Sum(transaction => transaction.Amount).ToString("N0", IdId)}";
            ExpenseTransactionCountText.Text = $"{monthExpenses.Count} transaksi bulan ini";

            var classBillIds = store.TagihanList
                .Where(tagihan => tagihan.KelasId == _kelasId && tagihan.IsWeeklyOccurrence)
                .Select(tagihan => tagihan.Id)
                .ToHashSet();
            var classEntries = store.TagihanSiswaList
                .Where(entry => classBillIds.Contains(entry.TagihanId) && studentIds.Contains(entry.SiswaId))
                .ToList();
            var openEntries = classEntries
                .Where(entry => entry.Status != StatusTagihan.Proses && Outstanding(entry) > 0)
                .ToList();
            var owingStudentIds = openEntries.Select(entry => entry.SiswaId).Distinct().ToHashSet();
            var outstandingTotal = openEntries.Sum(Outstanding);
            TotalStudentCountBadgeText.Text = $"{students.Count} Siswa";
            AttentionStudentCountText.Text = $"{owingStudentIds.Count} Belum Lunas";
            BelumLunasText.Text = $"Rp {outstandingTotal.ToString("N0", IdId)}";
            UnpaidCaptionText.Text = owingStudentIds.Count == 0
                ? "Tidak ada siswa menunggak"
                : $"{owingStudentIds.Count} siswa menunggak";
            var displayStudents = students.Select(student =>
            {
                var entries = classEntries.Where(entry => entry.SiswaId == student.Id).ToList();
                var dueEntries = entries
                    .Where(entry => entry.Status != StatusTagihan.Proses && Outstanding(entry) > 0)
                    .ToList();
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
                : classEntries.Count == 0
                    ? "Belum ada tagihan mingguan untuk ditindaklanjuti."
                    : "Semua siswa sudah melunasi tagihan mereka.";
            NoSiswaText.Visibility = displayStudents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            RefreshTransactions();
            BuildCashFlow(store);
            RenderCashFlow();
        }

        private static decimal Outstanding(TagihanSiswa entry) =>
            Math.Max(0, entry.AmountDue);

        private void RefreshTransactions()
        {
            var store = DataStore.Instance;
            IEnumerable<Transaksi> query = store.TransaksiList
                .Where(transaction => transaction.KelasId == _kelasId)
                .OrderByDescending(transaction => transaction.Date);

            if (_transactionFilterType != "All" &&
                Enum.TryParse(_transactionFilterType, out JenisTransaksi transactionType))
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
            int monthCount = _useYearlyCashFlow ? 12 : 6;
            var firstMonth = currentMonth.AddMonths(-(monthCount - 1));
            _cashFlow = Enumerable.Range(0, monthCount)
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
                CashFlowHoverLine.Visibility = Visibility.Collapsed;
                return;
            }

            CashFlowCanvas.Children.Clear();
            CashFlowPointTooltip.Visibility = Visibility.Collapsed;
            CashFlowHoverLine.Visibility = Visibility.Collapsed;
            _incomeChartPoints.Clear();
            _expenseChartPoints.Clear();
            var hasTransactions = _cashFlow.Any(period => period.Income > 0 || period.Expense > 0);
            CashFlowEmptyText.Visibility = hasTransactions ? Visibility.Collapsed : Visibility.Visible;
            if (!hasTransactions)
            {
                CashFlowCanvas.Children.Add(CashFlowHoverLine);
                CashFlowCanvas.Children.Add(CashFlowPointTooltip);
                return;
            }

            double width = CashFlowCanvas.ActualWidth;
            double height = CashFlowCanvas.ActualHeight;
            const double left = 64;
            const double right = 10;
            const double top = CashFlowPlotTop;
            const double bottom = CashFlowPlotBottom;
            double plotWidth = Math.Max(1, width - left - right);
            double plotHeight = Math.Max(1, height - top - bottom);
            decimal maxValue = _cashFlow.Max(period => Math.Max(period.Income, period.Expense));
            decimal chartMax = NiceChartMaximum(maxValue);

            var gridBrush = FindBrush("BorderColor", Brushes.Gray);
            var textBrush = FindBrush("TextGray", Brushes.Gray);
            var incomeBrush = FindBrush("CashFlowPrimaryColor", Brushes.SeaGreen);
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
                    Amount = period.Income
                });
                _expenseChartPoints.Add(new CashFlowChartPoint
                {
                    Position = expensePoints[^1],
                    StartDate = period.StartDate,
                    EndDate = period.EndDate,
                    Amount = period.Expense
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

            CashFlowCanvas.Children.Add(CashFlowHoverLine);
            CashFlowCanvas.Children.Add(CashFlowPointTooltip);
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
            var value = FindCashFlowValueAtPointer(pointer);
            if (value == null)
            {
                CashFlowPointTooltip.Visibility = Visibility.Collapsed;
                CashFlowHoverLine.Visibility = Visibility.Collapsed;
                return;
            }

            CashFlowPointLabel.Text = FormatCashFlowHoverPeriod(value.StartDate, value.EndDate);
            CashFlowPointIncome.Text = $"Pemasukan: Rp {value.Income.ToString("N0", IdId)}";
            CashFlowPointExpense.Text = $"Pengeluaran: Rp {value.Expense.ToString("N0", IdId)}";
            CashFlowHoverLine.X1 = value.X;
            CashFlowHoverLine.X2 = value.X;
            CashFlowHoverLine.Y1 = CashFlowPlotTop;
            CashFlowHoverLine.Y2 = CashFlowCanvas.ActualHeight - CashFlowPlotBottom;
            CashFlowHoverLine.Visibility = Visibility.Visible;
            CashFlowPointTooltip.Visibility = Visibility.Visible;
            CashFlowPointTooltip.Measure(new Size(260, 110));

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

        private CashFlowLineValue FindCashFlowValueAtPointer(Point pointer)
        {
            if (_incomeChartPoints.Count < 2 || _expenseChartPoints.Count < 2 ||
                pointer.Y < CashFlowPlotTop || pointer.Y > CashFlowCanvas.ActualHeight - CashFlowPlotBottom)
                return null;

            double firstX = _incomeChartPoints[0].Position.X;
            double lastX = _incomeChartPoints[^1].Position.X;
            if (pointer.X < firstX || pointer.X > lastX)
                return null;

            int segmentIndex = _incomeChartPoints.Count - 2;
            for (int index = 0; index < _incomeChartPoints.Count - 1; index++)
            {
                if (pointer.X <= _incomeChartPoints[index + 1].Position.X)
                {
                    segmentIndex = index;
                    break;
                }
            }

            var start = _incomeChartPoints[segmentIndex];
            var end = _incomeChartPoints[segmentIndex + 1];
            double segmentWidth = end.Position.X - start.Position.X;
            double interpolation = segmentWidth <= 0
                ? 0
                : Math.Clamp((pointer.X - start.Position.X) / segmentWidth, 0, 1);
            var hoveredPeriod = interpolation < 0.5 ? start : end;

            return new CashFlowLineValue
            {
                StartDate = hoveredPeriod.StartDate,
                EndDate = hoveredPeriod.EndDate,
                SegmentIndex = segmentIndex,
                Interpolation = interpolation,
                X = pointer.X,
                Income = InterpolateCashFlowAmount(_incomeChartPoints, segmentIndex, interpolation),
                Expense = InterpolateCashFlowAmount(_expenseChartPoints, segmentIndex, interpolation)
            };
        }

        private static decimal InterpolateCashFlowAmount(IReadOnlyList<CashFlowChartPoint> points, int segmentIndex, double t)
        {
            if (points.Count == 0)
                return 0;
            if (points.Count == 1)
                return points[0].Amount;

            int startIndex = Math.Clamp(segmentIndex, 0, points.Count - 2);
            var start = points[startIndex];
            var end = points[startIndex + 1];
            return start.Amount + (end.Amount - start.Amount) * (decimal)t;
        }

        private void CashFlowCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            CashFlowPointTooltip.Visibility = Visibility.Collapsed;
            CashFlowHoverLine.Visibility = Visibility.Collapsed;
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

        private string FormatCashFlowHoverPeriod(DateTime start, DateTime end)
        {
            return _useWeeklyCashFlow
                ? $"{start.ToString("d MMM", IdId)}–{end.ToString("d MMM", IdId)}"
                : start.ToString("MMMM yyyy", IdId);
        }

        private void UpdateCashFlowPeriodText()
        {
            string period = _useWeeklyCashFlow
                ? "6 minggu terakhir"
                : _useYearlyCashFlow ? "12 bulan terakhir" : "6 bulan terakhir";
            CashFlowSubtitleText.Text = $"Pemasukan dan pengeluaran, {period}";
            CashFlowEmptyText.Text = $"Belum ada transaksi kas dalam {period}.";
        }

        private void CashFlowPeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady || CashFlowPeriodCombo.SelectedItem is not ComboBoxItem selected)
                return;

            var selectedPeriod = selected.Tag?.ToString();
            _useWeeklyCashFlow = string.Equals(selectedPeriod, "Weeks", StringComparison.Ordinal);
            _useYearlyCashFlow = string.Equals(selectedPeriod, "Year", StringComparison.Ordinal);
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
            DashboardHeaderGrid.ColumnDefinitions.Clear();
            DashboardHeaderGrid.RowDefinitions.Clear();
            SummaryCardsGrid.ColumnDefinitions.Clear();
            SummaryCardsGrid.RowDefinitions.Clear();
            AnalyticsGrid.ColumnDefinitions.Clear();
            AnalyticsGrid.RowDefinitions.Clear();

            if (compact)
            {
                DashboardHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                DashboardHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                DashboardHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(DashboardIntro, 0);
                Grid.SetRow(DashboardIntro, 0);
                Grid.SetColumn(HeaderActions, 0);
                Grid.SetRow(HeaderActions, 1);
                HeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
                HeaderActions.Margin = new Thickness(0, 12, 0, 0);

                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int row = 0; row < 4; row++)
                    SummaryCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(BalanceCard, 0);
                Grid.SetColumn(StudentCountCard, 0);
                Grid.SetColumn(UnpaidCountCard, 0);
                Grid.SetColumn(ExpenseCard, 0);
                Grid.SetRow(BalanceCard, 0);
                Grid.SetRow(StudentCountCard, 1);
                Grid.SetRow(UnpaidCountCard, 2);
                Grid.SetRow(ExpenseCard, 3);
                BalanceCard.Margin = new Thickness(0, 0, 0, 10);
                StudentCountCard.Margin = new Thickness(0, 0, 0, 10);
                UnpaidCountCard.Margin = new Thickness(0, 0, 0, 10);
                ExpenseCard.Margin = new Thickness(0);

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
                DashboardHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                DashboardHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                DashboardHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(DashboardIntro, 0);
                Grid.SetRow(DashboardIntro, 0);
                Grid.SetColumn(HeaderActions, 1);
                Grid.SetRow(HeaderActions, 0);
                HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
                HeaderActions.Margin = new Thickness(0);

                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(BalanceCard, 0);
                Grid.SetColumn(StudentCountCard, 2);
                Grid.SetColumn(UnpaidCountCard, 4);
                Grid.SetColumn(ExpenseCard, 6);
                Grid.SetRow(BalanceCard, 0);
                Grid.SetRow(StudentCountCard, 0);
                Grid.SetRow(UnpaidCountCard, 0);
                Grid.SetRow(ExpenseCard, 0);
                BalanceCard.Margin = new Thickness(0);
                StudentCountCard.Margin = new Thickness(0);
                UnpaidCountCard.Margin = new Thickness(0);
                ExpenseCard.Margin = new Thickness(0);

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

        private void BuatTagihanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
                return;

            var schedule = DataStore.Instance.JadwalTagihanMingguanList
                .FirstOrDefault(item => item.KelasId == _kelasId);
            var dialog = new JadwalTagihanMingguanDialog(bendahara, _kelasId, _mainWindow, Refresh, schedule);
            _mainWindow.ShowModal(dialog);
        }

        private void LihatSiswaButton_Click(object sender, RoutedEventArgs e) =>
            _mainWindow.NavigateTo(new SiswaPage(_currentUser, _mainWindow));

        private void LihatSemuaButton_Click(object sender, RoutedEventArgs e) =>
            _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow));

        private void TransactionTypeFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clickedButton || clickedButton.Tag is not string selectedType)
                return;

            _transactionFilterType = selectedType;
            AllTransactionsFilter.IsChecked = selectedType == "All";
            IncomeTransactionsFilter.IsChecked = selectedType == "Masuk";
            ExpenseTransactionsFilter.IsChecked = selectedType == "Keluar";
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
            {
                var siswa = DataStore.Instance.Users.OfType<Siswa>()
                    .FirstOrDefault(student => student.KelasId == _kelasId && student.Nis == item.Nis);
                if (siswa != null)
                    _mainWindow.ShowModal(new StudentPaymentHistoryDialog(_currentUser, _mainWindow, siswa, Refresh));
            }
        }
    }
}
