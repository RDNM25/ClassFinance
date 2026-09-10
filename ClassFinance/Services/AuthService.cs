using System.Linq;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    public static class AuthService
    {
        public static User CurrentUser { get; private set; }

        public static User Login(string username, string password)
        {
            var user = DataStore.Instance.Users
                .FirstOrDefault(u => u.Username.Equals(username, System.StringComparison.OrdinalIgnoreCase)
                                      && u.VerifyPassword(password));
            CurrentUser = user;
            return user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}
