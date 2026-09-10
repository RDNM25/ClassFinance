using System;

namespace ClassFinance.Models
{
    public class Transaksi
    {
        public int Id { get; set; }
        public int KelasId { get; set; }
        public JenisTransaksi Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public int? TagihanSiswaId { get; set; }

        /// <summary>
        /// Optional direct link to the student this money belongs to. Used for the
        /// per-student "total paid" figures on the dashboard, independent of whether
        /// the payment was tied to a formal Tagihan (TagihanSiswaId).
        /// </summary>
        public int? SiswaId { get; set; }
        public string ProofFile { get; set; }
        public string CreatedBy { get; set; }
    }
}
