using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public partial class RiwayatTransaksiPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private readonly int? _siswaId;

        public RiwayatTransaksiPage(User currentUser, MainWindow mainWindow, int? siswaId = null)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _siswaId = siswaId;
            _kelasId = currentUser switch
            {
                Siswa s => s.KelasId,
                WaliKelas w => w.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };

            if (siswaId.HasValue)
            {
                var siswa = DataStore.Instance.Users.OfType<Siswa>().FirstOrDefault(s => s.Id == siswaId.Value);
                TitleText.Text = siswa != null
                    ? $"RIWAYAT TRANSAKSI — {siswa.Name}"
                    : "RIWAYAT TRANSAKSI";
            }

            TambahButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
            Refresh();

            // Attached here (instead of in XAML) so it can't fire during InitializeComponent(),
            // before _kelasId above is set -- that used to crash with "Sequence contains no
            // matching element" because _kelasId was still 0 at that point.
            FilterCombo.SelectionChanged += FilterCombo_SelectionChanged;
        }

        private void Refresh()
        {
            var all = DataStore.Instance.KelasList.First(k => k.Id == _kelasId).GetRiwayatTransaksi();

            if (_siswaId.HasValue)
                all = all.Where(t => t.SiswaId == _siswaId.Value).ToList();

            var filterIndex = FilterCombo.SelectedIndex;
            var filtered = filterIndex switch
            {
                1 => all.Where(t => t.Type == JenisTransaksi.Masuk).ToList(),
                2 => all.Where(t => t.Type == JenisTransaksi.Keluar).ToList(),
                _ => all
            };

            TransaksiItems.ItemsSource = filtered;
            EmptyText.Visibility = filtered.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Refresh();

        private void TambahButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahTransaksiDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }
    }
}
