using ClassFinance.Services;

namespace ClassFinance.Models
{
    public class Kelas
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string AcademicYear { get; set; }

        public decimal HitungSaldo() =>
            DataStore.Instance.TransaksiList
                .Where(t => t.KelasId == Id && t.AffectsKas)
                .Sum(t => t.Type == JenisTransaksi.Masuk ? t.Amount : -t.Amount);

        public List<Transaksi> GetRiwayatTransaksi() =>
            DataStore.Instance.TransaksiList
                .Where(t => t.KelasId == Id)
                .OrderByDescending(t => t.Date)
                .ToList();
    }
}
