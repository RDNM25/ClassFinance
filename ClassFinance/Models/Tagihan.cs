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

        /// <summary>When this tagihan was actually created (set automatically, not user-editable).</summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates one TagihanSiswa entry per student currently enrolled in the class.
        /// Takes the DataStore explicitly (instead of reading the static DataStore.Instance)
        /// so this also works correctly while DataStore itself is still being constructed
        /// (e.g. during its own seed data setup).
        /// </summary>
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
