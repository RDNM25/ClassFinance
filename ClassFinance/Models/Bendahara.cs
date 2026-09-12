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

            // FIX: When a new bill is created, automatically apply any existing student SaldoTitipan toward it
            var siswaList = DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == kelasId).ToList();
            foreach (var siswa in siswaList)
            {
                if (siswa.SaldoTitipan > 0)
                {
                    ProsesSaldoTitipanBerikutnya(siswa.Id);
                }
            }

            Notifikasi.KirimUntukTagihanBaru(kelasId, tagihan.Id);
            return tagihan;
        }

        public Transaksi CatatPembayaran(int tagihanSiswaId, decimal amount, string method, string proofFile = null)
        {
            var ts = DataStore.Instance.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == ts.SiswaId);

            // TRACK PARTIAL PAYMENTS
            ts.JumlahDibayar += amount;
            ts.AmountDue = Math.Max(0, ts.AmountDue - amount);
            ts.UpdateStatus();

            return DataStore.Instance.TambahTransaksi(
                siswa.KelasId, JenisTransaksi.Masuk, amount, DateTime.Now,
                $"Pembayaran dari {siswa.Name}", tagihanSiswaId, Name, proofFile, siswaId: siswa.Id);
        }

        // Refund overpayment directly back to the student
        public Transaksi KembalikanDana(int kelasId, int siswaId, decimal amount)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            // Record as an expense (Keluar) to represent physical cash being handed back
            return DataStore.Instance.TambahTransaksi(
                kelasId, JenisTransaksi.Keluar, amount, DateTime.Now,
                $"Pengembalian dana lebih (refund) untuk {siswa.Name}", null, Name, siswaId: siswa.Id);
        }

        // Save overpayment to the student's balance for next time and automatically apply it
        public void SimpanKelebihan(int siswaId, decimal amount)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            siswa.SaldoTitipan += amount;

            // Automatically check and apply stored balance to future bills
            ProsesSaldoTitipanBerikutnya(siswaId);
        }

        // Automatically apply stored balance to subsequent bills
        public void ProsesSaldoTitipanBerikutnya(int siswaId)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            var unpaidBills = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.SiswaId == siswaId && ts.Status != StatusTagihan.Lunas)
                .OrderBy(ts => ts.Id)
                .ToList();

            foreach (var ts in unpaidBills)
            {
                if (siswa.SaldoTitipan <= 0) break;

                if (siswa.SaldoTitipan >= ts.AmountDue)
                {
                    // Enough to cover the remaining bill completely
                    decimal amountToPay = ts.AmountDue;
                    siswa.SaldoTitipan -= amountToPay;
                    CatatPembayaran(ts.Id, amountToPay, "Saldo Titipan");
                }
                else
                {
                    // Not enough: pay as much as possible, deplete stored amount, and log as pemasukan
                    decimal amountToPay = siswa.SaldoTitipan;
                    siswa.SaldoTitipan = 0;
                    CatatPembayaran(ts.Id, amountToPay, "Saldo Titipan");
                    break;
                }
            }
        }

        // Allow student to use their saved balance to pay a future bill
        public Transaksi BayarTagihanDariSaldo(int tagihanSiswaId, int siswaId, decimal amount)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            if (siswa.SaldoTitipan < amount)
                throw new InvalidOperationException("Saldo titipan tidak mencukupi.");

            // Deduct from their saved balance[cite: 7]
            siswa.SaldoTitipan -= amount;

            // Record the payment normally[cite: 7]
            return CatatPembayaran(tagihanSiswaId, amount, "Saldo Titipan");
        }

        public string GenerateLaporan(int kelasId, string periode, string format)
        {
            var kelas = DataStore.Instance.KelasList.First(k => k.Id == kelasId);
            return $"Laporan Keuangan {kelas.Name} - Periode {periode} ({format})\nTotal Saldo: Rp{kelas.HitungSaldo():N0}";
        }

        public void BackupDatabase()
        {
            // Placeholder hook for a future export-to-file / cloud backup routine.[cite: 7]
        }
    }
}