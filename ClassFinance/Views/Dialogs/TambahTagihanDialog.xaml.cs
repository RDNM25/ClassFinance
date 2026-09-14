using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class TambahTagihanDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly int _kelasId;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public TambahTagihanDialog(Bendahara bendahara, int kelasId, MainWindow mainWindow, Action onSaved)
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
            var name = NameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                ErrorText.Text = "Isi nama tagihan dan jumlah yang valid (angka > 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            // DatePicker only stores a date (no time-of-day -- WPF normalizes it to
            // midnight internally), so combine whichever day was picked with the actual
            // current time, otherwise a tagihan created "today" would sort as if it
            // happened at 00:00, below anything else from today that has a real time.
            var pickedDate = (DatePickerInput.SelectedDate ?? DateTime.Now).Date;
            var createdDate = pickedDate + DateTime.Now.TimeOfDay;

            _bendahara.BuatTagihan(_kelasId, name, amount, createdDate);

            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }
    }
}
