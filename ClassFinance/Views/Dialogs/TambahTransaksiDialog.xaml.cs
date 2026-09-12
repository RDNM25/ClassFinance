using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

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
            // DatePicker only stores a date (no time-of-day -- WPF normalizes it to midnight
            // internally), so combine whichever day was picked with the actual current time.
            // Otherwise every new entry would sort as "00:00 today", landing below any other
            // same-day transaction that has a real time, instead of at the top where a
            // just-added entry belongs.
            var pickedDate = (DatePickerInput.SelectedDate ?? DateTime.Now).Date;
            var dateValue = pickedDate + DateTime.Now.TimeOfDay;

            if (string.IsNullOrWhiteSpace(description) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                ErrorText.Text = "Isi keterangan dan jumlah yang valid (angka > 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (MasukRadio.IsChecked == true)
                _bendahara.TambahPemasukan(_kelasId, description, amount, dateValue);
            else
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
