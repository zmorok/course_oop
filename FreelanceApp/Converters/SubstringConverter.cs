using System.Globalization;
using System.Windows.Data;

namespace FreelanceApp.Converters
{
    public class SubstringConverter : IValueConverter
    {
        public int MaxLength { get; set; } = 50;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            int max = MaxLength;
            if (parameter != null && int.TryParse(parameter.ToString(), out int paramMax))
                max = paramMax;

            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}