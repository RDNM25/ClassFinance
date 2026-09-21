using System;
using System.Linq;
using ClassFinance.Services;

namespace ClassFinance.Models
{
    public class Tagihan
    {
        public int Id { get; set; }
        public int KelasId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;


        public void GenerateTagihanSiswa(DataStore store)
        {
            var siswaDiKelas = store.Users.OfType<Siswa>().Where(s => s.KelasId == KelasId);
            foreach (var siswa in siswaDiKelas)
            {
                store.TagihanSiswaList.Add(new TagihanSiswa
                {
                    Id = store.NextTagihanSiswaId(),
                    TagihanId = Id,
                    SiswaId = siswa.Id,
                    AmountDue = Amount,
                    Status = StatusTagihan.BelumBayar
                });
            }
        }
    }
}
