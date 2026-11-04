using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using ClassFinance.Models;
using ClassFinance.Converters;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class TransaksiDisplayItem
    {
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public Transaksi Transaksi { get; set; }
        public string TagihanName { get; set; }
        public string DateText => Transaksi.Date.ToString("dd MMM yyyy", IdId);
        public string TimeText => Transaksi.Date.ToString("HH:mm", IdId);
        public string DescriptionText => string.IsNullOrWhiteSpace(Transaksi.Description) ? "Transaksi" : Transaksi.Description;
        public string TypeLabel => Transaksi.Type == JenisTransaksi.Masuk ? "Pemasukan" : "Pengeluaran";
        public bool CanMarkRefundPaid { get; set; }
        public bool CanEditAmount { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShowActions => CanEditAmount || CanDelete;
        public string RefundStatus => Transaksi != null && Transaksi.IsRefund
            ? (Transaksi.IsPendingRefund ? "Proses" : "Lunas")
            : string.Empty;
    }

    public class TagihanPaymentDisplayItem
    {
        public Tagihan Tagihan { get; set; }
        public bool HasPaid { get; set; }
        public bool IsLunas { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AmountDue { get; set; }
        public StatusTagihan Status { get; set; }
        public string AmountPaidDisplay => AmountPaid > 0 ? $"Rp {AmountPaid:N0}" : string.Empty;
    }

    public partial class RiwayatTransaksiPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private readonly string _nis;
        private ContextMenu _openTransactionMenu;
        private Button _pendingTransactionMenuToggle;

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
            ((HasTransactionDateConverter)FindResource("TransactionDateHighlight")).KelasId = _kelasId;

            if (!string.IsNullOrEmpty(nis))
            {
                var siswa = FindSiswaByNis(nis);
                TitleText.Text = siswa != null
                    ? $"Status Tagihan — {siswa.Name}"
                    : "Status Tagihan";
                FilterCombo.Visibility = Visibility.Collapsed;
                DateFilterPanel.Visibility = Visibility.Collapsed;
                SummaryScopeText.Visibility = Visibility.Collapsed;
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

        private void RefreshTransaksi()
        {
            var all = DataStore.Instance.KelasList.First(k => k.Id == _kelasId).GetRiwayatTransaksi();

            var dateFiltered = all.Where(t =>
                (!DateFilterPicker.SelectedDate.HasValue || t.Date.Date == DateFilterPicker.SelectedDate.Value.Date)).ToList();

            var filtered = FilterCombo.SelectedIndex switch
            {
                1 => dateFiltered.Where(t => t.Type == JenisTransaksi.Masuk).ToList(),
                2 => dateFiltered.Where(t => t.Type == JenisTransaksi.Keluar).ToList(),
                _ => dateFiltered
            };

            var totalEarned = filtered.Where(t => t.Type == JenisTransaksi.Masuk && t.AffectsKas).Sum(t => t.Amount);
            var totalSpent = filtered.Where(t => t.Type == JenisTransaksi.Keluar && t.AffectsKas).Sum(t => t.Amount);
            var currentKas = all.Where(t => t.AffectsKas)
                .Sum(t => t.Type == JenisTransaksi.Masuk ? t.Amount : -t.Amount);
            TotalKasText.Text = $"Rp {currentKas:N0}";
            TotalEarnedText.Text = $"Rp {totalEarned:N0}";
            TotalSpentText.Text = $"Rp {totalSpent:N0}";

            TransaksiItems.ItemsSource = filtered.Select(t => new TransaksiDisplayItem
            {
                Transaksi = t,
                TagihanName = null,
                CanMarkRefundPaid = _currentUser is Bendahara && t.IsPendingRefund,
                CanEditAmount = CanEditTransactionAmount(t),
                CanDelete = CanDeleteTransaction(t)
            }).ToList();
            TransactionListHeader.Visibility = Visibility.Visible;
            TransactionHeaderSeparator.Visibility = Visibility.Visible;
            TagihanPaymentHeader.Visibility = Visibility.Collapsed;
            TagihanPaymentHeaderSeparator.Visibility = Visibility.Collapsed;
            TransaksiItems.Visibility = Visibility.Visible;
            TagihanPaymentItems.Visibility = Visibility.Collapsed;
            StudentBillTotals.Visibility = Visibility.Collapsed;

            EmptyText.Text = "Belum ada transaksi.";
            EmptyText.Visibility = filtered.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshTagihanStatus(Siswa siswa)
        {
            var tagihanList = DataStore.Instance.TagihanSiswaList
                .Where(entry => entry.SiswaId == siswa.Id)
                .Join(
                    DataStore.Instance.TagihanList.Where(tagihan => tagihan.KelasId == _kelasId),
                    entry => entry.TagihanId,
                    tagihan => tagihan.Id,
                    (entry, tagihan) => new { Entry = entry, Tagihan = tagihan })
                .OrderByDescending(item => item.Tagihan.CreatedDate)
                .ThenByDescending(item => item.Tagihan.Id)
                .Select(item =>
                {
                    return new TagihanPaymentDisplayItem
                    {
                        Tagihan = item.Tagihan,
                        HasPaid = item.Entry.JumlahDibayar > 0,
                        IsLunas = item.Entry.Status == StatusTagihan.Lunas,
                        AmountPaid = item.Entry.JumlahDibayar,
                        AmountDue = item.Entry.AmountDue,
                        Status = item.Entry.Status
                    };
                })
                .ToList();

            TagihanPaymentItems.ItemsSource = tagihanList;
            TagihanPaymentItems.Visibility = Visibility.Visible;
            TransactionListHeader.Visibility = Visibility.Collapsed;
            TransactionHeaderSeparator.Visibility = Visibility.Collapsed;
            TagihanPaymentHeader.Visibility = tagihanList.Any() ? Visibility.Visible : Visibility.Collapsed;
            TagihanPaymentHeaderSeparator.Visibility = tagihanList.Any() ? Visibility.Visible : Visibility.Collapsed;
            TransaksiItems.Visibility = Visibility.Collapsed;

            var totalOwed = tagihanList.Sum(x => x.AmountDue);
            TotalOwedText.Text = $"Rp {totalOwed:N0}";
            TotalPaidText.Text = $"Rp {tagihanList.Sum(x => x.AmountPaid):N0}";
            StudentBillTotals.Visibility = Visibility.Visible;

            EmptyText.Text = "Belum ada tagihan.";
            EmptyText.Visibility = tagihanList.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private Siswa FindSiswaByNis(string nis) =>
            string.IsNullOrEmpty(nis)
                ? null
                : DataStore.Instance.Users.OfType<Siswa>()
                    .FirstOrDefault(s => s.KelasId == _kelasId && s.Nis == nis);

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Refresh();

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshTransaksi();

        private void ClearDateFilter_Click(object sender, RoutedEventArgs e)
        {
            DateFilterPicker.SelectedDate = null;
            RefreshTransaksi();
        }

        private void TambahButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahTransaksiDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void MarkRefundPaid_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara && sender is FrameworkElement element && element.Tag is Transaksi transaksi)
            {
                if (MessageBox.Show(
                        $"Selesaikan refund sebesar Rp {transaksi.Amount:N0}?\n\nSaldo kas akan berkurang sebesar nominal ini dan status refund berubah menjadi Lunas.",
                        "Konfirmasi penyelesaian refund", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                ((Bendahara)_currentUser).TandaiRefundDibayar(transaksi.Id);
                RefreshTransaksi();
            }
        }

        private bool CanEditTransactionAmount(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara || transaksi.IsRefund)
                return false;

            if (transaksi.Type == JenisTransaksi.Keluar) return true;
            if (transaksi.Type != JenisTransaksi.Masuk) return false;

            if (!transaksi.TagihanSiswaId.HasValue) return true;

            var tagihanSiswaId = transaksi.TagihanSiswaId.Value;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == tagihanSiswaId))
                return false;

            var entry = DataStore.Instance.TagihanSiswaList.FirstOrDefault(ts => ts.Id == tagihanSiswaId);
            var tagihan = entry == null ? null : DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == entry.TagihanId);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == tagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private bool CanDeleteTransaction(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara) return false;
            if (transaksi.Type != JenisTransaksi.Masuk || !transaksi.TagihanSiswaId.HasValue) return true;

            var tagihanSiswaId = transaksi.TagihanSiswaId.Value;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == tagihanSiswaId))
                return false;

            var entry = DataStore.Instance.TagihanSiswaList.FirstOrDefault(ts => ts.Id == tagihanSiswaId);
            var tagihan = entry == null ? null : DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == entry.TagihanId);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == tagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private void EditTransactionAmount_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara bendahara && sender is FrameworkElement element && element.Tag is Transaksi transaksi)
            {
                _mainWindow.ShowModal(new EditPembayaranDialog(bendahara, transaksi, _mainWindow, Refresh));
            }
        }

        private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara || sender is not FrameworkElement element || element.Tag is not Transaksi transaksi)
                return;

            var result = MessageBox.Show(
                $"Hapus transaksi ini sebesar Rp {transaksi.Amount:N0}? Total kas akan disesuaikan.",
                "Hapus transaksi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                bendahara.HapusTransaksi(transaksi.Id);
                Refresh();
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(exception.Message, "Transaksi tidak dapat dihapus", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowTransactionActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not TransaksiDisplayItem item)
                return;

            bool togglingCurrent = ReferenceEquals(_pendingTransactionMenuToggle, button) ||
                ReferenceEquals(_openTransactionMenu?.PlacementTarget, button);
            _pendingTransactionMenuToggle = null;
            if (togglingCurrent)
            {
                if (_openTransactionMenu != null)
                    _openTransactionMenu.IsOpen = false;
                _openTransactionMenu = null;
                return;
            }

            if (_openTransactionMenu?.IsOpen == true)
                _openTransactionMenu.IsOpen = false;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("PopupActionMenu"),
                Placement = PlacementMode.Bottom
            };
            if (item.CanEditAmount)
                menu.Items.Add(CreateTransactionMenuItem("Ubah nominal", item.Transaksi, EditTransactionAmount_Click));
            if (item.CanDelete)
                menu.Items.Add(CreateTransactionMenuItem("Hapus", item.Transaksi, DeleteTransaction_Click));

            menu.PlacementTarget = button;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_pendingTransactionMenuToggle, button)) return;
                if (ReferenceEquals(_openTransactionMenu, menu))
                    _openTransactionMenu = null;
            };
            _openTransactionMenu = menu;
            menu.IsOpen = menu.Items.Count > 0;
        }

        private void TransactionMenuButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _pendingTransactionMenuToggle = sender is Button button &&
                ReferenceEquals(_openTransactionMenu?.PlacementTarget, button) ? button : null;
        }

        private static MenuItem CreateTransactionMenuItem(string header, Transaksi transaksi, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = transaksi };
            item.Click += onClick;
            return item;
        }

        private void TagihanPaymentRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is Tagihan tagihan)
            {
                _mainWindow.NavigateTo(new TagihanPage(_currentUser, _mainWindow, tagihan.Id));
            }
        }
    }
}
