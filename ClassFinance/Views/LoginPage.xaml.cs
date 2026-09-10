using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClassFinance.Services;

namespace ClassFinance.Views
{
    public partial class LoginPage : Page
    {
        private readonly MainWindow _mainWindow;

        public LoginPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            PasswordBoxInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
            UsernameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e) => DoLogin();

        private void DoLogin()
        {
            var user = AuthService.Login(UsernameBox.Text.Trim(), PasswordBoxInput.Password);
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
