using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    /// <summary>
    /// Wraps a Siswa for the roster page with two computed figures: how much they've
    /// paid in total, and how many tagihan they still haven't fully paid off. Unlike
    /// the Dashboard's list, nobody is filtered out here -- this is the full roster.
    /// </summary>
    public class SiswaRosterItem
    {
        public Siswa Siswa { get; set; }
        public decimal TotalDibayar { get; set; }
        public int UnpaidCount { get; set; }
        public int TotalTagihanCount { get; set; }
        public bool HasUnpaid => UnpaidCount > 0;

        // Guarded by TotalTagihanCount > 0: a student with zero tagihan assigned
        // (e.g. just added, or the class has no tagihan yet at all) has UnpaidCount
        // == 0 too, but that's "nothing to pay" -- not "paid everything" -- so it
        // must not be reported as fully paid.
        public bool IsFullyPaid => TotalTagihanCount > 0 && UnpaidCount == 0;
        public bool HasNoTagihan => TotalTagihanCount == 0;
    }

    public partial class SiswaPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;

        public SiswaPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = currentUser switch
            {
                Siswa s => s.KelasId,
                WaliKelas w => w.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };

            bool canManage = currentUser is Bendahara || currentUser is WaliKelas;
            TambahButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;

            Refresh();
        }

        private void Refresh() => ApplyFilter();


        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private static SiswaRosterItem ToRosterItem(Siswa s)
        {
            var tagihanEntries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.SiswaId == s.Id).ToList();
            return new SiswaRosterItem
            {
                Siswa = s,
                TotalDibayar = DataStore.Instance.TransaksiList
                    .Where(t => t.Type == JenisTransaksi.Masuk && t.SiswaId == s.Id)
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                UnpaidCount = tagihanEntries.Count(ts => ts.Status != StatusTagihan.Lunas),
                TotalTagihanCount = tagihanEntries.Count
            };
        }

        private void ApplyFilter()
        {
            var allSiswa = DataStore.Instance.Users
                .OfType<Siswa>()
                .Where(s => s.KelasId == _kelasId)
                .ToList();

            var keyword = SearchBox.Text?.Trim().ToLower() ?? string.Empty;

            var filteredSiswa = string.IsNullOrEmpty(keyword)
                ? allSiswa
                : allSiswa.Where(s =>
                    (s.Name != null && s.Name.ToLower().Contains(keyword)) ||
                    (s.Nis != null && s.Nis.ToLower().Contains(keyword))
                  ).ToList();

            var rosterItems = filteredSiswa.Select(ToRosterItem).ToList();
            SiswaItems.ItemsSource = rosterItems;

            // Handle empty state visibility
            if (rosterItems.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                EmptyText.Text = allSiswa.Count == 0
                    ? "Belum ada siswa terdaftar di kelas ini."
                    : "Siswa tidak ditemukan.";
            }
            else
            {
                EmptyText.Visibility = Visibility.Collapsed;
            }
        }

        private void TambahButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahSiswaDialog(_kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
            ApplyFilter();
        }

        private void Hapus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Siswa siswa)
            {
                var confirm = MessageBox.Show($"Hapus data siswa '{siswa.Name}'?", "Konfirmasi",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    DataStore.Instance.HapusSiswa(siswa.Id);
                    ApplyFilter();
                    Refresh();
                }
            }
        }

        private void SiswaName_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Siswa siswa)
                _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow, siswa.Nis));
        }
    }
}
