using System.Globalization;
using System.Text.Json;
using System.Windows.Data;

namespace FreelanceApp.Converters
{
    public class SubstringConverter : IValueConverter
    {
        public int MaxLength { get; set; } = 50;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? s = value as string;

            // Поддержка jsonb-полей (JsonDocument / JsonElement)
            if (s is null && value is JsonDocument doc)
                s = doc.RootElement.GetRawText();
            else if (s is null && value is JsonElement el)
                s = el.GetRawText();
            else if (s is null && value is not null)
                s = value.ToString();

            if (string.IsNullOrEmpty(s))
                return string.Empty;

            int max = MaxLength;
            if (parameter != null && int.TryParse(parameter.ToString(), out int paramMax))
                max = paramMax;

            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}

