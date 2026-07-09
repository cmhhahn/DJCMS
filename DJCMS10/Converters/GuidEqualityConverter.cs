using System;
using System.Globalization;
using System.Windows.Data;

namespace DJCMS.Converters
{
    public class GuidEqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            if (values[0] is Guid trackId)
            {
                if (values[1] == null)
                    return false;

                if (values[1] is Guid playingId)
                {
                    return trackId == playingId;
                }
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
