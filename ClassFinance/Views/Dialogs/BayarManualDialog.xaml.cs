using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class BayarManualDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly Siswa _siswa;
        private readonly TagihanSiswa _tagihan;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public BayarManualDialog(Bendahara bendahara, Siswa siswa, TagihanSiswa tagihan, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _siswa = siswa;
            _tagihan = tagihan;
            _mainWindow = mainWindow;
            _onSaved = onSaved;

            SiswaNameText.Text = $"Siswa: {_siswa.Name}";
            AmountDueText.Text = $"Sisa Tagihan: Rp {_tagihan.AmountDue:N0}";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Masukkan nominal yang valid (> 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (amount > _tagihan.AmountDue)
            {
                // Overpayment logic: Close this and open the OpsiKelebihanBayar dialog
                _mainWindow.CloseModal();
                var overpaymentDialog = new OpsiKelebihanBayarDialog(_bendahara, _siswa, _tagihan, amount, _mainWindow, _onSaved);
                _mainWindow.ShowModal(overpaymentDialog);
            }
            else
            {
                // Normal or partial payment logic
                _bendahara.CatatPembayaran(_tagihan.Id, amount, "Tunai");
                _onSaved?.Invoke();
                _mainWindow.CloseModal();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }
    }
}
