using System;
using System.Globalization;
using System.Windows.Data;

namespace DJCMS.Converters
{
    public class AddOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return (i + 1).ToString();

            if (int.TryParse(value?.ToString() ?? "0", out var v))
                return (v + 1).ToString();

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
