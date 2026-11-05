using System.Linq;
using System.Globalization;
using ClassFinance.Services;

namespace ClassFinance.Models
{
    public sealed class HasilPengingatTagihan
    {
        public int JumlahTerkirim { get; set; }
        public int SudahDiingatkanHariIni { get; set; }
        public int JumlahSiswaBelumLunas { get; set; }
        public decimal TotalSisa { get; set; }
    }

    public class Notifikasi
    {
        private const string JudulPengingatMingguan = "Pengingat Pembayaran";
        private static readonly CultureInfo IdId = CultureInfo.GetCultureInfo("id-ID");

        public int Id { get; set; }
        public int UserId { get; set; }
        public int? BillId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public void MarkAsRead() => IsRead = true;

        public static Notifikasi Kirim(int userId, string title, string message, int? billId = null)
        {
            var n = Buat(userId, title, message, billId);
            DataStore.Instance.NotifikasiList.Add(n);
            DataStore.Instance.SaveChanges();
            return n;
        }

        public static void KirimUntukTagihanBaru(int kelasId, int tagihanId)
        {
            var tagihan = DataStore.Instance.TagihanList.First(t => t.Id == tagihanId);
            if (!tagihan.IsWeeklyOccurrence)
                throw new InvalidOperationException("Notifikasi tagihan hanya dapat dibuat untuk periode mingguan.");

            var store = DataStore.Instance;
            var entries = store.TagihanSiswaList.Where(x => x.TagihanId == tagihanId).ToDictionary(x => x.SiswaId);
            var created = 0;
            foreach (var siswa in store.Users.OfType<Siswa>().Where(s => s.KelasId == kelasId))
            {
                var period = PeriodLabel(tagihan);
                var remaining = entries.TryGetValue(siswa.Id, out var entry) ? entry.AmountDue : tagihan.Amount;
                store.NotifikasiList.Add(Buat(siswa.Id, "Tagihan Baru",
                    $"Tagihan '{period}' sebesar Rp{tagihan.Amount.ToString("N0", IdId)} telah dibuat. Sisa pembayaran Anda Rp{remaining.ToString("N0", IdId)}.", tagihan.Id));
                created++;
            }

            if (created > 0)
                store.SaveChanges();
        }

        public static void KirimPembaruanJadwalMingguan(JadwalTagihanMingguan schedule)
        {
            var store = DataStore.Instance;
            var effectiveWeek = schedule.StartsOn.ToString("dd MMM yyyy", IdId);
            var message = schedule.IsActive
                ? $"Jadwal iuran mingguan diperbarui. Mulai minggu {effectiveWeek}, nominalnya Rp{schedule.Amount.ToString("N0", IdId)} per siswa. Tagihan untuk minggu yang sudah terbit tetap mengikuti nominal sebelumnya."
                : $"Jadwal iuran mingguan '{schedule.Name}' dijeda. Tagihan untuk minggu yang sudah terbit tetap harus dibayar sesuai nominalnya.";
            var created = 0;

            foreach (var student in store.Users.OfType<Siswa>().Where(x => x.KelasId == schedule.KelasId))
            {
                store.NotifikasiList.Add(Buat(student.Id, "Jadwal Tagihan Diperbarui", message, null));
                created++;
            }

            if (created > 0)
                store.SaveChanges();
        }

        public static HasilPengingatTagihan KirimPengingatTagihan(int tagihanId)
        {
            var store = DataStore.Instance;
            var bill = store.TagihanList.FirstOrDefault(x => x.Id == tagihanId)
                ?? throw new InvalidOperationException("Tagihan tidak ditemukan.");
            if (!bill.IsWeeklyOccurrence)
                throw new InvalidOperationException("Pengingat hanya tersedia untuk tagihan mingguan.");

            var dueEntries = store.TagihanSiswaList
                .Where(x => x.TagihanId == tagihanId && x.AmountDue > 0 && x.Status != StatusTagihan.Proses && x.Status != StatusTagihan.Lunas)
                .ToList();
            var result = new HasilPengingatTagihan
            {
                JumlahSiswaBelumLunas = dueEntries.Count,
                TotalSisa = dueEntries.Sum(x => x.AmountDue)
            };
            var today = DateTime.Today;

            foreach (var entry in dueEntries)
            {
                var alreadySent = store.NotifikasiList.Any(x => x.UserId == entry.SiswaId && x.BillId == tagihanId &&
                    x.Title == JudulPengingatMingguan && x.CreatedAt.Date == today);
                if (alreadySent)
                {
                    result.SudahDiingatkanHariIni++;
                    continue;
                }

                var message = $"Masih ada tagihan sebesar Rp{entry.AmountDue.ToString("N0", IdId)} yang belum lunas untuk {PeriodLabel(bill)}. Silakan membayar langsung kepada Bendahara.";
                store.NotifikasiList.Add(Buat(entry.SiswaId, JudulPengingatMingguan, message, tagihanId));
                result.JumlahTerkirim++;
            }

            if (result.JumlahTerkirim > 0)
                store.SaveChanges();

            return result;
        }

        private static Notifikasi Buat(int userId, string title, string message, int? billId) => new()
        {
            Id = DataStore.Instance.NextNotifikasiId(),
            UserId = userId,
            BillId = billId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        private static string PeriodLabel(Tagihan bill)
        {
            if (!bill.PeriodStart.HasValue || !bill.PeriodEnd.HasValue)
                return bill.Name;
            var week = JadwalTagihanMingguanService.GetWeekNumber(bill);
            return $"Minggu ke-{week}, {bill.PeriodStart.Value.ToString("dd MMM", IdId)}–{bill.PeriodEnd.Value.ToString("dd MMM yyyy", IdId)}";
        }
    }
}
