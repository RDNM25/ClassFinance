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

            // Automatically apply any existing student SaldoTitipan toward the newly created bill without adding extra cash
            var siswaList = DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == kelasId).ToList();
            foreach (var siswa in siswaList)
            {
                if (siswa.SaldoTitipan > 0)
                {
                    ProsesSaldoTitipanBerikutnya(siswa.Id, siswa.SaldoTitipan);
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

        // Save overpayment to the student's balance and automatically apply it without inflating total cash
        public void SimpanKelebihan(int siswaId, decimal amount)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            siswa.SaldoTitipan += amount;

            // Automatically check and apply stored balance to future bills with 0 cash added
            ProsesSaldoTitipanBerikutnya(siswaId, amount);
        }

        // Automatically apply stored balance to subsequent bills with 0 additional cash added to the total kas
        public void ProsesSaldoTitipanBerikutnya(int siswaId, decimal storedAmountContext = 0)
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
                    decimal amountToPay = ts.AmountDue;
                    siswa.SaldoTitipan -= amountToPay;

                    // Update bill status to Lunas
                    ts.JumlahDibayar += amountToPay;
                    ts.AmountDue = 0;
                    ts.UpdateStatus();

                    // Record the real amount applied (so history shows what actually happened),
                    // but exclude it from kas totals -- that cash was already counted once
                    // when the original overpayment came in.
                    DataStore.Instance.TambahTransaksi(
                        siswa.KelasId, JenisTransaksi.Masuk, amountToPay, DateTime.Now,
                        $"Pembayaran dari {siswa.Name} (dari simpanan Rp {storedAmountContext:N0})", ts.Id, Name, siswaId: siswa.Id, affectsKas: false);
                }
                else
                {
                    decimal amountToPay = siswa.SaldoTitipan;
                    siswa.SaldoTitipan = 0;

                    ts.JumlahDibayar += amountToPay;
                    ts.AmountDue = Math.Max(0, ts.AmountDue - amountToPay);
                    ts.UpdateStatus();

                    DataStore.Instance.TambahTransaksi(
                        siswa.KelasId, JenisTransaksi.Masuk, amountToPay, DateTime.Now,
                        $"Pembayaran dari {siswa.Name} (dari simpanan Rp {storedAmountContext:N0})", ts.Id, Name, siswaId: siswa.Id, affectsKas: false);
                    break;
                }
            }
        }

        // Allow student to use their saved balance to pay a future bill with 0 additional cash added
        public Transaksi BayarTagihanDariSaldo(int tagihanSiswaId, int siswaId, decimal amount)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            if (siswa.SaldoTitipan < amount)
                throw new InvalidOperationException("Saldo titipan tidak mencukupi.");

            siswa.SaldoTitipan -= amount;

            var ts = DataStore.Instance.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            ts.JumlahDibayar += amount;
            ts.AmountDue = Math.Max(0, ts.AmountDue - amount);
            ts.UpdateStatus();

            return DataStore.Instance.TambahTransaksi(
                siswa.KelasId, JenisTransaksi.Masuk, amount, DateTime.Now,
                $"Pembayaran dari {siswa.Name} (dari simpanan)", tagihanSiswaId, Name, siswaId: siswa.Id, affectsKas: false);
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
