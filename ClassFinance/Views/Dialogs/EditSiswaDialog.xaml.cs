using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassFinance.Models;
using ClassFinance.Services;

namespace ClassFinance.Views.Dialogs
{
    public partial class EditSiswaDialog : UserControl
    {
        private readonly Siswa _siswa;
        private readonly MainWindow _mainWindow;
        private readonly Action _onSaved;

        public EditSiswaDialog(Siswa siswa, MainWindow mainWindow, Action onSaved)
        {
            InitializeComponent();
            _siswa = siswa;
            _mainWindow = mainWindow;
            _onSaved = onSaved;
            NameBox.Text = siswa.Name ?? string.Empty;
            NisBox.Text = siswa.Nis ?? string.Empty;
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

            if (DataStore.Instance.Users.OfType<Siswa>().Any(s => s.Id != _siswa.Id &&
                string.Equals(s.Nis, nis, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorText.Text = "NIS sudah digunakan siswa lain.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            _siswa.Name = name;
            _siswa.Nis = nis;
            _siswa.Username = $"siswa-{nis}";
            DataStore.Instance.SaveChanges();
            _onSaved?.Invoke();
            _mainWindow.CloseModal();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
