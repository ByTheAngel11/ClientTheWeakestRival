using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFTheWeakestRival.Helpers
{
    public static class UiImageHelper
    {
        public static ImageSource TryCreateFromUrlOrPath(string urlOrPath, int decodePixelWidth = 24)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

            try
            {
                // Acepta http/https/file/pack y rutas absolutas
                if (Uri.TryCreate(urlOrPath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    if (!uri.IsAbsoluteUri)
                    {
                        // Si vino como ruta relativa, resuélvela contra el bin
                        var full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, urlOrPath);
                        if (!File.Exists(full)) return null;
                        uri = new Uri(full, UriKind.Absolute);
                    }

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;   // no bloquear archivo
                    if (decodePixelWidth > 0) bi.DecodePixelWidth = decodePixelWidth;
                    bi.UriSource = uri;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
                // swallow
            }

            return null;
        }

        public static ImageSource TryCreateFromBytes(byte[] bytes, int decodePixelWidth = 24)
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    if (decodePixelWidth > 0) bi.DecodePixelWidth = decodePixelWidth;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
                return null;
            }
        }

        public static ImageSource DefaultAvatar(int decodePixelWidth = 24)
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Images/Avatars/default.png", UriKind.Absolute);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                if (decodePixelWidth > 0) bi.DecodePixelWidth = decodePixelWidth;
                bi.UriSource = uri;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }
    }
}
