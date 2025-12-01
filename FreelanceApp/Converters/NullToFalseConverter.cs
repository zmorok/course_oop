using System;
using System.Globalization;
using System.Windows.Data;

namespace FreelanceApp.Converters
{
    public class NullToFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException(
                "NullToFalseConverter не поддерживает обратную конвертацию."
            );
        }
    }
}
