using ClassFinance.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassFinance.Models
{
    /// <summary>Student account. Read-only view of their own tagihan and class cash.</summary>
    public class Siswa : User
    {
        public string Nis { get; set; }
        public int KelasId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Tracks excess payments saved for future bills
        public decimal SaldoTitipan { get; set; }

        public Siswa()
        {
            Role = Role.Siswa;
            SaldoTitipan = 0; 
        }

        public override string GetDashboardTitle() => "Dashboard Siswa";
    }
}
