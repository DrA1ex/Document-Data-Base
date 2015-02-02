using System;
using System.Globalization;
using System.Windows.Data;

namespace DocumentDb.Converters
{
    public class CacheStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool cached = (bool)value;

            return cached ? "Доступно для поиска" : "Ожидает индексации";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
