using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public partial class TambahSiswaDialog : UserControl
    {
        private readonly int _kelasId;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public TambahSiswaDialog(int kelasId, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _kelasId = kelasId;
            _mainWindow = mainWindow;
            _onSaved = onSaved;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var nis = NisBox.Text.Trim();
            var username = UsernameBox.Text.Trim();
            var password = PasswordBoxInput.Password;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorText.Text = "Nama, username dan password wajib diisi.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (DataStore.Instance.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorText.Text = "Username sudah digunakan.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var siswa = new Siswa
            {
                Id = DataStore.Instance.NextUserId(),
                Name = name,
                Nis = nis,
                Username = username,
                PasswordHash = password,
                KelasId = _kelasId
            };
            DataStore.Instance.Users.Add(siswa);

            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.CloseModal();
        }
    }
}
