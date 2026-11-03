using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ClassFinance.Models;
using ClassFinance.Services;
using ClassFinance.Views.Dialogs;

namespace ClassFinance.Views
{
    public class TagihanSiswaDisplay
    {
        public TagihanSiswa Entry { get; set; }
        public string SiswaName { get; set; }
        public bool CanMakePayment { get; set; }
        public bool HasPendingRefund { get; set; }
        public List<TagihanTransactionDisplay> Transactions { get; set; }
    }

    public class TagihanTransactionDisplay
    {
        public Transaksi Transaction { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShowActions => CanEdit || CanDelete;
    }

    public class TagihanDisplayItem
    {
        public Tagihan Tagihan { get; set; }
        public bool IsFullyPaid { get; set; }
        public bool CanManage { get; set; }
    }

    public partial class TagihanPage : Page
    {
        private readonly User _currentUser;
        private readonly MainWindow _mainWindow;
        private readonly int _kelasId;
        private Tagihan _selectedTagihan;
        private int? _initialTagihanId; 
        private ContextMenu _openActionMenu;
        private Button _pendingActionMenuToggle;
        private bool _responsiveLayoutInitialized;
        private bool _isCompactLayout;
        private bool _showCompactDetails;

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

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0)
                return;

            var useCompactLayout = e.NewSize.Width < 840;
            if (_responsiveLayoutInitialized && useCompactLayout == _isCompactLayout)
                return;

            _responsiveLayoutInitialized = true;
            _isCompactLayout = useCompactLayout;
            TagihanLayoutGrid.ColumnDefinitions.Clear();
            TagihanLayoutGrid.RowDefinitions.Clear();

            if (useCompactLayout)
            {
                TagihanLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TagihanLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(BillListPanel, 0);
                Grid.SetColumnSpan(BillListPanel, 1);
                Grid.SetRow(BillListPanel, 0);
                Grid.SetColumn(PaymentDetailsPanel, 0);
                Grid.SetRow(PaymentDetailsPanel, 0);
                PaymentDetailsPanel.Margin = new Thickness(0);
            }
            else
            {
                TagihanLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                TagihanLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                TagihanLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
                TagihanLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(BillListPanel, 0);
                Grid.SetColumnSpan(BillListPanel, 2);
                Grid.SetRow(BillListPanel, 0);
                Grid.SetColumn(PaymentDetailsPanel, 2);
                Grid.SetRow(PaymentDetailsPanel, 0);
                PaymentDetailsPanel.Margin = new Thickness(30, 0, 10, 0);
            }

            UpdateCompactPaneVisibility();
        }

