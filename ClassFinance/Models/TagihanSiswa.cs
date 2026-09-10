namespace ClassFinance.Models
{
    public class TagihanSiswa
    {
        public int Id { get; set; }
        public int TagihanId { get; set; }
        public int SiswaId { get; set; }
        public StatusTagihan Status { get; set; }
        public decimal AmountDue { get; set; }

        public void UpdateStatus()
        {
            Status = AmountDue <= 0 ? StatusTagihan.Lunas : StatusTagihan.BelumBayar;
        }
    }
}
