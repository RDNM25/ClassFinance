using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class EditPembayaranDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly Transaksi _transaksi;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public EditPembayaranDialog(Bendahara bendahara, Transaksi transaksi, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _transaksi = transaksi;
            _mainWindow = mainWindow;
            _onSaved = onSaved;
            AmountBox.Text = transaksi.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Masukkan nominal yang valid (angka > 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                await _bendahara.UbahNominalTransaksiAsync(_transaksi.Id, amount);
                _onSaved?.Invoke();
                _mainWindow.CloseModal();
            }
            catch (Exception exception)
            {
                ErrorText.Text = exception.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
