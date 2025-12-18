using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace FreelanceApp.Helpers
{
    public static class MediaJsonHelper
    {
        public static (string? name, string? base64) ExtractFirstImage(JsonDocument? doc)
        {
            if (doc is null) return (null, null);

            try
            {
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in root.EnumerateArray())
                    {
                        if (TryExtract(el, out var name, out var base64))
                            return (name, base64);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryExtract(root, out var name, out var base64))
                        return (name, base64);
                }
            }
            catch
            {
            }

            return (null, null);
        }

        private static bool TryExtract(JsonElement el, out string? name, out string? base64)
        {
            name = null;
            base64 = null;

            if (el.TryGetProperty("type", out var typeProp)
                && string.Equals(typeProp.GetString(), "image", StringComparison.OrdinalIgnoreCase)
                && el.TryGetProperty("content", out var contentProp))
            {
                base64 = contentProp.GetString();
                if (string.IsNullOrWhiteSpace(base64))
                    return false;

                if (el.TryGetProperty("name", out var nameProp))
                    name = nameProp.GetString();

                return true;
            }

            return false;
        }

        public static ImageSource? CreateImageSource(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(base64);
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