        private void UpdateCompactPaneVisibility()
        {
            if (!_responsiveLayoutInitialized)
                return;

            var showDetails = _selectedTagihan != null && _showCompactDetails;
            BackToTagihanListButton.Visibility = _isCompactLayout && showDetails
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_isCompactLayout)
            {
                BillListPanel.Visibility = showDetails ? Visibility.Collapsed : Visibility.Visible;
                PaymentDetailsPanel.Visibility = showDetails ? Visibility.Visible : Visibility.Collapsed;
                DetailTitle.HorizontalAlignment = HorizontalAlignment.Left;
                DetailTitle.TextAlignment = TextAlignment.Left;
                DetailTitle.Margin = new Thickness(0, 12, 0, 0);
                StatusFilterComboBox.HorizontalAlignment = HorizontalAlignment.Left;
                StatusFilterComboBox.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                BillListPanel.Visibility = Visibility.Visible;
                PaymentDetailsPanel.Visibility = Visibility.Visible;
                DetailTitle.HorizontalAlignment = HorizontalAlignment.Right;
                DetailTitle.TextAlignment = TextAlignment.Right;
                DetailTitle.Margin = new Thickness(27, 19, 18, 0);
                StatusFilterComboBox.HorizontalAlignment = HorizontalAlignment.Right;
                StatusFilterComboBox.Margin = new Thickness(27, 12, 0, 0);
            }
        }

        private void BackToTagihanList_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCompactLayout)
                return;

            _showCompactDetails = false;
            UpdateCompactPaneVisibility();
            TagihanItems.Focus();
        }

        private void TagihanItems_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isCompactLayout || _showCompactDetails || _selectedTagihan == null ||
                ItemsControl.ContainerFromElement(TagihanItems, e.OriginalSource as DependencyObject) is not ListBoxItem)
                return;

            _showCompactDetails = true;
            DetailTitle.Text = $"Status Pembayaran \u2014 {_selectedTagihan.Name}";
            ApplyFilter();
        }

        private void Refresh()
        {
            if (TagihanItems == null) return;

            var preserveCompactPane = _showCompactDetails;

            var query = DataStore.Instance.TagihanList.Where(t => t.KelasId == _kelasId)
                .OrderByDescending(t => t.Id)
                .Select(t =>
                {
                    var entries = DataStore.Instance.TagihanSiswaList.Where(ts => ts.TagihanId == t.Id).ToList();
                    return new TagihanDisplayItem
                    {
                        Tagihan = t,
                        IsFullyPaid = entries.Any() && entries.All(ts => ts.Status == StatusTagihan.Lunas),
                        CanManage = _currentUser is Bendahara
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
            EmptyText.Text = DataStore.Instance.TagihanList.Any(t => t.KelasId == _kelasId)
                ? "Tidak ada tagihan yang sesuai dengan filter."
                : "Belum ada tagihan.";

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
                var selected = tagihanList.FirstOrDefault(x => x.Tagihan.Id == _selectedTagihan.Id);
                if (selected == null)
                {
                    _selectedTagihan = null;
                    _showCompactDetails = false;
                    TagihanItems.SelectedItem = null;
                    DetailTitle.Text = "Status Pembayaran";
                    ApplyFilter();
                }
                else
                {
                    TagihanItems.SelectedItem = selected;
                    _showCompactDetails = preserveCompactPane;
                    ApplyFilter();
                }
            }
            else
                ApplyFilter();

            UpdateCompactPaneVisibility();
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
                _showCompactDetails = true;
                DetailTitle.Text = $"Status Pembayaran \u2014 {_selectedTagihan.Name}";
                ApplyFilter();
            }
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The initial IsSelected item can raise this event while InitializeComponent
            // is still building the rest of the page, before DetailItems exists.
            if (DetailItems == null) return;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_selectedTagihan == null)
            {
                DetailItems.ItemsSource = null;
                DetailScrollViewer.Visibility = Visibility.Collapsed;
                NoSelectedTagihanText.Visibility = Visibility.Visible;
                NoMatchingPaymentsText.Visibility = Visibility.Collapsed;
                StatusFilterComboBox.Visibility = Visibility.Collapsed;
                UpdateCompactPaneVisibility();
                return;
            }

            var query = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.TagihanId == _selectedTagihan.Id)
                .Select(ts => new TagihanSiswaDisplay
                {
                    Entry = ts,
                    SiswaName = DataStore.Instance.Users.OfType<Siswa>().FirstOrDefault(s => s.Id == ts.SiswaId)?.Name ?? "?",
                    CanMakePayment = ts.Status != StatusTagihan.Lunas && ts.Status != StatusTagihan.Proses,
                    HasPendingRefund = _currentUser is Bendahara && DataStore.Instance.TransaksiList.Any(t =>
                        t.TagihanSiswaId == ts.Id && t.IsPendingRefund),
                    Transactions = DataStore.Instance.TransaksiList
                        .Where(t => t.TagihanSiswaId == ts.Id)
                        .OrderByDescending(t => t.Date)
                        .Select(t => new TagihanTransactionDisplay
                        {
                            Transaction = t,
                            CanEdit = CanEditTransaction(t),
                            CanDelete = CanDeleteTransaction(t)
                        })
                        .ToList()
                });

            if (StatusFilterComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag)
            {
                query = filterTag switch
                {
                    "Lunas" => query.Where(x => x.Entry.Status == StatusTagihan.Lunas),
                    "BelumLunas" => query.Where(x => x.Entry.Status == StatusTagihan.BelumBayar),
                    "Sebagian" => query.Where(x => x.Entry.Status == StatusTagihan.Sebagian),
                    "Proses" => query.Where(x => x.Entry.Status == StatusTagihan.Proses),
                    _ => query
                };
            }

            var details = query.ToList();
            DetailItems.ItemsSource = details;
            if (details.Any())
            {
                DetailScrollViewer.Visibility = Visibility.Visible;
                DetailScrollViewer.Opacity = 0;
                DetailScrollViewer.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    }, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                DetailScrollViewer.BeginAnimation(UIElement.OpacityProperty, null);
                DetailScrollViewer.Opacity = 1;
                DetailScrollViewer.Visibility = Visibility.Collapsed;
            }
            NoSelectedTagihanText.Visibility = Visibility.Collapsed;
            NoMatchingPaymentsText.Visibility = details.Any() ? Visibility.Collapsed : Visibility.Visible;
            StatusFilterComboBox.Visibility = Visibility.Visible;
            UpdateCompactPaneVisibility();
        }

        private void BuatTagihanButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TambahTagihanDialog((Bendahara)_currentUser, _kelasId, _mainWindow, Refresh);
            _mainWindow.ShowModal(dialog);
        }

        private void EditTagihan_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara bendahara && sender is FrameworkElement element && element.Tag is TagihanDisplayItem display)
                _mainWindow.ShowModal(new EditTagihanDialog(bendahara, display.Tagihan, _mainWindow, Refresh));
        }

        private void DeleteTagihan_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara || sender is not FrameworkElement element || element.Tag is not TagihanDisplayItem display)
                return;

            if (MessageBox.Show($"Hapus tagihan '{display.Tagihan.Name}'? Tagihan yang memiliki transaksi pembayaran tidak dapat dihapus.",
                    "Hapus tagihan", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                bendahara.HapusTagihan(display.Tagihan.Id);
                _selectedTagihan = null;
                TagihanItems.SelectedItem = null;
                DetailItems.ItemsSource = null;
                DetailTitle.Text = "Status pembayaran";
                Refresh();
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(exception.Message, "Tagihan tidak dapat dihapus", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TandaiLunas_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
            {
                MessageBox.Show("Hanya Bendahara yang dapat mencatat pembayaran.", "Akses ditolak");
                return;
            }

            if (sender is Button btn && btn.Tag is TagihanSiswaDisplay display &&
                display.Entry.Status != StatusTagihan.Lunas && display.Entry.Status != StatusTagihan.Proses)
            {
                if (MessageBox.Show(
                        $"Catat pembayaran tunai sebesar Rp {display.Entry.AmountDue:N0} untuk {display.SiswaName}?\n\nSaldo kas akan bertambah sebesar nominal ini.",
                        "Konfirmasi pembayaran", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                bendahara.CatatPembayaran(display.Entry.Id, display.Entry.AmountDue, "Tunai");
                Refresh();
            }
        }

        private void TandaiRefundLunas_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara ||
                sender is not Button button || button.Tag is not TagihanSiswaDisplay display)
                return;

            var pendingRefunds = DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == display.Entry.Id && t.IsPendingRefund)
                .ToList();

            if (pendingRefunds.Count == 0)
            {
                Refresh();
                return;
            }

            var totalRefund = pendingRefunds.Sum(t => t.Amount);
            if (MessageBox.Show(
                    $"Selesaikan {pendingRefunds.Count} refund untuk {display.SiswaName} dengan total Rp {totalRefund:N0}?\n\nSaldo kas akan berkurang sebesar jumlah tersebut dan status refund berubah menjadi Lunas.",
                    "Konfirmasi penyelesaian refund", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            foreach (var refund in pendingRefunds)
                bendahara.TandaiRefundDibayar(refund.Id);

            Refresh();
        }

        private bool CanEditTransaction(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara || transaksi.Type != JenisTransaksi.Masuk || transaksi.IsRefund)
                return false;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == transaksi.TagihanSiswaId))
                return false;

            var tagihan = DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == _selectedTagihan?.Id);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == transaksi.TagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private bool CanDeleteTransaction(Transaksi transaksi)
        {
            if (_currentUser is not Bendahara) return false;
            if (transaksi.Type != JenisTransaksi.Masuk) return true;
            if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == transaksi.TagihanSiswaId))
                return false;

            var tagihan = DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == _selectedTagihan?.Id);
            return tagihan != null && DataStore.Instance.TransaksiList
                .Where(t => t.TagihanSiswaId == transaksi.TagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                .Sum(t => t.Amount) <= tagihan.Amount;
        }

        private void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is Bendahara bendahara && sender is FrameworkElement element && element.Tag is Transaksi transaksi)
                _mainWindow.ShowModal(new EditPembayaranDialog(bendahara, transaksi, _mainWindow, Refresh));
        }

        private void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara || sender is not FrameworkElement element || element.Tag is not Transaksi transaksi)
                return;

            if (MessageBox.Show($"Hapus transaksi ini sebesar Rp {transaksi.Amount:N0}? Total kas akan disesuaikan.",
                    "Hapus transaksi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

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

        private void ShowTagihanActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not TagihanDisplayItem bill)
                return;

            if (CloseOpenMenuIfSameButton(button))
            {
                return;
            }

            var menu = CreatePopupActionMenu();
            menu.Items.Add(CreateBillMenuItem("Ubah tagihan", bill, EditTagihan_Click));
            menu.Items.Add(CreateBillMenuItem("Hapus tagihan", bill, DeleteTagihan_Click));
            OpenActionMenu(button, menu);
        }

        private void ShowPaymentActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not TagihanTransactionDisplay payment)
                return;

            if (CloseOpenMenuIfSameButton(button))
            {
                return;
            }

            var menu = CreatePopupActionMenu();
            if (payment.CanEdit)
                menu.Items.Add(CreatePaymentMenuItem("Ubah nominal", payment.Transaction, EditPayment_Click));
            if (payment.CanDelete)
                menu.Items.Add(CreatePaymentMenuItem("Hapus transaksi", payment.Transaction, DeletePayment_Click));
            OpenActionMenu(button, menu);
        }

        private static MenuItem CreateBillMenuItem(string header, TagihanDisplayItem bill, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = bill };
            item.Click += onClick;
            return item;
        }

        private static MenuItem CreatePaymentMenuItem(string header, Transaksi transaction, RoutedEventHandler onClick)
        {
            var item = new MenuItem { Header = header, Tag = transaction };
            item.Click += onClick;
            return item;
        }

        private ContextMenu CreatePopupActionMenu() => new()
        {
            Style = (Style)FindResource("PopupActionMenu"),
            Placement = PlacementMode.Bottom
        };

        private bool CloseOpenMenuIfSameButton(Button button)
        {
            bool togglingCurrent = ReferenceEquals(_pendingActionMenuToggle, button) ||
                ReferenceEquals(_openActionMenu?.PlacementTarget, button);
            _pendingActionMenuToggle = null;
            if (!togglingCurrent)
                return false;

            if (_openActionMenu != null)
                _openActionMenu.IsOpen = false;
            _openActionMenu = null;
            return true;
        }

        private void ActionMenuButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _pendingActionMenuToggle = sender is Button button &&
                ReferenceEquals(_openActionMenu?.PlacementTarget, button) ? button : null;
        }

        private void OpenActionMenu(Button button, ContextMenu menu)
        {
            if (_openActionMenu?.IsOpen == true)
                _openActionMenu.IsOpen = false;

            menu.PlacementTarget = button;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_pendingActionMenuToggle, button)) return;
                if (ReferenceEquals(_openActionMenu, menu))
                    _openActionMenu = null;
            };
            _openActionMenu = menu;
            menu.IsOpen = menu.Items.Count > 0;
        }

        private void BayarManual_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser is not Bendahara bendahara)
            {
                MessageBox.Show("Hanya Bendahara yang dapat mencatat pembayaran.", "Akses ditolak");
                return;
            }

            if (sender is Button btn && btn.Tag is TagihanSiswaDisplay display &&
                display.Entry.Status != StatusTagihan.Lunas && display.Entry.Status != StatusTagihan.Proses)
            {
                var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == display.Entry.SiswaId);
                var dialog = new BayarManualDialog(bendahara, siswa, display.Entry, _mainWindow, onSaved: () => Refresh());
                _mainWindow.ShowModal(dialog);
            }
        }
    }
}
