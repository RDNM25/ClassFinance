using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
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

        private void Refresh()
        {
            var siswaList = DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == _kelasId).ToList();
            SiswaItems.ItemsSource = siswaList;
            EmptyText.Visibility = siswaList.Any() ? Visibility.Collapsed : Visibility.Visible;
        }


        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var allSiswa = DataStore.Instance.Users
                .OfType<Siswa>()
                .Where(s => s.KelasId == _kelasId)
                .ToList();

            var keyword = SearchBox.Text?.Trim().ToLower() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(keyword)
                ? allSiswa
                : allSiswa.Where(s =>
                    (s.Name != null && s.Name.ToLower().Contains(keyword)) ||
                    (s.Nis != null && s.Nis.ToLower().Contains(keyword))
                  ).ToList();

            SiswaItems.ItemsSource = filtered;

            // Handle empty state visibility
            if (filtered.Count == 0)
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
