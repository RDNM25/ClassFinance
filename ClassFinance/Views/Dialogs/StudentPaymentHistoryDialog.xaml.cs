using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public sealed class StudentWeekPaymentDisplay
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public TagihanSiswa Entry { get; init; }
        public Tagihan Bill { get; init; }
        public string WeekText { get; init; }
        public string PeriodText { get; init; }
        public string StatusText { get; init; }
        public string MethodText { get; init; }
        public decimal AmountPaid { get; init; }
        public decimal AmountDue { get; init; }
        public string AmountText => AmountPaid > 0
            ? $"+Rp {AmountPaid.ToString("N0", IdId)}"
            : $"−Rp {AmountDue.ToString("N0", IdId)}";
        public string RemainingText => AmountDue > 0 ? $"Sisa Rp {AmountDue.ToString("N0", IdId)}" : string.Empty;
        public Brush AmountForeground => FindBrush(AmountPaid > 0 ? "IncomeColor" : "StatusRed", Brushes.Gray);
        public Brush StatusForeground => FindBrush(Entry.Status switch
        {
            StatusTagihan.Lunas => "StatusGreen",
            StatusTagihan.Proses or StatusTagihan.Sebagian => "StatusAmber",
            _ => "StatusRed"
        }, Brushes.Gray);
        public Brush StatusBackground => FindBrush(Entry.Status == StatusTagihan.Lunas ? "CashFlowIncomeFill" : "WarningBadgeBackground", Brushes.Transparent);

        private static Brush FindBrush(string key, Brush fallback) =>
            Application.Current?.TryFindResource(key) as Brush ?? fallback;
    }

    public partial class StudentPaymentHistoryDialog : UserControl
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly Siswa _siswa;
        private readonly Action _onSaved;

        public StudentPaymentHistoryDialog(User currentUser, MainWindow mainWindow, Siswa siswa, Action onSaved = null)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _siswa = siswa;
            _onSaved = onSaved;

            PayButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void Refresh()
        {
            var store = DataStore.Instance;
            var weeklyBills = store.TagihanList
                .Where(bill => bill.KelasId == _siswa.KelasId && bill.IsWeeklyOccurrence)
                .ToDictionary(bill => bill.Id);
            var entries = store.TagihanSiswaList
                .Where(entry => entry.SiswaId == _siswa.Id && weeklyBills.ContainsKey(entry.TagihanId))
                .Select(entry => new { Entry = entry, Bill = weeklyBills[entry.TagihanId] })
                .OrderByDescending(item => item.Bill.PeriodStart)
                .ThenByDescending(item => item.Bill.Id)
                .ToList();

            StudentNameText.Text = _siswa.Name;
            AvatarText.Text = GetInitials(_siswa.Name);
            var kelas = store.KelasList.FirstOrDefault(item => item.Id == _siswa.KelasId);
            StudentDetailsText.Text = $"NIS {_siswa.Nis} · {kelas?.Name ?? "Kelas tidak tersedia"}";
            TotalPaidText.Text = $"Rp {entries.Sum(item => item.Entry.JumlahDibayar).ToString("N0", IdId)}";

            var dueEntries = entries.Where(item => item.Entry.Status != StatusTagihan.Proses && item.Entry.AmountDue > 0).ToList();
            var outstanding = dueEntries.Sum(item => item.Entry.AmountDue);
            string statusLabel = dueEntries.Count > 0
                ? $"{dueEntries.Count} minggu tertunggak · Rp {outstanding.ToString("N0", IdId)}"
                : entries.Any(item => item.Entry.Status == StatusTagihan.Proses)
                    ? "Refund diproses"
                    : entries.Count > 0
                        ? $"Lunas s/d Minggu ke-{entries.Max(item => JadwalTagihanMingguanService.GetWeekNumber(item.Bill))}"
                        : "Belum ada tagihan";
            StatusText.Text = statusLabel;
            StatusBadge.Background = FindResource(dueEntries.Count > 0 ? "WarningBadgeBackground" : "CashFlowIncomeFill") as Brush;
            StatusText.Foreground = FindResource(dueEntries.Count > 0 ? "StatusRed" : entries.Any(item => item.Entry.Status == StatusTagihan.Proses) ? "StatusAmber" : "StatusGreen") as Brush;

            var paymentItems = entries.Select(item =>
            {
                var methods = store.TransaksiList
                    .Where(transaction => transaction.TagihanSiswaId == item.Entry.Id &&
                                          transaction.Type == JenisTransaksi.Masuk && transaction.Amount > 0)
                    .OrderByDescending(transaction => transaction.Date)
                    .Select(transaction => transaction.Metode.ToDisplayLabel())
                    .Distinct()
                    .ToList();
                var status = item.Entry.Status switch
                {
                    StatusTagihan.Lunas => "Lunas",
                    StatusTagihan.Proses => "Proses",
                    StatusTagihan.Sebagian => "Sebagian",
                    _ => "Belum lunas"
                };
                return new StudentWeekPaymentDisplay
                {
                    Entry = item.Entry,
                    Bill = item.Bill,
                    WeekText = $"Minggu ke-{JadwalTagihanMingguanService.GetWeekNumber(item.Bill)}",
                    PeriodText = FormatPeriod(item.Bill.PeriodStart, item.Bill.PeriodEnd),
                    StatusText = status,
                    MethodText = methods.Count > 0 ? string.Join(", ", methods) : "—",
                    AmountPaid = item.Entry.JumlahDibayar,
                    AmountDue = item.Entry.AmountDue
                };
            }).ToList();

            PaymentHistoryItems.ItemsSource = paymentItems;
            EmptyHistoryText.Visibility = paymentItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatPeriod(DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue)
                return "Tanggal tidak tersedia";
            if (start.Value.Year == end.Value.Year && start.Value.Month == end.Value.Month)
                return $"{start.Value.Day}–{end.Value.Day} {end.Value.ToString("MMM yyyy", IdId)}";
            return $"{start.Value.ToString("dd MMM", IdId)}–{end.Value.ToString("dd MMM yyyy", IdId)}";
        }

        private static string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "S",
                1 => parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant(),
                _ => $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}"
            };
        }

        private static Brush FindResource(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;

        private void PayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
                return;

            var weeklyBills = DataStore.Instance.TagihanList
                .Where(bill => bill.KelasId == _siswa.KelasId && bill.IsWeeklyOccurrence && bill.PeriodStart.HasValue)
                .OrderByDescending(bill => bill.PeriodStart)
                .ToList();
            var entriesByBill = DataStore.Instance.TagihanSiswaList
                .Where(entry => entry.SiswaId == _siswa.Id)
                .ToDictionary(entry => entry.TagihanId);
            var currentWeek = JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            var selectedEntry = weeklyBills
                .Where(bill => entriesByBill.ContainsKey(bill.Id) &&
                               JadwalTagihanMingguanService.MondayOf(bill.PeriodStart.Value) < currentWeek)
                .OrderBy(bill => bill.PeriodStart)
                .Select(bill => entriesByBill[bill.Id])
                .FirstOrDefault(entry => entry.Status != StatusTagihan.Proses && entry.AmountDue > 0);

            if (selectedEntry == null)
            {
                PaymentActionStatusText.Text = "Tidak ada tagihan minggu sebelumnya yang masih perlu dibayar.";
                PaymentActionStatusText.Visibility = Visibility.Visible;
                return;
            }

            _mainWindow.ShowModal(new BayarManualDialog(bendahara, _siswa, selectedEntry, _mainWindow, _onSaved));
        }

        private void Close_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
