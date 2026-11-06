using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task<Transaksi> TambahPemasukanAsync(int kelasId, string source, decimal amount, DateTime date, int? tagihanSiswaId = null,
            int? siswaId = null, MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            return DataStore.Instance.TambahTransaksiAsync(kelasId, JenisTransaksi.Masuk, amount, date, source, tagihanSiswaId, Name,
                siswaId: siswaId, metode: metode);
        }

        public Task<Transaksi> TambahPengeluaranAsync(int kelasId, string category, decimal amount, DateTime date,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            return DataStore.Instance.TambahTransaksiAsync(kelasId, JenisTransaksi.Keluar, amount, date, category, null, Name,
                metode: metode);
        }

        public async Task UbahNominalTransaksiAsync(int transaksiId, decimal amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            var transaksi = DataStore.Instance.TransaksiList.First(t => t.Id == transaksiId);
            if (transaksi.IsRefund)
                throw new InvalidOperationException("Nominal refund harus dikelola melalui status refund.");

            if (transaksi.Type == JenisTransaksi.Keluar)
            {
                transaksi.Amount = amount;
                await DataStore.Instance.SaveChangesAsync();
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
                await DataStore.Instance.SaveChangesAsync();
                return;
            }

            transaksi.Amount = amount;
            await DataStore.Instance.SaveChangesAsync();
        }

        public async Task HapusTransaksiAsync(int transaksiId)
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

            DataStore.Instance.RemoveTransaction(transaksiId);

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
            await DataStore.Instance.SaveChangesAsync();
        }

        public async Task<JadwalTagihanMingguan> SimpanJadwalTagihanMingguanAsync(
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
            await store.SaveChangesAsync();

            var changed = isExistingSchedule &&
                (!string.Equals(previousName, schedule.Name, StringComparison.Ordinal) ||
                 previousAmount != schedule.Amount ||
                 previousStart != schedule.StartsOn.Date ||
                 previousEnd != schedule.EndsOn?.Date ||
                 previousActive != schedule.IsActive);
            if (changed)
                await Notifikasi.KirimPembaruanJadwalMingguanAsync(schedule);

            return schedule;
        }

        public Task<Transaksi> CatatPembayaranAsync(int tagihanSiswaId, decimal amount, MetodeTransaksi metode = MetodeTransaksi.Tunai,
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

            return DataStore.Instance.TambahTransaksiAsync(
                siswa.KelasId, JenisTransaksi.Masuk, amount, date ?? DateTime.Now,
                $"Pembayaran dari {siswa.Name}", tagihanSiswaId, Name, proofFile, siswaId: siswa.Id, metode: metode);
        }

        // Record a refund as pending. It only reduces kas after the cash is paid out.
        public async Task<Transaksi> KembalikanDanaAsync(int kelasId, int siswaId, decimal amount, int tagihanSiswaId, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);

            var refund = DataStore.Instance.CreateTransaction(
                kelasId, JenisTransaksi.Keluar, amount, date ?? DateTime.Now,
                $"Pengembalian dana lebih (refund) untuk {siswa.Name}", tagihanSiswaId, Name,
                siswaId: siswa.Id, affectsKas: false, metode: metode);
            refund.IsRefund = true;
            refund.IsPendingRefund = true;
            var tagihan = DataStore.Instance.TagihanSiswaList.First(t => t.Id == tagihanSiswaId);
            tagihan.Status = StatusTagihan.Proses;
            await DataStore.Instance.SaveChangesAsync();
            return refund;
        }

        public Task TandaiRefundDibayarAsync(int transaksiId) => TandaiRefundDibayarAsync(new[] { transaksiId });

        public async Task TandaiRefundDibayarAsync(IEnumerable<int> transaksiIds)
        {
            var store = DataStore.Instance;
            var refunds = transaksiIds
                .Select(id => store.TransaksiList.FirstOrDefault(t => t.Id == id))
                .Where(refund => refund?.IsPendingRefund == true)
                .ToList();
            if (refunds.Count == 0) return;

            foreach (var refund in refunds)
            {
                refund.IsPendingRefund = false;
                refund.AffectsKas = true;
            }

            var affectedBillIds = refunds
                .Where(refund => refund.TagihanSiswaId.HasValue)
                .Select(refund => refund.TagihanSiswaId.Value)
                .Distinct();
            foreach (var tagihanId in affectedBillIds)
            {
                if (!store.TransaksiList.Any(t => t.IsPendingRefund && t.TagihanSiswaId == tagihanId))
                    store.TagihanSiswaList.FirstOrDefault(t => t.Id == tagihanId)?.UpdateStatus();
            }
            await store.SaveChangesAsync();
        }

        // Save overpayment to the student's balance and automatically apply it without inflating total cash
        public Task SimpanKelebihanAsync(int siswaId, decimal amount, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var siswa = DataStore.Instance.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            siswa.SaldoTitipan += amount;

            // Automatically check and apply stored balance to future bills with 0 cash added
            return ProsesSaldoTitipanBerikutnyaAsync(siswaId, amount, date, metode);
        }

        // Automatically apply stored balance to subsequent bills with 0 additional cash added to the total kas
        public async Task ProsesSaldoTitipanBerikutnyaAsync(int siswaId, decimal storedAmountContext = 0, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var store = DataStore.Instance;
            var siswa = store.Users.OfType<Siswa>().First(s => s.Id == siswaId);
            var weeklyBillIds = store.TagihanList
                .Where(bill => bill.KelasId == siswa.KelasId && bill.IsWeeklyOccurrence)
                .Select(bill => bill.Id)
                .ToHashSet();
            if (ApplyStoredCredit(siswa, weeklyBillIds, storedAmountContext, date ?? DateTime.Now, metode))
                await store.SaveChangesAsync();
        }

        internal bool ApplyStoredCreditForClass(int kelasId, DateTime? date = null,
            MetodeTransaksi metode = MetodeTransaksi.Tunai)
        {
            var store = DataStore.Instance;
            var weeklyBillIds = store.TagihanList
                .Where(bill => bill.KelasId == kelasId && bill.IsWeeklyOccurrence)
                .Select(bill => bill.Id)
                .ToHashSet();
            var effectiveDate = date ?? DateTime.Now;
            var changed = false;
            foreach (var student in store.Users.OfType<Siswa>()
                         .Where(student => student.KelasId == kelasId && student.SaldoTitipan > 0).ToList())
            {
                var originalBalance = student.SaldoTitipan;
                changed |= ApplyStoredCredit(student, weeklyBillIds, originalBalance, effectiveDate, metode);
            }
            return changed;
        }

        private bool ApplyStoredCredit(Siswa siswa, HashSet<int> weeklyBillIds, decimal storedAmountContext,
            DateTime effectiveDate, MetodeTransaksi metode)
        {
            var unpaidBills = DataStore.Instance.TagihanSiswaList
                .Where(ts => ts.SiswaId == siswa.Id && weeklyBillIds.Contains(ts.TagihanId) &&
                             ts.Status != StatusTagihan.Lunas && ts.Status != StatusTagihan.Proses)
                .OrderBy(ts => ts.Id)
                .ToList();
            var changed = false;

            foreach (var ts in unpaidBills)
            {
                if (siswa.SaldoTitipan <= 0) break;

                var amountToPay = Math.Min(siswa.SaldoTitipan, ts.AmountDue);
                if (amountToPay <= 0) continue;

                siswa.SaldoTitipan -= amountToPay;
                ts.JumlahDibayar += amountToPay;
                ts.AmountDue = Math.Max(0, ts.AmountDue - amountToPay);
                ts.UpdateStatus();

                // This balance was already counted as cash when it was first received.
                DataStore.Instance.CreateTransaction(
                    siswa.KelasId, JenisTransaksi.Masuk, amountToPay, effectiveDate,
                    $"Pembayaran dari {siswa.Name} (dari simpanan Rp {storedAmountContext:N0})", ts.Id, Name,
                    siswaId: siswa.Id, affectsKas: false, metode: metode);
                changed = true;
            }

            return changed;
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
