using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;

namespace ClassFinance.Views.Dialogs
{
    public partial class EditTagihanDialog : UserControl
    {
        private readonly Bendahara _bendahara;
        private readonly Tagihan _tagihan;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public EditTagihanDialog(Bendahara bendahara, Tagihan tagihan, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _bendahara = bendahara;
            _tagihan = tagihan;
            _mainWindow = mainWindow;
            _onSaved = onSaved;
            NameBox.Text = tagihan.Name;
            AmountBox.Text = tagihan.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) ||
                !decimal.TryParse(AmountBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Isi nama dan jumlah tagihan yang valid.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                _bendahara.UbahTagihan(_tagihan.Id, NameBox.Text, amount);
                _onSaved?.Invoke();
                _mainWindow.CloseModal();
            }
            catch (InvalidOperationException exception)
            {
                ErrorText.Text = exception.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
