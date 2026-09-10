using System;

namespace ClassFinance.Models
{
    /// <summary>
    /// Base class for every account type in the app (parent class in the OOP hierarchy).
    /// Bendahara, WaliKelas and Siswa all inherit from User.
    /// </summary>
    public abstract class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public Role Role { get; set; }

        public bool VerifyPassword(string password) => PasswordHash == password;

        /// <summary>Polymorphic title shown at the top of each role's dashboard.</summary>
        public abstract string GetDashboardTitle();
    }
}
