using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFTheWeakestRival.Helpers
{
    public static class UiImageHelper
    {
        private const string DEFAULT_AVATAR_PACK_URI =
            "pack://application:,,,/Assets/Images/Avatars/default.png";

        private const string PROFILE_IMAGES_PACK_URI_FORMAT =
            "pack://application:,,,/Assets/Images/Profiles/{0}.png";

        public static ImageSource TryCreateFromProfileCode(string profileImageCode, int decodeWidth = 0)
        {
            string safeCode = (profileImageCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeCode))
            {
                return null;
            }

            if (Uri.TryCreate(safeCode, UriKind.RelativeOrAbsolute, out _))
            {
                ImageSource fromUri = TryCreateFromUrlOrPath(safeCode, decodeWidth);
                if (fromUri != null)
                {
                    return fromUri;
                }
            }

            string packUri = string.Format(PROFILE_IMAGES_PACK_URI_FORMAT, safeCode);
            return TryCreateFromUrlOrPath(packUri, decodeWidth);
        }

        public static ImageSource TryCreateFromUrlOrPath(string sourcePathOrUri, int decodeWidth = 0)
        {
            if (string.IsNullOrWhiteSpace(sourcePathOrUri))
            {
                return null;
            }

            try
            {
                if (!Uri.TryCreate(sourcePathOrUri, UriKind.RelativeOrAbsolute, out Uri candidateUri))
                {
                    return null;
                }

                Uri resolvedUri;

                if (!candidateUri.IsAbsoluteUri)
                {
                    string applicationBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string absoluteFilePath = Path.Combine(applicationBaseDirectory, sourcePathOrUri);

                    if (!File.Exists(absoluteFilePath))
                    {
                        return null;
                    }

                    resolvedUri = new Uri(absoluteFilePath, UriKind.Absolute);
                }
                else
                {
                    string uriScheme = (candidateUri.Scheme ?? string.Empty).ToLowerInvariant();

                    bool isAllowedScheme =
                        string.Equals(uriScheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
                        string.Equals(uriScheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
                        string.Equals(uriScheme, Uri.UriSchemeFile, StringComparison.Ordinal) ||
                        string.Equals(uriScheme, "pack", StringComparison.Ordinal);

                    if (!isAllowedScheme)
                    {
                        return null;
                    }

                    resolvedUri = candidateUri;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                if (decodeWidth > 0)
                {
                    bitmap.DecodePixelWidth = decodeWidth;
                }

                bitmap.UriSource = resolvedUri;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ImageSource TryCreateFromBytes(byte[] imageBytes, int decodeWidth = 0)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    memoryStream.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    if (decodeWidth > 0)
                    {
                        bitmap.DecodePixelWidth = decodeWidth;
                    }

                    bitmap.StreamSource = memoryStream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    return bitmap;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ImageSource DefaultAvatar(int decodeWidth = 24)
        {
            return TryCreateFromUrlOrPath(DEFAULT_AVATAR_PACK_URI, decodeWidth);
        }
    }
}
