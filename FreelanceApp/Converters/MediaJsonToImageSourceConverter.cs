using System;
using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using FreelanceApp.Helpers;

namespace FreelanceApp.Converters
{
    public sealed class MediaJsonToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            JsonDocument? doc = null;
            var ownsDoc = false;
            string? base64 = null;

            try
            {
                if (value is JsonDocument existingDoc)
                {
                    doc = existingDoc;
                }
                else if (value is string json)
                {
                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    doc = JsonDocument.Parse(json);
                    ownsDoc = true;
                }
                else
                {
                    return null;
                }

                var parsed = MediaJsonHelper.ExtractFirstImage(doc);
                base64 = parsed.base64;
            }
            catch
            {
                // игнорируем, просто не показываем картинку
            }
            finally
            {
                if (ownsDoc)
                    doc?.Dispose();
            }

            return MediaJsonHelper.CreateImageSource(base64);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

    }
}
