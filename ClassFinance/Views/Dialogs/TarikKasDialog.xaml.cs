using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public partial class TarikKasDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly int _kelasId;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public TarikKasDialog(Bendahara bendahara, int kelasId, MainWindow mainWindow, Action onSaved)
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

            if (string.IsNullOrWhiteSpace(description) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                ErrorText.Text = "Isi keterangan dan jumlah yang valid (angka > 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            //  BALANCE CHECK LOGIC 
            // Calculate current balance (HitungSaldo already excludes bookkeeping-only
            // entries like applying stored student balance to a bill, so this matches
            // what the rest of the app shows as the real cash total).
            var saldoSekarang = DataStore.Instance.KelasList.First(k => k.Id == _kelasId).HitungSaldo();

            // Prevent overdraft
            if (amount > saldoSekarang)
            {
                decimal kurangAmount = amount - saldoSekarang;
                ErrorText.Text = $"Uang kas tidak cukup, kurang Rp {kurangAmount:N0}";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            // -------------------------------

            // DatePicker only stores a date (no time-of-day), so combine whichever day
            // was picked with the actual current time -- otherwise a withdrawal made
            // "today" would sort as if it happened at 00:00.
            var pickedDate = (DatePickerInput.SelectedDate ?? DateTime.Now).Date;
            var dateValue = pickedDate + DateTime.Now.TimeOfDay;

            _bendahara.TambahPengeluaran(_kelasId, description, amount, dateValue);

            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }
    }
}
