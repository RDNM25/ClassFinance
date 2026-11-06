using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http;
using System.Threading.Tasks;
using ClassFinance.Services;

namespace ClassFinance.Views
{
    public partial class LoginPage : UserControl
    {
        private readonly MainWindow _mainWindow;
        private bool _isPasswordVisible;
        private bool _isSyncingPassword;
        private bool _isSigningIn;

        public LoginPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            PasswordBoxInput.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; await DoLoginAsync(); }
            };
            VisiblePasswordBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; await DoLoginAsync(); }
            };
            PasswordBoxInput.PasswordChanged += (s, e) =>
            {
                if (_isSyncingPassword)
                    return;

                _isSyncingPassword = true;
                VisiblePasswordBox.Text = PasswordBoxInput.Password;
                _isSyncingPassword = false;
            };
            VisiblePasswordBox.TextChanged += (s, e) =>
            {
                if (_isSyncingPassword)
                    return;

                _isSyncingPassword = true;
                PasswordBoxInput.Password = VisiblePasswordBox.Text;
                _isSyncingPassword = false;
            };
            UsernameBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; await DoLoginAsync(); }
            };
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e) => await DoLoginAsync();

        private void TogglePasswordVisibility_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TogglePasswordVisibility();
        }

        private void TogglePasswordVisibility_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                TogglePasswordVisibility();
                e.Handled = true;
            }
        }

        private void TogglePasswordVisibility()
        {
            _isPasswordVisible = !_isPasswordVisible;
            VisiblePasswordBox.Text = PasswordBoxInput.Password;
            PasswordBoxInput.Visibility = _isPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
            VisiblePasswordBox.Visibility = _isPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
            PasswordHiddenSlash.Visibility = _isPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
            TogglePasswordVisibilityButton.ToolTip = _isPasswordVisible ? "Hide password" : "Show password";

            if (_isPasswordVisible)
            {
                VisiblePasswordBox.Focus();
                VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
            }
            else
            {
                PasswordBoxInput.Focus();
            }
        }

        private async Task DoLoginAsync()
        {
            if (_isSigningIn)
                return;

            var password = _isPasswordVisible ? VisiblePasswordBox.Text : PasswordBoxInput.Password;
            _isSigningIn = true;
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Memeriksa akun...";
            ErrorText.Visibility = Visibility.Collapsed;
            try
            {
                var username = UsernameBox.Text.Trim();
                var user = await AuthService.LoginAsync(username, password);
                if (user == null)
                {
                    ErrorText.Text = "Username atau password salah.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                LoginButton.Content = "Memuat data...";
                await DataStore.Instance.InitializeAsync();
                await _mainWindow.ShowShellAsync(user);
            }
            catch (HttpRequestException)
            {
                ErrorText.Text = "Tidak dapat menghubungi backend KasMate. Pastikan Apache dan MySQL XAMPP aktif serta migrasi API sudah diimpor.";
                ErrorText.Visibility = Visibility.Visible;
            }
            catch (Exception exception)
            {
                ErrorText.Text = exception.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                _isSigningIn = false;
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Masuk";
            }
        }
    }
}
