using System;
using System.Globalization;
using System.Windows.Data;

namespace ClassFinance.Converters
{
    public class CalendarMonthStateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] is not DateTime monthDate || values[1] is not DateTime displayDate)
                return "None";

            if (values[2] is DateTime selectedDate && SameMonth(monthDate, selectedDate))
                return "Selected";

            return SameMonth(monthDate, displayDate) ? "Current" : "None";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static bool SameMonth(DateTime first, DateTime second) =>
            first.Year == second.Year && first.Month == second.Month;
    }
}
