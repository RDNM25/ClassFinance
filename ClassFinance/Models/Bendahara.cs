using System;
using System.Linq;
using ClassFinance.Services;

namespace ClassFinance.Models
{
    /// <summary>Treasurer account. Has full read/write access to cash, tagihan and reports.</summary>
    public class Bendahara : User
    {
        public Bendahara()
        {
            Role = Role.Bendahara;
        }

        public override string GetDashboardTitle() => "Dashboard Bendahara";

        public Transaksi TambahPemasukan(int kelasId, string source, decimal amount, DateTime date, int? tagihanSiswaId = null, int? siswaId = null)
        {
            return DataStore.Instance.TambahTransaksi(kelasId, JenisTransaksi.Masuk, amount, date, source, tagihanSiswaId, Name, siswaId: siswaId);
        }

        public Transaksi TambahPengeluaran(int kelasId, string category, decimal amount, DateTime date)
        {
            return DataStore.Instance.TambahTransaksi(kelasId, JenisTransaksi.Keluar, amount, date, category, null, Name);
        }

        public Tagihan BuatTagihan(int kelasId, string name, decimal amount)
        {
            var tagihan = DataStore.Instance.BuatTagihan(kelasId, name, amount);
            Notifikasi.KirimUntukTagihanBaru(kelasId, tagihan.Id);
            return tagihan;
        }

        public Transaksi CatatPembayaran(int tagihanSiswaId, decimal amount, string method, string proofFile = null)
        {
            var ts = DataStore.Instance.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == ts.SiswaId);

            ts.AmountDue = Math.Max(0, ts.AmountDue - amount);
            ts.UpdateStatus();

            return DataStore.Instance.TambahTransaksi(
                siswa.KelasId, JenisTransaksi.Masuk, amount, DateTime.Now,
                $"Pembayaran dari {siswa.Name}", tagihanSiswaId, Name, proofFile, siswaId: siswa.Id);
        }

        public string GenerateLaporan(int kelasId, string periode, string format)
        {
            var kelas = DataStore.Instance.KelasList.First(k => k.Id == kelasId);
            return $"Laporan Keuangan {kelas.Name} - Periode {periode} ({format})\nTotal Saldo: Rp{kelas.HitungSaldo():N0}";
        }

        public void BackupDatabase()
        {
            // Placeholder hook for a future export-to-file / cloud backup routine.
        }
    }
}
