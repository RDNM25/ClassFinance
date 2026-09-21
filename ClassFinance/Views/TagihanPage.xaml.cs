using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class TagihanSiswaDisplay
    {
        public TagihanSiswa Entry { get; set; }
        public string SiswaName { get; set; }
    }

    public class TagihanDisplayItem
    {
        public Tagihan Tagihan { get; set; }
        public bool IsFullyPaid { get; set; }
    }

    public partial class TagihanPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private Tagihan _selectedTagihan;
        private int? _initialTagihanId; 

        public TagihanPage(User currentUser, MainWindow mainWindow, int? initialTagihanId = null)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _mainWindow = mainWindow;
            _initialTagihanId = initialTagihanId;
            _kelasId = currentUser switch
            {
                Siswa s => s.KelasId,
                WaliKelas w => w.KelasId,
                _ => DataStore.Instance.KelasList.First().Id
            };

            BuatTagihanButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void Refresh()
        {
            if (TagihanItems == null) return;

            var query = DataStore.Instance.TagihanList.Where(t => t.KelasId == _kelasId)
                .OrderByDescending(t => t.Id)
                .Select(t =>
                {
                    var entries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.TagihanId == t.Id).ToList();
                    return new TagihanDisplayItem
                    {
                        Tagihan = t,
                        IsFullyPaid = entries.Any() && entries.All(ts => ts.Status == StatusTagihan.Lunas)
                    };
                });

            if (TagihanFilterComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag)
            {
                query = filterTag switch
                {
                    "Selesai" => query.Where(x => x.IsFullyPaid),
                    "BelumSelesai" => query.Where(x => !x.IsFullyPaid),
                    _ => query
                };
            }

            var tagihanList = query.ToList();
            TagihanItems.ItemsSource = tagihanList;
            EmptyText.Visibility = tagihanList.Any() ? Visibility.Collapsed : Visibility.Visible;

            if (_initialTagihanId.HasValue)
            {
                var target = tagihanList.FirstOrDefault(x => x.Tagihan.Id == _initialTagihanId.Value);
                if (target != null)
                {
                    TagihanItems.SelectedItem = target;
                    TagihanItems.ScrollIntoView(target);
                }
                _initialTagihanId = null;
            }
            else if (_selectedTagihan != null)
            {
                TagihanItems.SelectedItem = tagihanList.FirstOrDefault(x => x.Tagihan.Id == _selectedTagihan.Id);
                ApplyFilter();
            }
        }

        private void TagihanFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagihanItems != null) Refresh();
        }

        private void TagihanItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagihanItems.SelectedItem is TagihanDisplayItem displayItem)
            {
                _selectedTagihan = displayItem.Tagihan;
                DetailTitle.Text = $"Status pembayaran \u2014 {_selectedTagihan.Name}";
                ApplyFilter();
            }
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_selectedTagihan == null) return;

            var query = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.TagihanId == _selectedTagihan.Id)
                .Select(ts => new TagihanSiswaDisplay
                {
                    Entry = ts,
                    SiswaName = DataStore.Instance.Users.OfType<Siswa>().FirstOrDefault(s => s.Id == ts.SiswaId)?.Name ?? "?"
                });

            if (StatusFilterComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag)
            {
                query = filterTag switch
                {
                    "Lunas" => query.Where(x => x.Entry.Status == StatusTagihan.Lunas),
                    "BelumLunas" => query.Where(x => x.Entry.Status == StatusTagihan.BelumBayar),
                    "Sebagian" => query.Where(x => x.Entry.Status == StatusTagihan.Sebagian),
                    _ => query
                };
            }

            DetailItems.ItemsSource = query.ToList();
        }

        private void BuatTagihanButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahTagihanDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void TandaiLunas_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
            {
                MessageBox.Show("Hanya Bendahara yang dapat mencatat pembayaran.", "Akses ditolak");
                return;
            }

            if (sender is Button btn && btn.Tag is TagihanSiswaDisplay display && display.Entry.Status != StatusTagihan.Lunas)
            {
                bendahara.CatatPembayaran(display.Entry.Id, display.Entry.AmountDue, "Tunai");
                Refresh();
            }
        }
        private void BayarManual_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
            {
                MessageBox.Show("Hanya Bendahara yang dapat mencatat pembayaran.", "Akses ditolak");
                return;
            }

            if (sender is Button btn && btn.Tag is TagihanSiswaDisplay display && display.Entry.Status != StatusTagihan.Lunas)
            {
                var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == display.Entry.SiswaId);
                var dialog = new BayarManualDialog(bendahara, siswa, display.Entry, _mainWindow, onSaved: () => Refresh());
                _mainWindow.ShowModal(dialog);
            }
        }
    }
}