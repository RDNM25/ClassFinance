using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    /// <summary>Small display-only wrapper for the Dashboard's student list: shows every
    /// student who isn't registered to any tagihan yet (name only, no count), plus every
    /// student who owes on at least one tagihan (with how many). Students who are
    /// registered and have paid everything off are left out entirely.</summary>
    public class SiswaDisplayItem
    {
        public string Name { get; set; }
        public string Nis { get; set; }
        public int UnpaidCount { get; set; }
        public int TotalTagihanCount { get; set; }
        public bool IsRegistered => TotalTagihanCount > 0;
    }

    public partial class DashboardPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private bool _isCompactLayout;
        private bool _responsiveLayoutInitialized;

        public DashboardPage(User currentUser, MainWindow mainWindow)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _kelasId = ResolveKelasId(currentUser);

            TitleText.Text = "Uang Kas";
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

            var displaySiswa = siswaList
                .Select(s =>
                {
                    var tagihanEntries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.SiswaId == s.Id).ToList();
                    return new SiswaDisplayItem
                    {
                        Name = s.Name,
                        Nis = s.Nis,
                        UnpaidCount = tagihanEntries.Count(ts => ts.Status != StatusTagihan.Lunas),
                        TotalTagihanCount = tagihanEntries.Count
                    };
                })
                // Keep: not registered to any tagihan yet (shown plain, no count) OR
                // registered and still owes something. Excluded: registered and fully paid.
                .Where(d => d.TotalTagihanCount == 0 || d.UnpaidCount > 0)
                .ToList();

            SiswaItems.ItemsSource = displaySiswa;
            NoSiswaText.Text = siswaList.Any()
                ? "Semua siswa sudah melunasi tagihan mereka."
                : "Belum ada siswa terdaftar.";
            NoSiswaText.Visibility = displaySiswa.Any() ? Visibility.Collapsed : Visibility.Visible;

            var recentTransaksi = kelas.GetRiwayatTransaksi().Take(5).ToList();
            TransaksiItems.ItemsSource = recentTransaksi;
            NoTransaksiText.Visibility = recentTransaksi.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0)
                return;

            var useCompactLayout = e.NewSize.Width < 780;
            if (_responsiveLayoutInitialized && useCompactLayout == _isCompactLayout)
                return;

            _responsiveLayoutInitialized = true;
            _isCompactLayout = useCompactLayout;

            SummaryCardsGrid.ColumnDefinitions.Clear();
            SummaryCardsGrid.RowDefinitions.Clear();
            DashboardSectionsGrid.ColumnDefinitions.Clear();
            DashboardSectionsGrid.RowDefinitions.Clear();

            if (useCompactLayout)
            {
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (var row = 0; row < 3; row++)
                    SummaryCardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(BalanceCard, 0);
                Grid.SetColumn(StudentCountCard, 0);
                Grid.SetColumn(UnpaidCountCard, 0);
                Grid.SetRow(BalanceCard, 0);
                Grid.SetRow(StudentCountCard, 1);
                Grid.SetRow(UnpaidCountCard, 2);
                BalanceCard.Margin = new Thickness(0, 0, 0, 12);
                StudentCountCard.Margin = new Thickness(0, 0, 0, 12);
                UnpaidCountCard.Margin = new Thickness(0);

                DashboardSectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                DashboardSectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                DashboardSectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(StudentsCard, 0);
                Grid.SetColumn(RecentTransactionsCard, 0);
                Grid.SetRow(StudentsCard, 0);
                Grid.SetRow(RecentTransactionsCard, 1);
                StudentsCard.Margin = new Thickness(0, 0, 0, 20);
            }
            else
            {
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SummaryCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
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

                DashboardSectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                DashboardSectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                DashboardSectionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                DashboardSectionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(StudentsCard, 0);
                Grid.SetColumn(RecentTransactionsCard, 2);
                Grid.SetRow(StudentsCard, 0);
                Grid.SetRow(RecentTransactionsCard, 0);
                StudentsCard.Margin = new Thickness(0);
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

        private void LihatSemuaButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow));
        }

        private void SiswaRow_Click(object sender, MouseButtonEventArgs e)
        {
            OpenStudentHistory(sender);
        }

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
