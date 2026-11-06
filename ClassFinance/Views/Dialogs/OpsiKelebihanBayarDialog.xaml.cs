using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class OpsiKelebihanBayarDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly Siswa _siswa;
        private readonly TagihanSiswa _tagihan;
        private readonly decimal _totalBayar;
        private readonly DateTime _paymentDate;
        private readonly MetodeTransaksi _metode;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public OpsiKelebihanBayarDialog(Bendahara bendahara, Siswa siswa, TagihanSiswa tagihan,
            decimal totalBayar, DateTime paymentDate, MetodeTransaksi metode, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _siswa = siswa;
            _tagihan = tagihan;
            _totalBayar = totalBayar;
            _paymentDate = paymentDate;
            _metode = metode;
            _mainWindow = mainWindow;
            _onSaved = onSaved;

            decimal kelebihan = _totalBayar - _tagihan.AmountDue;
            InfoText.Text = $"Nominal bayar (Rp {_totalBayar:N0}) melebihi sisa tagihan. Terdapat kelebihan dana sebesar Rp {kelebihan:N0}.";
        }

        private async void Process_Click(object sender, RoutedEventArgs e)
        {
            decimal kelebihan = _totalBayar - _tagihan.AmountDue;
            try
            {
                // Record the received payment, then handle its excess according to the selected option.
                await _bendahara.CatatPembayaranAsync(_tagihan.Id, _totalBayar, _metode, date: _paymentDate);

                if (SimpanRadio.IsChecked == true)
                {
                    await _bendahara.SimpanKelebihanAsync(_siswa.Id, kelebihan, _paymentDate, _metode);
                }
                else if (KembalikanRadio.IsChecked == true)
                {
                    await _bendahara.KembalikanDanaAsync(_siswa.KelasId, _siswa.Id, kelebihan, _tagihan.Id, _paymentDate, _metode);
                }
            }
            catch (Exception exception)
            {
                AppMessageBox.Show(_mainWindow, exception.Message, "Pembayaran tidak dapat disimpan", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }
    }
}
