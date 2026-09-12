using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class TransaksiDisplayItem
    {
        public Transaksi Transaksi { get; set; }
        public string TagihanName { get; set; }
    }

    /// <summary>
    /// Per-student row: a class tagihan on the left, and what this specific student
    /// paid toward it on the right (blank if they haven't paid at all).
    /// </summary>
    public class TagihanPaymentDisplayItem
    {
        public Tagihan Tagihan { get; set; }
        public bool HasPaid { get; set; }
        public bool IsLunas { get; set; }
        public decimal AmountPaid { get; set; }
        // Display the amount if they have paid anything (even partially)
        public string AmountPaidDisplay => AmountPaid > 0 ? $"Rp {AmountPaid:N0}" : string.Empty;
    }

    public partial class RiwayatTransaksiPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private readonly string _nis;

        public RiwayatTransaksiPage(User currentUser, MainWindow mainWindow, string nis = null)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _nis = nis;
            _kelasId = currentUser switch
            {
                Siswa s => s.KelasId,
                WaliKelas w => w.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };

            if (!string.IsNullOrEmpty(nis))
            {
                var siswa = FindSiswaByNis(nis);
                TitleText.Text = siswa != null
                    ? $"STATUS TAGIHAN — {siswa.Name}"
                    : "STATUS TAGIHAN";
                FilterCombo.Visibility = Visibility.Collapsed;
                TambahButton.Visibility = Visibility.Collapsed;
                SummaryRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                TambahButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
                FilterCombo.SelectionChanged += FilterCombo_SelectionChanged;
            }

            Refresh();
        }

        private void Refresh()
        {
            var siswa = FindSiswaByNis(_nis);

            if (siswa != null)
            {
                RefreshTagihanStatus(siswa);
                return;
            }

            RefreshTransaksi();
        }

        /// <summary>Class-wide transaction history (no specific student selected).</summary>
        private void RefreshTransaksi()
        {
            var all = DataStore.Instance.KelasList.First(k => k.Id == _kelasId).GetRiwayatTransaksi();

            var totalEarned = all.Where(t => t.Type == JenisTransaksi.Masuk && t.AffectsKas).Sum(t => t.Amount);
            var totalSpent = all.Where(t => t.Type == JenisTransaksi.Keluar && t.AffectsKas).Sum(t => t.Amount);
            TotalKasText.Text = $"Rp {(totalEarned - totalSpent):N0}";
            TotalEarnedText.Text = $"Rp {totalEarned:N0}";
            TotalSpentText.Text = $"Rp {totalSpent:N0}";

            var filtered = FilterCombo.SelectedIndex switch
            {
                1 => all.Where(t => t.Type == JenisTransaksi.Masuk).ToList(),
                2 => all.Where(t => t.Type == JenisTransaksi.Keluar).ToList(),
                _ => all
            };

            TransaksiItems.ItemsSource = filtered.Select(t => new TransaksiDisplayItem
            {
                Transaksi = t,
                TagihanName = null
            }).ToList();
            TransaksiItems.Visibility = Visibility.Visible;
            TagihanPaymentItems.Visibility = Visibility.Collapsed;

            EmptyText.Text = "Belum ada transaksi.";
            EmptyText.Visibility = filtered.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Per-student view: every class tagihan on the left, with what this student
        /// paid toward each one on the right.
        /// </summary>
        private void RefreshTagihanStatus(Siswa siswa)
        {
            var tagihanList = DataStore.Instance.TagihanList
                .Where(t => t.KelasId == _kelasId)
                .OrderByDescending(t => t.Id)
                .Select(t =>
                {
                    var entry = DataStore.Instance.TagihanSiswaList
                        .FirstOrDefault(ts => ts.TagihanId == t.Id && ts.SiswaId == siswa.Id);

                    // Use the new JumlahDibayar and Status properties to reflect partial/full payments
                    bool hasPaid = entry != null && entry.JumlahDibayar > 0;
                    bool isLunas = entry != null && entry.Status == StatusTagihan.Lunas;

                    return new TagihanPaymentDisplayItem
                    {
                        Tagihan = t,
                        HasPaid = hasPaid,
                        IsLunas = isLunas,
                        AmountPaid = entry?.JumlahDibayar ?? 0
                    };
                })
                .ToList();

            TagihanPaymentItems.ItemsSource = tagihanList;
            TagihanPaymentItems.Visibility = Visibility.Visible;
            TransaksiItems.Visibility = Visibility.Collapsed;

            EmptyText.Text = "Belum ada tagihan.";
            EmptyText.Visibility = tagihanList.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private Siswa FindSiswaByNis(string nis) =>
            string.IsNullOrEmpty(nis)
                ? null
                : DataStore.Instance.Users.OfType<Siswa>()
                    .FirstOrDefault(s => s.KelasId == _kelasId && s.Nis == nis);

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Refresh();

        private void TambahButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahTransaksiDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }
    }
}
