using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Caching
{
    /// <summary>
    /// Multi-tier caching implementation with L1 (memory) and L2 (distributed) cache layers
    /// </summary>
    public class MultiTierMcpCache : IDistributedMcpCache, IDisposable
    {
        private readonly IMemoryCache _l1Cache;
        private readonly IDistributedCache _l2Cache;
        private readonly ICacheInvalidationService _invalidationService;
        private readonly ILogger<MultiTierMcpCache> _logger;
        private readonly MultiTierCacheOptions _options;
        private readonly CacheStatistics _statistics = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockSemaphores = new();
        private readonly Timer _statsTimer;

        public MultiTierMcpCache(
            IMemoryCache l1Cache,
            IDistributedCache l2Cache,
            ICacheInvalidationService invalidationService,
            ILogger<MultiTierMcpCache> logger,
            IOptions<MultiTierCacheOptions> options)
        {
            _l1Cache = l1Cache ?? throw new ArgumentNullException(nameof(l1Cache));
            _l2Cache = l2Cache ?? throw new ArgumentNullException(nameof(l2Cache));
            _invalidationService = invalidationService ?? throw new ArgumentNullException(nameof(invalidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Subscribe to invalidation events
            _ = Task.Run(async () => await _invalidationService.SubscribeToInvalidationEventsAsync(OnCacheInvalidation));

            // Start statistics timer
            _statsTimer = new Timer(LogStatistics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var fullKey = GetFullKey<T>(key);

            // Try L1 cache first
            if (_l1Cache.TryGetValue(fullKey, out var l1Value))
            {
                Interlocked.Increment(ref _statistics.HitCount);
                _logger.LogDebug("Cache hit (L1) for key: {Key}", key);
                return (T)l1Value!;
            }

            // Try L2 cache
            try
            {
                var l2Bytes = await _l2Cache.GetAsync(fullKey, cancellationToken);
                if (l2Bytes != null)
                {
                    var l2Value = JsonSerializer.Deserialize<T>(l2Bytes);

                    // Promote to L1 cache
                    var l1Options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.L1CacheExpiry,
                        Priority = CacheItemPriority.Normal
                    };
                    _l1Cache.Set(fullKey, l2Value, l1Options);

                    Interlocked.Increment(ref _statistics.HitCount);
                    _logger.LogDebug("Cache hit (L2) for key: {Key}, promoted to L1", key);
                    return l2Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving from L2 cache for key: {Key}", key);
            }

            Interlocked.Increment(ref _statistics.MissCount);
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var fullKey = GetFullKey<T>(key);
            var effectiveExpiry = expiry ?? _options.DefaultExpiry;

            // Set in L1 cache
            var l1Options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(Math.Min(effectiveExpiry.TotalMilliseconds, _options.L1CacheExpiry.TotalMilliseconds)),
                Priority = CacheItemPriority.Normal
            };
            _l1Cache.Set(fullKey, value, l1Options);

            // Set in L2 cache
            try
            {
                var serializedValue = JsonSerializer.SerializeToUtf8Bytes(value);
                var l2Options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = effectiveExpiry
                };
                await _l2Cache.SetAsync(fullKey, serializedValue, l2Options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error setting L2 cache for key: {Key}", key);
            }

            Interlocked.Increment(ref _statistics.SetCount);
            _logger.LogDebug("Cache set for key: {Key} with expiry: {Expiry}", key, effectiveExpiry);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var fullKey = GetFullKey<object>(key);

            // Remove from L1 cache
            _l1Cache.Remove(fullKey);

            // Remove from L2 cache
            try
            {
                await _l2Cache.RemoveAsync(fullKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing from L2 cache for key: {Key}", key);
            }

            Interlocked.Increment(ref _statistics.RemoveCount);
            _logger.LogDebug("Cache remove for key: {Key}", key);
        }

        public async Task InvalidateAsync(string pattern, CancellationToken cancellationToken = default)
        {
            await _invalidationService.InvalidateAsync(pattern, cancellationToken);
            Interlocked.Increment(ref _statistics.InvalidateCount);
            _logger.LogInformation("Cache invalidation requested for pattern: {Pattern}", pattern);
        }

        public async Task<IDistributedLock?> TryLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            var lockKey = $"lock:{key}";
            var lockValue = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.Add(expiry);

            try
            {
                // Try to acquire lock in L2 cache
                var lockData = JsonSerializer.SerializeToUtf8Bytes(new { Value = lockValue, ExpiresAt = expiresAt });
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };

                // This is a simplified implementation - in production, use Redis with SET NX EX
                var existing = await _l2Cache.GetAsync(lockKey, cancellationToken);
                if (existing == null)
                {
                    await _l2Cache.SetAsync(lockKey, lockData, options, cancellationToken);
                    return new DistributedLock(lockKey, lockValue, expiresAt, _l2Cache, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error acquiring distributed lock for key: {Key}", key);
            }

            return null;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
                return cached;

            // Use semaphore to prevent cache stampede
            var semaphore = _lockSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                cached = await GetAsync<T>(key, cancellationToken);
                if (cached != null)
                    return cached;

                // Generate value and cache it
                var value = await factory();
                await SetAsync(key, value, expiry, cancellationToken);
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            var fullKey = GetFullKey<object>(key);

            // Check L1 cache
            if (_l1Cache.TryGetValue(fullKey, out _))
                return true;

            // Check L2 cache
            try
            {
                var l2Value = await _l2Cache.GetAsync(fullKey, cancellationToken);
                return l2Value != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking existence in L2 cache for key: {Key}", key);
                return false;
            }
        }

        public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_statistics);
        }

        private string GetFullKey<T>(string key)
        {
            return $"{_options.KeyPrefix}:{typeof(T).Name}:{key}";
        }

        private async Task OnCacheInvalidation(string pattern)
        {
            try
            {
                // Simple pattern matching - in production, use more sophisticated pattern matching
                if (pattern.Contains("*"))
                {
                    // For memory cache, we'd need to track keys or use a more sophisticated approach
                    _logger.LogInformation("Cache invalidation pattern received: {Pattern}", pattern);
                }
                else
                {
                    // Exact key invalidation
                    _l1Cache.Remove(pattern);
                    await _l2Cache.RemoveAsync(pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cache invalidation for pattern: {Pattern}", pattern);
            }
        }

        private void LogStatistics(object? state)
        {
            _logger.LogInformation("Cache Statistics - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, Sets: {Sets}, Removes: {Removes}",
                _statistics.HitCount, _statistics.MissCount, _statistics.HitRatio, _statistics.SetCount, _statistics.RemoveCount);
        }

        public void Dispose()
        {
            _statsTimer?.Dispose();
            foreach (var semaphore in _lockSemaphores.Values)
            {
                semaphore?.Dispose();
            }
            _lockSemaphores.Clear();
        }
    }

    /// <summary>
    /// Distributed lock implementation
    /// </summary>
    internal class DistributedLock : IDistributedLock
    {
        private readonly string _key;
        private readonly string _value;
        private readonly IDistributedCache _cache;
        private readonly ILogger _logger;
        private bool _disposed;

        public string Key { get; }
        public DateTime ExpiresAt { get; }

        public DistributedLock(string key, string value, DateTime expiresAt, IDistributedCache cache, ILogger logger)
        {
            _key = key;
            _value = value;
            Key = key;
            ExpiresAt = expiresAt;
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> ExtendAsync(TimeSpan additionalTime, CancellationToken cancellationToken = default)
        {
            if (_disposed) return false;

            try
            {
                var newExpiresAt = ExpiresAt.Add(additionalTime);
                var lockData = JsonSerializer.SerializeToUtf8Bytes(new { Value = _value, ExpiresAt = newExpiresAt });
                var options = new DistributedCacheEntryOptions { AbsoluteExpiration = newExpiresAt };

                await _cache.SetAsync(_key, lockData, options, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extend distributed lock: {Key}", _key);
                return false;
            }
        }

        public async Task ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            try
            {
                await _cache.RemoveAsync(_key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release distributed lock: {Key}", _key);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await ReleaseAsync());
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Configuration options for multi-tier cache
    /// </summary>
    public class MultiTierCacheOptions
    {
        public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan L1CacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
        public string KeyPrefix { get; set; } = "mcp";
        public bool EnableCompression { get; set; } = false;
        public bool EnableEncryption { get; set; } = false;
    }
}
