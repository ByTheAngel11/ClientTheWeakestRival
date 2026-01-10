using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using WPFTheWeakestRival.Helpers;

namespace WPFTheWeakestRival.Infrastructure
{
    public sealed class ProfileImageCache
    {
        private const int MAX_CACHE_ITEMS = 500;

        private readonly object syncRoot = new object();
        private readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<ImageSource>> inFlight = new Dictionary<string, Task<ImageSource>>(StringComparer.Ordinal);
        private readonly Queue<string> keys = new Queue<string>();

        public static ProfileImageCache Current { get; } = new ProfileImageCache();

        private ProfileImageCache()
        {
        }

        public Task<ImageSource> GetOrFetchAsync(int accountId, string profileImageCode, int decodeWidth, Func<Task<byte[]>> fetchBytesAsync)
        {
            string safeCode = (profileImageCode ?? string.Empty).Trim();
            string cacheKey = accountId.ToString() + "|" + safeCode + "|" + decodeWidth.ToString();

            lock (syncRoot)
            {
                if (cache.TryGetValue(cacheKey, out ImageSource cached))
                {
                    return Task.FromResult(cached);
                }

                if (inFlight.TryGetValue(cacheKey, out Task<ImageSource> running))
                {
                    return running;
                }

                Task<ImageSource> task = FetchAndCacheAsync(cacheKey, decodeWidth, fetchBytesAsync);
                inFlight[cacheKey] = task;
                return task;
            }
        }

        private async Task<ImageSource> FetchAndCacheAsync(string cacheKey, int decodeWidth, Func<Task<byte[]>> fetchBytesAsync)
        {
            try
            {
                byte[] bytes = fetchBytesAsync == null ? Array.Empty<byte>() : (await fetchBytesAsync().ConfigureAwait(true) ?? Array.Empty<byte>());

                ImageSource image = UiImageHelper.TryCreateFromBytes(bytes, decodeWidth);

                lock (syncRoot)
                {
                    inFlight.Remove(cacheKey);

                    if (image != null)
                    {
                        cache[cacheKey] = image;
                        keys.Enqueue(cacheKey);

                        while (keys.Count > MAX_CACHE_ITEMS)
                        {
                            string oldKey = keys.Dequeue();
                            cache.Remove(oldKey);
                        }
                    }
                }

                return image;
            }
            catch
            {
                lock (syncRoot)
                {
                    inFlight.Remove(cacheKey);
                }

                return null;
            }
        }
    }
}
