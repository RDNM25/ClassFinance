using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>Wraps a Tagihan with a computed flag for whether every student has paid it off.</summary>
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

        public TagihanPage(User currentUser, MainWindow mainWindow)
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

            BuatTagihanButton.Visibility = currentUser is Bendahara ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void Refresh()
        {
            var tagihanList = DataStore.Instance.TagihanList.Where(t => t.KelasId == _kelasId)
                .OrderByDescending(t => t.Id) // Id increments on creation, so this puts the newest tagihan first
                .Select(t =>
                {
                    var entries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.TagihanId == t.Id).ToList();
                    return new TagihanDisplayItem
                    {
                        Tagihan = t,
                        IsFullyPaid = entries.Any() && entries.All(ts => ts.Status == StatusTagihan.Lunas)
                    };
                })
                .ToList();

            TagihanItems.ItemsSource = tagihanList;
            EmptyText.Visibility = tagihanList.Any() ? Visibility.Collapsed : Visibility.Visible;

            if (_selectedTagihan != null) ShowDetail(_selectedTagihan);
        }

        private void ShowDetail(Tagihan tagihan)
        {
            _selectedTagihan = tagihan;
            DetailTitle.Text = $"Status pembayaran \u2014 {tagihan.Name}";

            var entries = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.TagihanId == tagihan.Id)
                .Select(ts => new TagihanSiswaDisplay
                {
                    Entry = ts,
                    SiswaName = DataStore.Instance.Users.OfType<Siswa>().FirstOrDefault(s => s.Id == ts.SiswaId)?.Name ?? "?"
                })
                .ToList();

            DetailItems.ItemsSource = entries;
        }

        private void TagihanRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Tagihan tagihan)
                ShowDetail(tagihan);
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
    }
}
