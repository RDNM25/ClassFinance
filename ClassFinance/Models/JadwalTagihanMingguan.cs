using System;

namespace ClassFinance.Models
{
    /// <summary>Recurring weekly bill settings. Each generated week is stored as its own Tagihan.</summary>
    public class JadwalTagihanMingguan
    {
        public int Id { get; set; }
        public int KelasId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
