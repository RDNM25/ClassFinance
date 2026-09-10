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

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(nis))
            {
                ErrorText.Text = "Nama dan NIS wajib diisi.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (DataStore.Instance.Users.OfType<Siswa>().Any(s => s.Nis == nis))
            {
                ErrorText.Text = "NIS sudah terdaftar.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var siswa = new Siswa
            {
                Id = DataStore.Instance.NextUserId(),
                Name = name,
                Nis = nis,
                KelasId = _kelasId,
                // Students are roster entries only and never log in, but the base
                // User model still requires these -- filled with unusable placeholders
                // that are never shown or checked against anywhere.
                Username = $"siswa-{nis}",
                PasswordHash = Guid.NewGuid().ToString("N")
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
