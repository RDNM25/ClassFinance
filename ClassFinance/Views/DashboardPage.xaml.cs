using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    /// <summary>Small display-only wrapper so the student list can show a computed total next to each name.</summary>
    public class SiswaDisplayItem
    {
        public string Name { get; set; }
        public string Nis { get; set; }
        public decimal TotalDibayar { get; set; }
    }

    public partial class DashboardPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;

        public DashboardPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = ResolveKelasId(currentUser);

            TitleText.Text = "UANG KAS";
            SubtitleText.Text = $"Selamat datang, {currentUser.Name} ({RoleLabel(currentUser.Role)})";

            // Only Bendahara can withdraw cash or add students
            bool canManage = currentUser is Bendahara;
            TarikKasButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;
            TambahSiswaButton.Visibility = (canManage || currentUser is WaliKelas) ? Visibility.Visible : Visibility.Collapsed;

            Refresh();
        }

        private static int ResolveKelasId(User user) => user switch
        {
            Siswa s => s.KelasId,
            WaliKelas w => w.KelasId,
            _ => DataStore.Instance.KelasList.First().Id
        };

        private static string RoleLabel(Role role) => role switch
        {
            Role.Bendahara => "Bendahara",
            Role.WaliKelas => "Wali Kelas",
            Role.Siswa => "Siswa",
            _ => role.ToString()
        };

        private void Refresh()
        {
            var kelas = DataStore.Instance.KelasList.First(k => k.Id == _kelasId);
            SaldoText.Text = $"Rp {kelas.HitungSaldo():N0}";

            var siswaList = DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == _kelasId).ToList();
            TotalSiswaText.Text = siswaList.Count.ToString();

            int belumLunas = DataStore.Instance.TagihanSiswaList
                .Count(t => t.Status == StatusTagihan.BelumBayar &&
                            siswaList.Any(s => s.Id == t.SiswaId));
            BelumLunasText.Text = belumLunas.ToString();

            var displaySiswa = siswaList.Select(s => new SiswaDisplayItem
            {
                Name = s.Name,
                Nis = s.Nis,
                TotalDibayar = DataStore.Instance.TransaksiList
                    .Where(t => t.Type == JenisTransaksi.Masuk && t.SiswaId == s.Id)
                    .Sum(t => (decimal?)t.Amount) ?? 0
            }).ToList();

            SiswaItems.ItemsSource = displaySiswa;
            NoSiswaText.Visibility = displaySiswa.Any() ? Visibility.Collapsed : Visibility.Visible;

            var recentTransaksi = kelas.GetRiwayatTransaksi().Take(5).ToList();
            TransaksiItems.ItemsSource = recentTransaksi;
            NoTransaksiText.Visibility = recentTransaksi.Any() ? Visibility.Collapsed : Visibility.Visible;
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

        private void LihatSemuaButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow));
        }
    }
}
