using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClassFinance.Models;

namespace ClassFinance.Converters
{
    /// <summary>Incoming transactions use semantic green; expenses use semantic red.</summary>
    public class TransaksiColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Income = new(Color.FromRgb(0x10, 0xB9, 0x81));
        private static readonly SolidColorBrush Expense = new(Color.FromRgb(0xF4, 0x3F, 0x5E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Transaksi t)
                return t.Type == JenisTransaksi.Masuk
                    ? ThemeBrush("IncomeColor", Income)
                    : ThemeBrush("ExpenseColor", Expense);
            return ThemeBrush("ExpenseColor", Expense);
        }

        private static SolidColorBrush ThemeBrush(string key, SolidColorBrush fallback) =>
            Application.Current?.TryFindResource(key) as SolidColorBrush ?? fallback;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
