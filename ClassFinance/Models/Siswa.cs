using ClassFinance.Services;
using System.Collections.Generic;
using System.Linq;

namespace ClassFinance.Models
{
    /// <summary>Student account. Read-only view of their own tagihan and class cash.</summary>
    public class Siswa : User
    {
        public string Nis { get; set; }
        public int KelasId { get; set; }

        // Tracks excess payments saved for future bills
        public decimal SaldoTitipan { get; set; }

        public Siswa()
        {
            Role = Role.Siswa;
            SaldoTitipan = 0; 
        }

        public override string GetDashboardTitle() => "Dashboard Siswa";

        public List<TagihanSiswa> LihatTagihan() =>
            DataStore.Instance.TagihanSiswaList.Where(t => t.SiswaId == Id).ToList();

        public List<Transaksi> LihatRiwayatPembayaran() =>
            DataStore.Instance.TransaksiList
                .Where(t => t.SiswaId == Id)
                .OrderByDescending(t => t.Date)
                .ToList();

        public decimal LihatSaldoKas() =>
            DataStore.Instance.KelasList.First(k => k.Id == KelasId).HitungSaldo();
    }
}