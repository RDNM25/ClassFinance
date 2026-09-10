using System;
using System.Globalization;
using System.Windows.Data;
using ClassFinance.Models;

namespace ClassFinance.Converters
{
    /// <summary>Turns a Transaksi's Type + Amount into a display string like "+Rp 12.000" or "-Rp 12.000".</summary>
    public class TransaksiSignConverter : IValueConverter
    {
        private static readonly CultureInfo IdId = new("id-ID");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Transaksi t) return string.Empty;
            var sign = t.Type == JenisTransaksi.Masuk ? "+" : "-";
            return $"{sign}Rp {t.Amount.ToString("N0", IdId)}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
