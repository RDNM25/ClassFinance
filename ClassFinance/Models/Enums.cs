namespace ClassFinance.Models
{
    public enum Role
    {
        Bendahara,
        WaliKelas,
        Siswa
    }

    public enum JenisTransaksi
    {
        Masuk,
        Keluar
    }

    public enum MetodeTransaksi
    {
        Tunai,
        Qris,
        Tf
    }

    public static class MetodeTransaksiExtensions
    {
        public static string ToDisplayLabel(this MetodeTransaksi method) => method switch
        {
            MetodeTransaksi.Qris => "Qris",
            MetodeTransaksi.Tf => "Tf",
            _ => "Tunai"
        };
    }

    public enum StatusTagihan
    {
        BelumBayar,
        Sebagian,
        Proses,
        Lunas
    }
}
