using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

                var root = doc.RootElement;

                // Основной вариант — массив объектов медиа
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in root.EnumerateArray())
                    {
                        if (TryExtractImage(el, out var base64))
                            return CreateImageSource(base64);
                    }
                }
                // На всякий случай — одиночный объект в корне
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryExtractImage(root, out var base64))
                        return CreateImageSource(base64);
                }
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

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static bool TryExtractImage(JsonElement el, out string? base64)
        {
            base64 = null;
            if (el.TryGetProperty("type", out var typeProp)
                && string.Equals(typeProp.GetString(), "image", StringComparison.OrdinalIgnoreCase)
                && el.TryGetProperty("content", out var contentProp))
            {
                base64 = contentProp.GetString();
                return !string.IsNullOrWhiteSpace(base64);
            }

            return false;
        }

        private static ImageSource? CreateImageSource(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                var bytes = System.Convert.FromBase64String(base64);
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
