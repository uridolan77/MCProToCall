using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Response cache for frequently accessed resources
    /// </summary>
    public class ResponseCache
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _defaultExpiration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseCache"/> class
        /// </summary>
        /// <param name="defaultExpiration">Default cache expiration</param>
        public ResponseCache(TimeSpan defaultExpiration)
        {
            _defaultExpiration = defaultExpiration;
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 1000, // Maximum number of cached entries
                CompactionPercentage = 0.25
            });
        }

        /// <summary>
        /// Gets a value from the cache or adds it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create the value if it doesn't exist</param>
        /// <param name="expiration">Custom expiration time</param>
        /// <returns>Cached or newly created value</returns>
        public async Task<T> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return (T)cached;
            }

            var result = await factory();
            
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = expiration ?? _defaultExpiration,
                Size = 1
            };

            _cache.Set(key, result, cacheEntryOptions);

            return result;
        }

        /// <summary>
        /// Invalidates a cache entry
        /// </summary>
        /// <param name="key">Cache key</param>
        public void Invalidate(string key)
        {
            _cache.Remove(key);
        }

        /// <summary>
        /// Clears the entire cache
        /// </summary>
        public void Clear()
        {
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }
        }
    }
}
