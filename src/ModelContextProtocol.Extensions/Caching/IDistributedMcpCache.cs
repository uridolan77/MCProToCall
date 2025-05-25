using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Caching
{
    /// <summary>
    /// Distributed caching abstraction for MCP operations
    /// </summary>
    public interface IDistributedMcpCache
    {
        /// <summary>
        /// Gets a cached value
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a cached value with optional expiry
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a cached value
        /// </summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache entries matching a pattern
        /// </summary>
        Task InvalidateAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to acquire a distributed lock
        /// </summary>
        Task<IDistributedLock?> TryLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or sets a cached value using a factory function
        /// </summary>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Distributed lock interface
    /// </summary>
    public interface IDistributedLock : IDisposable
    {
        /// <summary>
        /// The key that is locked
        /// </summary>
        string Key { get; }

        /// <summary>
        /// When the lock expires
        /// </summary>
        DateTime ExpiresAt { get; }

        /// <summary>
        /// Extends the lock expiry time
        /// </summary>
        Task<bool> ExtendAsync(TimeSpan additionalTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the lock
        /// </summary>
        Task ReleaseAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public long HitCount;
        public long MissCount;
        public long SetCount;
        public long RemoveCount;
        public long InvalidateCount;
        public double HitRatio => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cache invalidation service for coordinated cache management
    /// </summary>
    public interface ICacheInvalidationService
    {
        /// <summary>
        /// Invalidates cache entries across all cache layers
        /// </summary>
        Task InvalidateAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cache entries by tags
        /// </summary>
        Task InvalidateByTagsAsync(string[] tags, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to cache invalidation events
        /// </summary>
        Task SubscribeToInvalidationEventsAsync(Func<string, Task> onInvalidation, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Cache entry with metadata
    /// </summary>
    public class CacheEntry<T>
    {
        public T Value { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public int Version { get; set; }
        public string ETag { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cache options for fine-grained control
    /// </summary>
    public class CacheOptions
    {
        public TimeSpan? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public CachePriority Priority { get; set; } = CachePriority.Normal;
        public bool CompressValue { get; set; } = false;
        public bool EncryptValue { get; set; } = false;
    }

    /// <summary>
    /// Cache priority levels
    /// </summary>
    public enum CachePriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}
