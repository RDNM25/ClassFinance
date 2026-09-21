namespace ClassFinance.Models
{
    public class TagihanSiswa
    {
        public int Id { get; set; }
        public int TagihanId { get; set; }
        public int SiswaId { get; set; }
        public StatusTagihan Status { get; set; }

        public decimal AmountDue { get; set; }

        public decimal JumlahDibayar { get; set; }

        public void UpdateStatus()
        {
            if (AmountDue <= 0)
            {
                Status = StatusTagihan.Lunas;
            }
            else if (JumlahDibayar > 0)
            {
                Status = StatusTagihan.Sebagian;
            }
            else
            {
                Status = StatusTagihan.BelumBayar;
            }
        }
    }
}