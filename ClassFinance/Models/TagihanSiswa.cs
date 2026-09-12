namespace ClassFinance.Models
{
    public class TagihanSiswa
    {
        public int Id { get; set; }
        public int TagihanId { get; set; }
        public int SiswaId { get; set; }
        public StatusTagihan Status { get; set; }

        // This represents the remaining amount the student still needs to pay
        public decimal AmountDue { get; set; }

        // NEW PROPERTY: Tracks how much has actually been paid so far
        public decimal JumlahDibayar { get; set; }

        public void UpdateStatus()
        {
            if (AmountDue <= 0)
            {
                Status = StatusTagihan.Lunas;
            }
            else if (JumlahDibayar > 0)
            {
                // Note: Make sure to add 'Sebagian' to your StatusTagihan enum if it's not there yet!
                Status = StatusTagihan.Sebagian;
            }
            else
            {
                Status = StatusTagihan.BelumBayar;
            }
        }
    }
}