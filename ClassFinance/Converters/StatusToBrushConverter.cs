using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClassFinance.Models;

namespace ClassFinance.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(0x10, 0xB9, 0x81));
        private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xF5, 0x9E, 0x0B));
        private static readonly SolidColorBrush Red = new(Color.FromRgb(0xF4, 0x3F, 0x5E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StatusTagihan s)
            {
                return s switch
                {
                    StatusTagihan.Lunas => ThemeBrush("StatusGreen", Green),
                    StatusTagihan.Proses => ThemeBrush("StatusAmber", Amber),
                    StatusTagihan.Sebagian => ThemeBrush("StatusAmber", Amber),
                    _ => ThemeBrush("StatusRed", Red)
                };
            }
            return ThemeBrush("StatusRed", Red);
        }

        private static SolidColorBrush ThemeBrush(string key, SolidColorBrush fallback) =>
            Application.Current?.TryFindResource(key) as SolidColorBrush ?? fallback;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
