using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public partial class JadwalTagihanMingguanDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly int _kelasId;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public JadwalTagihanMingguanDialog(
            Bendahara bendahara, int kelasId, MainWindow mainWindow, Action onSaved, JadwalTagihanMingguan schedule = null)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _kelasId = kelasId;
            _mainWindow = mainWindow;
            _onSaved = onSaved;

            StartDatePicker.SelectedDate = schedule?.StartsOn ?? JadwalTagihanMingguanService.MondayOf(DateTime.Today);
            if (schedule == null)
            {
                NameBox.Text = "Iuran Mingguan";
                AmountBox.Text = "";
            }
            else
            {
                NameBox.Text = schedule.Name;
                AmountBox.Text = schedule.Amount.ToString("0.##", CultureInfo.InvariantCulture);
                EndDatePicker.SelectedDate = schedule.EndsOn;
                ActiveCheckBox.IsChecked = schedule.IsActive;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Isi nama iuran dan jumlah yang valid (angka lebih dari 0).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var startsOn = StartDatePicker.SelectedDate ?? DateTime.Today;
            var endsOn = EndDatePicker.SelectedDate;
            if (endsOn.HasValue && endsOn.Value.Date < startsOn.Date)
            {
                ErrorText.Text = "Tanggal akhir tidak boleh sebelum tanggal mulai.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                _bendahara.SimpanJadwalTagihanMingguan(
                    _kelasId, NameBox.Text, amount, startsOn, endsOn, ActiveCheckBox.IsChecked == true);
                JadwalTagihanMingguanService.PastikanMingguTerbuat(_kelasId, DateTime.Today);
                _onSaved?.Invoke();
                _mainWindow.CloseModal();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                ErrorText.Text = exception.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
