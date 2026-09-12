using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClassFinance.Models;

namespace ClassFinance.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xF5, 0x7C, 0x00));
        private static readonly SolidColorBrush Red = new(Color.FromRgb(0xD3, 0x2F, 0x2F));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusTagihan s)
            {
                return s switch
                {
                    StatusTagihan.Lunas => Green,
                    StatusTagihan.Sebagian => Amber,
                    _ => Red
                };
            }
            return Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
