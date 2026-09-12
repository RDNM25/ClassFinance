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

        /// <summary>
        /// True (default) if this transaction's Amount should count toward the class's
        /// cash total. False for bookkeeping-only entries -- e.g. applying a student's
        /// previously-recorded SaldoTitipan to a new bill: the cash was already counted
        /// once when the original overpayment came in, so re-counting it here would
        /// double the total kas even though the amount is real and worth displaying.
        /// </summary>
        public bool AffectsKas { get; set; } = true;
    }
}
