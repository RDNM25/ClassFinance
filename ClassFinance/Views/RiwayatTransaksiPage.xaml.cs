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
                    ? $"RIWAYAT TRANSAKSI — {siswa.Name}"
                    : "RIWAYAT TRANSAKSI";
                FilterCombo.Visibility = Visibility.Collapsed;
                TambahButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                TambahButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
                // Attached here (instead of in XAML) so it can't fire during InitializeComponent(),
                // before _kelasId above is set -- that used to crash with "Sequence contains no
                // matching element" because _kelasId was still 0 at that point.
                FilterCombo.SelectionChanged += FilterCombo_SelectionChanged;
            }

            Refresh();
        }

        private void Refresh()
        {
            var all = DataStore.Instance.KelasList.First(k => k.Id == _kelasId).GetRiwayatTransaksi();

            var siswa = FindSiswaByNis(_nis);
            if (siswa != null)
                all = all.Where(t => t.SiswaId == siswa.Id).ToList();

            var filtered = string.IsNullOrEmpty(_nis)
                ? FilterCombo.SelectedIndex switch
                {
                    1 => all.Where(t => t.Type == JenisTransaksi.Masuk).ToList(),
                    2 => all.Where(t => t.Type == JenisTransaksi.Keluar).ToList(),
                    _ => all
                }
                : all;

            var showTagihan = !string.IsNullOrEmpty(_nis);
            TransaksiItems.ItemsSource = filtered.Select(t => new TransaksiDisplayItem
            {
                Transaksi = t,
                TagihanName = showTagihan ? ResolveTagihanName(t) : null
            }).ToList();
            EmptyText.Visibility = filtered.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string ResolveTagihanName(Transaksi t)
        {
            if (!t.TagihanSiswaId.HasValue) return null;

            var tagihanSiswa = DataStore.Instance.TagihanSiswaList
                .FirstOrDefault(ts => ts.Id == t.TagihanSiswaId.Value);
            if (tagihanSiswa == null) return null;

            return DataStore.Instance.TagihanList
                .FirstOrDefault(tag => tag.Id == tagihanSiswa.TagihanId)?.Name;
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
