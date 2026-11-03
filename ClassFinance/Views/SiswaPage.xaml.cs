using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
  
    public class SiswaRosterItem
    {
        public Siswa Siswa { get; set; }
        public decimal TotalDibayar { get; set; }
        public int UnpaidCount { get; set; }
        public int ProsesCount { get; set; }
        public int TotalTagihanCount { get; set; }
        public bool CanManage { get; set; }
        public bool HasUnpaid => UnpaidCount > 0;
        public bool HasProses => ProsesCount > 0;

        public bool IsFullyPaid => TotalTagihanCount > 0 && UnpaidCount == 0 && ProsesCount == 0;
        public bool HasNoTagihan => TotalTagihanCount == 0;
    }

    public partial class SiswaPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private ContextMenu _openStudentMenu;
        private Button _pendingStudentMenuToggle;

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

        private static SiswaRosterItem ToRosterItem(Siswa s, bool canManage)
        {
            var tagihanEntries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.SiswaId == s.Id).ToList();
            return new SiswaRosterItem
            {
                Siswa = s,
                TotalDibayar = DataStore.Instance.TransaksiList
                    .Where(t => t.Type == JenisTransaksi.Masuk && t.SiswaId == s.Id)
                    .Sum(t => (decimal?)t.Amount) ?? 0,
                UnpaidCount = tagihanEntries.Count(ts => ts.Status != StatusTagihan.Lunas && ts.Status != StatusTagihan.Proses),
                ProsesCount = tagihanEntries.Count(ts => ts.Status == StatusTagihan.Proses),
                TotalTagihanCount = tagihanEntries.Count,
                CanManage = canManage
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

            var rosterItems = filteredSiswa.Select(s => ToRosterItem(s, _currentUser is Bendahara)).ToList();
            SiswaItems.ItemsSource = rosterItems;

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
            if (_currentUser is Bendahara && sender is FrameworkElement element && element.Tag is Siswa siswa)
            {
                var confirm = MessageBox.Show($"Hapus data siswa '{siswa.Name}'?", "Hapus siswa",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    DataStore.Instance.HapusSiswa(siswa.Id);
                    Refresh();
                }
            }
        }

        private void EditSiswa_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara && sender is FrameworkElement element && element.Tag is Siswa siswa)
                _mainWindow.ShowModal(new EditSiswaDialog(siswa, _mainWindow, Refresh));
        }

        private void ShowStudentActions_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara || sender is not Button button ||
                button.DataContext is not SiswaRosterItem rosterItem)
                return;

            if (CloseOpenStudentMenuIfSameButton(button))
                return;

            if (_openStudentMenu?.IsOpen == true)
                _openStudentMenu.IsOpen = false;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("PopupActionMenu"),
                Placement = PlacementMode.Bottom
            };
            menu.Items.Add(CreateStudentMenuItem("Ubah data siswa", rosterItem.Siswa, EditSiswa_Click));
            menu.Items.Add(CreateStudentMenuItem("Hapus siswa", rosterItem.Siswa, Hapus_Click));
            menu.PlacementTarget = button;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_pendingStudentMenuToggle, button)) return;
                if (ReferenceEquals(_openStudentMenu, menu))
                    _openStudentMenu = null;
            };

            _openStudentMenu = menu;
            menu.IsOpen = menu.Items.Count > 0;
        }

        private bool CloseOpenStudentMenuIfSameButton(Button button)
        {
            bool togglingCurrent = ReferenceEquals(_pendingStudentMenuToggle, button) ||
                ReferenceEquals(_openStudentMenu?.PlacementTarget, button);
            _pendingStudentMenuToggle = null;
            if (!togglingCurrent)
                return false;

            if (_openStudentMenu != null)
                _openStudentMenu.IsOpen = false;
            _openStudentMenu = null;
            return true;
        }

        private void StudentActionMenuButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _pendingStudentMenuToggle = sender is Button button &&
                ReferenceEquals(_openStudentMenu?.PlacementTarget, button) ? button : null;
        }

        private static MenuItem CreateStudentMenuItem(string header, Siswa siswa, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = siswa };
            item.Click += onClick;
            return item;
        }

        private void SiswaName_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Siswa siswa)
                _mainWindow.NavigateTo(new RiwayatTransaksiPage(_currentUser, _mainWindow, siswa.Nis));
        }
    }
}
