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

        public Transaksi TambahPemasukan(int kelasId, string source, decimal amount, DateTime date, int? tagihanSiswaId = null,
            int? siswaId = null, MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            return DataStore.Instance.TambahTransaksi(kelasId, JenisTransaksi.Masuk, amount, date, source, tagihanSiswaId, Name,
                siswaId: siswaId, metode: metode);
        }

        public Transaksi TambahPengeluaran(int kelasId, string category, decimal amount, DateTime date,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            return DataStore.Instance.TambahTransaksi(kelasId, JenisTransaksi.Keluar, amount, date, category, null, Name,
                metode: metode);
        }

        public void UbahNominalTransaksi(int transaksiId, decimal amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            var transaksi = DataStore.Instance.TransaksiList.First(t => t.Id == transaksiId);
            if (transaksi.IsRefund)
                throw new InvalidOperationException("Nominal refund harus dikelola melalui status refund.");

            if (transaksi.Type == JenisTransaksi.Keluar)
            {
                transaksi.Amount = amount;
                DataStore.Instance.SaveChanges();
                return;
            }

            if (transaksi.Type != JenisTransaksi.Masuk)
                throw new InvalidOperationException("Jenis transaksi ini tidak dapat diubah.");

            if (transaksi.TagihanSiswaId.HasValue)
            {
                var tagihanId = transaksi.TagihanSiswaId.Value;
                if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == tagihanId))
                    throw new InvalidOperationException("Pembayaran yang sudah memiliki refund tidak dapat diubah.");

                var tagihanSiswa = DataStore.Instance.TagihanSiswaList.First(ts => ts.Id == tagihanId);
                var tagihan = DataStore.Instance.TagihanList.First(t => t.Id == tagihanSiswa.TagihanId);
                var totalAfterEdit = DataStore.Instance.TransaksiList
                    .Where(t => t.TagihanSiswaId == tagihanId && t.Type == JenisTransaksi.Masuk)
                    .Sum(t => t.Id == transaksiId ? amount : t.Amount);
                if (totalAfterEdit > tagihan.Amount)
                    throw new InvalidOperationException("Nominal baru tidak boleh membuat pembayaran melebihi tagihan.");

                transaksi.Amount = amount;
                tagihanSiswa.JumlahDibayar = totalAfterEdit;
                tagihanSiswa.AmountDue = Math.Max(0, tagihan.Amount - totalAfterEdit);
                tagihanSiswa.UpdateStatus();
                DataStore.Instance.SaveChanges();
                return;
            }

            transaksi.Amount = amount;
            DataStore.Instance.SaveChanges();
        }

        public void HapusTransaksi(int transaksiId)
        {
            var transaksi = DataStore.Instance.TransaksiList.First(t => t.Id == transaksiId);
            TagihanSiswa linkedEntry = null;
            Tagihan linkedBill = null;
              
            if (transaksi.Type == JenisTransaksi.Masuk && transaksi.TagihanSiswaId.HasValue)
            {
                var tagihanSiswaId = transaksi.TagihanSiswaId.Value;
                if (DataStore.Instance.TransaksiList.Any(t => t.IsRefund && t.TagihanSiswaId == tagihanSiswaId))
                    throw new InvalidOperationException("Pembayaran dengan refund terkait tidak dapat dihapus.");

                linkedEntry = DataStore.Instance.TagihanSiswaList.First(ts => ts.Id == tagihanSiswaId);
                linkedBill = DataStore.Instance.TagihanList.First(t => t.Id == linkedEntry.TagihanId);
                var totalPayments = DataStore.Instance.TransaksiList
                    .Where(t => t.TagihanSiswaId == tagihanSiswaId && t.Type == JenisTransaksi.Masuk)
                    .Sum(t => t.Amount);
                if (totalPayments > linkedBill.Amount)
                    throw new InvalidOperationException("Pembayaran dengan saldo lebih tidak dapat dihapus.");
            }

            DataStore.Instance.DeleteTransaction(transaksiId);

            if (transaksi.IsRefund && transaksi.TagihanSiswaId.HasValue)
            {
                linkedEntry = DataStore.Instance.TagihanSiswaList.FirstOrDefault(ts => ts.Id == transaksi.TagihanSiswaId.Value);
                linkedBill = linkedEntry == null ? null : DataStore.Instance.TagihanList.FirstOrDefault(t => t.Id == linkedEntry.TagihanId);
            }

            if (linkedEntry != null && linkedBill != null)
            {
                linkedEntry.JumlahDibayar = DataStore.Instance.TransaksiList
                    .Where(t => t.TagihanSiswaId == linkedEntry.Id && t.Type == JenisTransaksi.Masuk)
                    .Sum(t => t.Amount);
                linkedEntry.AmountDue = Math.Max(0, linkedBill.Amount - linkedEntry.JumlahDibayar);

                if (DataStore.Instance.TransaksiList.Any(t => t.IsPendingRefund && t.TagihanSiswaId == linkedEntry.Id))
                    linkedEntry.Status = StatusTagihan.Proses;
                else
                    linkedEntry.UpdateStatus();
            }
            DataStore.Instance.SaveChanges();
        }

        public JadwalTagihanMingguan SimpanJadwalTagihanMingguan(
            int kelasId, string name, decimal amount, DateTime startsOn, DateTime? endsOn, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nama iuran wajib diisi.", nameof(name));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (endsOn.HasValue && endsOn.Value.Date < startsOn.Date)
                throw new ArgumentException("Tanggal akhir tidak boleh sebelum tanggal mulai.", nameof(endsOn));

            var store = DataStore.Instance;
            var schedule = store.JadwalTagihanMingguanList.FirstOrDefault(x => x.KelasId == kelasId);
            var isExistingSchedule = schedule != null;
            var previousName = schedule?.Name;
            var previousAmount = schedule?.Amount;
            var previousStart = schedule?.StartsOn.Date;
            var previousEnd = schedule?.EndsOn?.Date;
            var previousActive = schedule?.IsActive;
            if (schedule == null)
            {
                schedule = new JadwalTagihanMingguan
                {
                    Id = store.NextWeeklyScheduleId(),
                    KelasId = kelasId,
                    CreatedDate = DateTime.Now
                };
                store.JadwalTagihanMingguanList.Add(schedule);
            }

            var daysSinceMonday = ((int)startsOn.DayOfWeek + 6) % 7;
            var normalizedStart = startsOn.Date.AddDays(-daysSinceMonday);
            schedule.Name = name.Trim();
            schedule.Amount = amount;
            schedule.StartsOn = normalizedStart;
            schedule.EndsOn = endsOn?.Date;
            schedule.IsActive = isActive;
            store.SaveChanges();

            var changed = isExistingSchedule &&
                (!string.Equals(previousName, schedule.Name, StringComparison.Ordinal) ||
                 previousAmount != schedule.Amount ||
                 previousStart != schedule.StartsOn.Date ||
                 previousEnd != schedule.EndsOn?.Date ||
                 previousActive != schedule.IsActive);
            if (changed)
                Notifikasi.KirimPembaruanJadwalMingguan(schedule);

            return schedule;
        }

        public Transaksi CatatPembayaran(int tagihanSiswaId, decimal amount, MetodeTransaksi metode = MetodeTransaksi.Tunai,
            string proofFile = null, DateTime? date = null)
        {
            var store = DataStore.Instance;
            var ts = store.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            var bill = store.TagihanList.First(t => t.Id == ts.TagihanId);
            if (!bill.IsWeeklyOccurrence)
                throw new InvalidOperationException("Pembayaran baru hanya dapat dicatat untuk tagihan mingguan.");

            var siswa = store.Users.OfType<Siswa>().First(s => s.Id == ts.SiswaId);

            // TRACK PARTIAL PAYMENTS
            ts.JumlahDibayar += amount;
            ts.AmountDue = Math.Max(0, ts.AmountDue - amount);
            ts.UpdateStatus();

            return DataStore.Instance.TambahTransaksi(
                siswa.KelasId, JenisTransaksi.Masuk, amount, date ?? DateTime.Now,
                $"Pembayaran dari {siswa.Name}", tagihanSiswaId, Name, proofFile, siswaId: siswa.Id, metode: metode);
        }

        // Record a refund as pending. It only reduces kas after the cash is paid out.
        public Transaksi KembalikanDana(int kelasId, int siswaId, decimal amount, int tagihanSiswaId, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            var refund = DataStore.Instance.TambahTransaksi(
                kelasId, JenisTransaksi.Keluar, amount, date ?? DateTime.Now,
                $"Pengembalian dana lebih (refund) untuk {siswa.Name}", tagihanSiswaId, Name,
                siswaId: siswa.Id, affectsKas: false, metode: metode);
            refund.IsRefund = true;
            refund.IsPendingRefund = true;
            var tagihan = DataStore.Instance.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            tagihan.Status = StatusTagihan.Proses;
            DataStore.Instance.SaveChanges();
            return refund;
        }

        public void TandaiRefundDibayar(int transaksiId)
        {
            var refund = DataStore.Instance.TransaksiList.First(t => t.Id == transaksiId);
            if (!refund.IsPendingRefund) return;

            refund.IsPendingRefund = false;
            refund.AffectsKas = true;

            var tagihanId = refund.TagihanSiswaId;
            if (tagihanId.HasValue && !DataStore.Instance.TransaksiList.Any(t =>
                    t.IsPendingRefund && t.TagihanSiswaId == tagihanId.Value))
            {
                DataStore.Instance.TagihanSiswaList.FirstOrDefault(t => t.Id == tagihanId.Value)?.UpdateStatus();
            }
            DataStore.Instance.SaveChanges();
        }

        // Save overpayment to the student's balance and automatically apply it without inflating total cash
        public void SimpanKelebihan(int siswaId, decimal amount, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            siswa.SaldoTitipan += amount;

            // Automatically check and apply stored balance to future bills with 0 cash added
            ProsesSaldoTitipanBerikutnya(siswaId, amount, date, metode);
            DataStore.Instance.SaveChanges();
        }

        // Automatically apply stored balance to subsequent bills with 0 additional cash added to the total kas
        public void ProsesSaldoTitipanBerikutnya(int siswaId, decimal storedAmountContext = 0, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            var effectiveDate = date ?? DateTime.Now;
            var weeklyBillIds = DataStore.Instance.TagihanList
                .Where(bill => bill.KelasId == siswa.KelasId && bill.IsWeeklyOccurrence)
                .Select(bill => bill.Id)
                .ToHashSet();

            var unpaidBills = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.SiswaId == siswaId && weeklyBillIds.Contains(ts.TagihanId) &&
                             ts.Status != StatusTagihan.Lunas && ts.Status != StatusTagihan.Proses)
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
                        siswa.KelasId, JenisTransaksi.Masuk, amountToPay, effectiveDate,
                        $"Pembayaran dari {siswa.Name} (dari simpanan Rp {storedAmountContext:N0})", ts.Id, Name,
                        siswaId: siswa.Id, affectsKas: false, metode: metode);
                }
                else
                {
                    decimal amountToPay = siswa.SaldoTitipan;
                    siswa.SaldoTitipan = 0;

                    ts.JumlahDibayar += amountToPay;
                    ts.AmountDue = Math.Max(0, ts.AmountDue - amountToPay);
                    ts.UpdateStatus();

                    DataStore.Instance.TambahTransaksi(
                        siswa.KelasId, JenisTransaksi.Masuk, amountToPay, effectiveDate,
                        $"Pembayaran dari {siswa.Name} (dari simpanan Rp {storedAmountContext:N0})", ts.Id, Name,
                        siswaId: siswa.Id, affectsKas: false, metode: metode);
                    break;
                }
            }
            DataStore.Instance.SaveChanges();
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
