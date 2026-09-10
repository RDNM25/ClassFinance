using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClassFinance.Models;

namespace ClassFinance.Converters
{
    /// <summary>Masuk -> red (matches the reference UI), Keluar -> gray.</summary>
    public class TransaksiColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Red = new(Color.FromRgb(0xD3, 0x2F, 0x2F));
        private static readonly SolidColorBrush Gray = new(Color.FromRgb(0x75, 0x75, 0x75));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Transaksi t)
                return t.Type == JenisTransaksi.Masuk ? Red : Gray;
            return Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
