using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassFinance.Models;
using ClassFinance.Services;
using Microsoft.Win32;

namespace ClassFinance.Views.Dialogs
{
    public partial class ProfileSettingsDialog : UserControl
    {
        private readonly User _user;
        private readonly MainWindow _mainWindow;
        private string _selectedPhotoPath;

        public ProfileSettingsDialog(User user, MainWindow mainWindow)
        {
            InitializeComponent();
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            UsernameBox.Text = user.Username ?? string.Empty;
            ProfileNameText.Text = string.IsNullOrWhiteSpace(user.Name) ? user.Username : user.Name;
            ProfileUsernameText.Text = $"{user.Username} · {GetRoleLabel(user.Role)}";
            ThemeComboBox.SelectedIndex = mainWindow.IsDarkMode ? 0 : 1;
            InitialsPreview.Text = MainWindow.GetProfileInitials(user.Name, user.Role);
            PhotoStatusText.Text = File.Exists(mainWindow.CurrentProfileImagePath)
                ? "Foto tersimpan di perangkat ini."
                : "Belum ada foto profil.";
            LoadPhotoPreview(mainWindow.CurrentProfileImagePath);
        }

        private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog
            {
                Title = "Pilih foto profil",
                Filter = "File gambar|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Semua file|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (picker.ShowDialog() != true)
                return;

            _selectedPhotoPath = picker.FileName;
            LoadPhotoPreview(_selectedPhotoPath);
            if (string.IsNullOrWhiteSpace(_selectedPhotoPath))
                return;

            SavePhotoButton.IsEnabled = true;
            SavePhotoButton.Visibility = Visibility.Visible;
            PhotoStatusText.Text = "Pratinjau foto dipilih. Simpan foto untuk menerapkannya.";
            PhotoStatusText.Foreground = FindStatusBrush("TextGray");
            PhotoStatusText.Visibility = Visibility.Visible;
        }

        private void LoadPhotoPreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                PhotoPreview.Source = null;
                PhotoPreview.Visibility = Visibility.Collapsed;
                InitialsPreview.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 256;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                PhotoPreview.Source = image;
                PhotoPreview.Visibility = Visibility.Visible;
                InitialsPreview.Visibility = Visibility.Collapsed;
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or FormatException)
            {
                PhotoPreview.Source = null;
                PhotoPreview.Visibility = Visibility.Collapsed;
                InitialsPreview.Visibility = Visibility.Visible;
                _selectedPhotoPath = null;
                SavePhotoButton.IsEnabled = false;
                SavePhotoButton.Visibility = Visibility.Collapsed;
                SetStatus(PhotoStatusText, "File foto tidak dapat dibuka. Pilih gambar PNG, JPG, BMP, atau GIF.", true);
            }
        }

        private void SavePhoto_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedPhotoPath))
            {
                SetStatus(PhotoStatusText, "Pilih foto terlebih dahulu.", true);
                return;
            }

            try
            {
                _mainWindow.SaveProfilePicture(_selectedPhotoPath);
                _selectedPhotoPath = null;
                SavePhotoButton.IsEnabled = false;
                SavePhotoButton.Visibility = Visibility.Collapsed;
                SetStatus(PhotoStatusText, "Foto profil berhasil diperbarui di perangkat ini.", false);
            }
            catch (Exception exception)
            {
                SetStatus(PhotoStatusText, exception.Message, true);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedIndex >= 0)
                _mainWindow.SetThemeMode(ThemeComboBox.SelectedIndex == 0);
        }

        private async void SaveUsername_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text.Trim();
            var currentPassword = UsernameCurrentPasswordBox.Password;

            if (username.Length < 3 || username.Length > 100)
            {
                SetStatus(UsernameStatusText, "Nama pengguna harus terdiri dari 3 sampai 100 karakter.", true);
                return;
            }
            if (string.Equals(username, _user.Username, StringComparison.Ordinal))
            {
                SetStatus(UsernameStatusText, "Nama pengguna tidak berubah.", false);
                return;
            }
            if (string.IsNullOrEmpty(currentPassword))
            {
                SetStatus(UsernameStatusText, "Masukkan kata sandi saat ini untuk mengubah nama pengguna.", true);
                return;
            }

            SaveUsernameButton.IsEnabled = false;
            SaveUsernameButton.Content = "Menyimpan...";
            try
            {
                var updatedUser = await KasMateApiClient.Instance.UpdateProfileAsync(
                    currentPassword, username, null);
                _user.Username = updatedUser.Username;
                _mainWindow.ApplyProfileUsername(updatedUser.Username);
                ProfileUsernameText.Text = $"{updatedUser.Username} · {GetRoleLabel(_user.Role)}";
                UsernameCurrentPasswordBox.Clear();
                SetStatus(UsernameStatusText, "Nama pengguna berhasil diperbarui.", false);
            }
            catch (Exception exception)
            {
                SetStatus(UsernameStatusText, exception.Message, true);
            }
            finally
            {
                SaveUsernameButton.IsEnabled = true;
                SaveUsernameButton.Content = "Simpan nama pengguna";
            }
        }

        private async void SavePassword_Click(object sender, RoutedEventArgs e)
        {
            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(currentPassword))
            {
                SetStatus(PasswordStatusText, "Masukkan kata sandi saat ini.", true);
                return;
            }
            if (newPassword.Length < 8)
            {
                SetStatus(PasswordStatusText, "Kata sandi baru harus terdiri dari minimal 8 karakter.", true);
                return;
            }
            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                SetStatus(PasswordStatusText, "Konfirmasi kata sandi baru tidak cocok.", true);
                return;
            }

            SavePasswordButton.IsEnabled = false;
            SavePasswordButton.Content = "Menyimpan...";
            try
            {
                var updatedUser = await KasMateApiClient.Instance.UpdateProfileAsync(
                    currentPassword, _user.Username, newPassword);
                _user.Username = updatedUser.Username;
                _mainWindow.ApplyProfileUsername(updatedUser.Username);
                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
                SetStatus(PasswordStatusText, "Kata sandi berhasil diperbarui.", false);
            }
            catch (Exception exception)
            {
                SetStatus(PasswordStatusText, exception.Message, true);
            }
            finally
            {
                SavePasswordButton.IsEnabled = true;
                SavePasswordButton.Content = "Ubah kata sandi";
            }
        }

        private void SetStatus(TextBlock target, string message, bool isError)
        {
            target.Text = message;
            target.Foreground = FindStatusBrush(isError ? "AccentText" : "TextGray");
            target.Visibility = Visibility.Visible;
        }

        private Brush FindStatusBrush(string resourceKey) =>
            TryFindResource(resourceKey) as Brush ?? SystemColors.ControlTextBrush;

        private static string GetRoleLabel(Role role) => role switch
        {
            Role.Bendahara => "Bendahara",
            Role.WaliKelas => "Wali kelas",
            _ => "Siswa"
        };

        private void Cancel_Click(object sender, RoutedEventArgs e) => _mainWindow.CloseModal();
    }
}
