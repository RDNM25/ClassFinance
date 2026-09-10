using System.Collections.Generic;
using System.Linq;
using ClassFinance.Services;

namespace ClassFinance.Models
{
    /// <summary>Homeroom teacher account. Oversight access: can view everything and manage the student roster.</summary>
    public class WaliKelas : User
    {
        public int KelasId { get; set; }

        public WaliKelas()
        {
            Role = Role.WaliKelas;
        }

        public override string GetDashboardTitle() => "Dashboard Wali Kelas";

        public decimal LihatSaldoKas() =>
            DataStore.Instance.KelasList.First(k => k.Id == KelasId).HitungSaldo();

        public List<Transaksi> LihatRiwayatTransaksi() =>
            DataStore.Instance.KelasList.First(k => k.Id == KelasId).GetRiwayatTransaksi();

        public List<Siswa> KelolaDataSiswa() =>
            DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == KelasId).ToList();
    }
}
