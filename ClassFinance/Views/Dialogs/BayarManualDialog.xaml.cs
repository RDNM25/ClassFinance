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
            DatePickerInput.SelectedDate = DateTime.Now;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Masukkan nominal yang valid (> 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            // DatePicker only stores a date (no time-of-day), so combine whichever day
            // was picked with the actual current time -- otherwise a payment made
            // "today" would sort as if it happened at 00:00.
            var pickedDate = (DatePickerInput.SelectedDate ?? DateTime.Now).Date;
            var paymentDate = pickedDate + DateTime.Now.TimeOfDay;

            if (amount > _tagihan.AmountDue)
            {
                // Overpayment logic: Close this and open the OpsiKelebihanBayar dialog,
                // carrying the chosen date along so both dialogs record the same payment
                // at the same date instead of asking for it twice.
                _mainWindow.CloseModal();
                var overpaymentDialog = new OpsiKelebihanBayarDialog(_bendahara, _siswa, _tagihan, amount, paymentDate, _mainWindow, _onSaved);
                _mainWindow.ShowModal(overpaymentDialog);
            }
            else
            {
                // Normal or partial payment logic
                _bendahara.CatatPembayaran(_tagihan.Id, amount, "Tunai", date: paymentDate);
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
