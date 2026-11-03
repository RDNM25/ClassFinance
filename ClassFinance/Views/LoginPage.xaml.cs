using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassFinance.Services;

namespace ClassFinance.Views
{
    public partial class LoginPage : Page
    {
        private readonly MainWindow _mainWindow;
        private bool _isPasswordVisible;
        private bool _isSyncingPassword;

        public LoginPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            PasswordBoxInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
            VisiblePasswordBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
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
            UsernameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e) => DoLogin();

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

        private void DoLogin()
        {
            var password = _isPasswordVisible ? VisiblePasswordBox.Text : PasswordBoxInput.Password;
            var user = AuthService.Login(UsernameBox.Text.Trim(), password);
            if (user == null)
            {
                ErrorText.Text = "Username atau password salah.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            _mainWindow.ShowShell(user);
        }
    }
}
