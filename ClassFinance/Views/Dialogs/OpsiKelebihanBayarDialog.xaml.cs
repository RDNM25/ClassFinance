using System;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class OpsiKelebihanBayarDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly Siswa _siswa;
        private readonly TagihanSiswa _tagihan;
        private readonly decimal _totalBayar;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public OpsiKelebihanBayarDialog(Bendahara bendahara, Siswa siswa, TagihanSiswa tagihan, decimal totalBayar, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _siswa = siswa;
            _tagihan = tagihan;
            _totalBayar = totalBayar;
            _mainWindow = mainWindow;
            _onSaved = onSaved;

            decimal kelebihan = _totalBayar - _tagihan.AmountDue;
            InfoText.Text = $"Nominal bayar (Rp {_totalBayar:N0}) melebihi sisa tagihan. Terdapat kelebihan dana sebesar Rp {kelebihan:N0}.";
        }

        private void Process_Click(object sender, RoutedEventArgs e)
        {
            decimal kelebihan = _totalBayar - _tagihan.AmountDue;

            // Add that result (amountForKas) to the total kas by recording the payment for the exact due amount
            _bendahara.CatatPembayaran(_tagihan.Id, _totalBayar, "Tunai");

            // Handle the overpayment based on user selection
            if (SimpanRadio.IsChecked == true)
            {
                _bendahara.SimpanKelebihan(_siswa.Id, kelebihan);
            }
            else if (KembalikanRadio.IsChecked == true)
            {
                _bendahara.KembalikanDana(_siswa.KelasId, _siswa.Id, kelebihan);
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