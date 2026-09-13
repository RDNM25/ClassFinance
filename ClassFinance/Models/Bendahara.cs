using System;
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

        public Tagihan BuatTagihan(int kelasId, string name, decimal amount, bool isIuran = false)
        {
            // Append "Iuran" to the name so it shows correctly in history and UI
            var tagihanName = isIuran ? $"{name} Iuran" : name;
            var tagihan = DataStore.Instance.BuatTagihan(kelasId, tagihanName, amount, isIuran);

            // Only apply stored balance (Saldo Titipan) if it is NOT an Iuran
            if (!isIuran)
            {
                var siswaList = DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == kelasId).ToList();
                foreach (var siswa in siswaList)
                {
                    if (siswa.SaldoTitipan > 0)
                    {
                        ProsesSaldoTitipanBerikutnya(siswa.Id, siswa.SaldoTitipan);
                    }
                }
            }

            Notifikasi.KirimUntukTagihanBaru(kelasId, tagihan.Id);
            return tagihan;
        }

        public void ProsesSaldoTitipanBerikutnya(int siswaId, decimal storedAmountContext = 0)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            // Get unpaid bills, EXCLUDING Iuran because Iuran does not take from stored money.
            var unpaidBills = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.SiswaId == siswaId && ts.Status != StatusTagihan.Lunas)
                .Where(ts => {
                    var t = DataStore.Instance.TagihanList.FirstOrDefault(x => x.Id == ts.TagihanId);
                    return t != null && !t.IsIuran;
                })
                .OrderBy(ts => ts.Id)
                .ToList();

            foreach (var ts in unpaidBills)
            {
                if (siswa.SaldoTitipan <= 0) break;

                if (siswa.SaldoTitipan >= ts.AmountDue)
                {
                    decimal amountToPay = ts.AmountDue;
                    siswa.SaldoTitipan -= amountToPay;

                    ts.JumlahDibayar += amountToPay;
                    ts.AmountDue = 0;
                    ts.UpdateStatus();

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
