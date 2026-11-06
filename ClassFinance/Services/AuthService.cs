using System;
using System.Threading.Tasks;
using ClassFinance.Models;

namespace ClassFinance.Services
{
    public static class AuthService
    {
        public static User CurrentUser { get; private set; }

        public static async Task<User> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                CurrentUser = null;
                return null;
            }

            var identity = await KasMateApiClient.Instance.LoginAsync(username.Trim(), password);
            if (identity == null) return CurrentUser = null;

            User user = identity.Role?.Replace("_", string.Empty).Equals("walikelas", StringComparison.OrdinalIgnoreCase) == true
                ? new WaliKelas { KelasId = identity.ClassId ?? 0 }
                : new Bendahara();
            user.Id = identity.Id;
            user.Name = identity.Name;
            user.Username = identity.Username;
            CurrentUser = user;
            return user;
        }

        public static void Logout()
        {
            _ = KasMateApiClient.Instance.LogoutAsync();
            CurrentUser = null;
        }
    }
}
