using System.Linq;
using ClassFinance.Services;

namespace ClassFinance.Models
{
    public class Notifikasi
    {
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
            var n = new Notifikasi
            {
                Id = DataStore.Instance.NextNotifikasiId(),
                UserId = userId,
                BillId = billId,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            DataStore.Instance.NotifikasiList.Add(n);
            DataStore.Instance.SaveChanges();
            return n;
        }

        public static void KirimUntukTagihanBaru(int kelasId, int tagihanId)
        {
            var tagihan = DataStore.Instance.TagihanList.First(t => t.Id == tagihanId);
            foreach (var siswa in DataStore.Instance.Users.OfType<Siswa>().Where(s => s.KelasId == kelasId))
            {
                Kirim(siswa.Id, "Tagihan Baru",
                    $"Tagihan '{tagihan.Name}' sebesar Rp{tagihan.Amount:N0} telah dibuat.", tagihan.Id);
            }
        }
    }
}
