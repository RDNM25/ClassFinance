using System;
using System.Globalization;
using System.Windows.Data;

namespace ClassFinance.Converters
{
    /// <summary>Formats a decimal as "Rp 12.345" using Indonesian thousands separators.</summary>
    public class RupiahConverter : IValueConverter
    {
        private static readonly CultureInfo IdId = new("id-ID");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return $"Rp {d.ToString("N0", IdId)}";
            if (value is int i) return $"Rp {i.ToString("N0", IdId)}";
            return "Rp 0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
