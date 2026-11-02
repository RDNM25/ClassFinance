using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using ClassFinance.Services;

namespace ClassFinance.Converters
{
    public class HasTransactionDateConverter : IMultiValueConverter
    {
        public int KelasId { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not DateTime date) return false;

            return DataStore.Instance.TransaksiList.Any(t =>
                t.KelasId == KelasId && t.Date.Date == date.Date);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
