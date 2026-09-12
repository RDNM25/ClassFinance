using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public partial class TambahTransaksiDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly int _kelasId;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public TambahTransaksiDialog(Bendahara bendahara, int kelasId, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _kelasId = kelasId;
            _mainWindow = mainWindow;
            _onSaved = onSaved;
            DatePickerInput.SelectedDate = DateTime.Now;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var description = DescriptionBox.Text.Trim();
            var dateValue = DatePickerInput.SelectedDate ?? DateTime.Now;

            if (string.IsNullOrWhiteSpace(description) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                ErrorText.Text = "Isi keterangan dan jumlah yang valid (angka > 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            // Calculate current balance
            var kelas = DataStore.Instance.KelasList.First(k => k.Id == _kelasId);
            var riwayat = kelas.GetRiwayatTransaksi();
            var totalEarned = riwayat.Where(t => t.Type == JenisTransaksi.Masuk).Sum(t => t.Amount);
            var totalSpent = riwayat.Where(t => t.Type == JenisTransaksi.Keluar).Sum(t => t.Amount);
            var saldoSekarang = totalEarned - totalSpent;

            // Prevent overdraft
            if (KeluarRadio.IsChecked == true && amount > saldoSekarang)
            {
                ErrorText.Text = $"Uang kas tidak cukup, kurang Rp {amount - saldoSekarang:N0}";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            // Hide error if valid
            ErrorText.Visibility = Visibility.Collapsed;

            if (KeluarRadio.IsChecked == true)
            {
                _bendahara.TambahPengeluaran(_kelasId, description, amount, dateValue);
            }
            else
            {
            _bendahara.TambahPemasukan(_kelasId, description, amount, dateValue);
            }

            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }    }
}