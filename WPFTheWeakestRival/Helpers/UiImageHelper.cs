using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFTheWeakestRival.Helpers
{
    public static class UiImageHelper
    {
        public static ImageSource TryCreateFromUrlOrPath(string sourcePathOrUri, int decodeWidth = 24)
        {
            if (string.IsNullOrWhiteSpace(sourcePathOrUri))
            {
                return null;
            }

            try
            {
                Uri candidateUri;
                if (!Uri.TryCreate(sourcePathOrUri, UriKind.RelativeOrAbsolute, out candidateUri))
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
                    string uriScheme = candidateUri.Scheme.ToLowerInvariant();
                    bool isAllowedScheme =
                        uriScheme == Uri.UriSchemeHttp ||
                        uriScheme == Uri.UriSchemeHttps ||
                        uriScheme == Uri.UriSchemeFile ||
                        uriScheme == "pack";

                    if (!isAllowedScheme)
                    {
                        return null;
                    }

                    resolvedUri = candidateUri;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (decodeWidth > 0)
                {
                    bitmap.DecodePixelWidth = decodeWidth;
                }
                bitmap.UriSource = resolvedUri;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (UriFormatException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
        }

        public static ImageSource TryCreateFromBytes(byte[] imageBytes, int decodeWidth = 24)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            try
            {
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
        }

        public static ImageSource DefaultAvatar(int decodeWidth = 24)
        {
            try
            {
                var avatarPackUri = new Uri("pack://application:,,,/Assets/Images/Avatars/default.png", UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (decodeWidth > 0)
                {
                    bitmap.DecodePixelWidth = decodeWidth;
                }
                bitmap.UriSource = avatarPackUri;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
        }
    }
}
