using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Caching
{
    /// <summary>
    /// Service for warming cache with predictive data loading
    /// </summary>
    public interface ICacheWarmingService
    {
        /// <summary>
        /// Warms cache with data matching the specified patterns
        /// </summary>
        /// <param name="patterns">Cache key patterns to warm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache warming report</returns>
        Task<CacheWarmingReport> WarmCacheAsync(string[] patterns, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of cache warming operations
        /// </summary>
        /// <returns>Cache warming status report</returns>
        Task<CacheWarmingReport> GetWarmingStatusAsync();

        /// <summary>
        /// Registers a warming strategy for specific patterns
        /// </summary>
        /// <param name="pattern">Cache key pattern</param>
        /// <param name="strategy">Warming strategy</param>
        void RegisterWarmingStrategy(string pattern, ICacheWarmingStrategy strategy);

        /// <summary>
        /// Enables predictive cache warming based on usage patterns
        /// </summary>
        /// <param name="enabled">Whether to enable predictive warming</param>
        /// <param name="lookAheadWindow">How far ahead to predict</param>
        Task EnablePredictiveWarmingAsync(bool enabled, TimeSpan lookAheadWindow = default);

        /// <summary>
        /// Schedules cache warming at specified intervals
        /// </summary>
        /// <param name="patterns">Patterns to warm</param>
        /// <param name="interval">Warming interval</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ScheduleWarmingAsync(string[] patterns, TimeSpan interval, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Strategy for warming specific cache patterns
    /// </summary>
    public interface ICacheWarmingStrategy
    {
        /// <summary>
        /// Gets the cache keys that should be warmed for this strategy
        /// </summary>
        /// <param name="pattern">Pattern to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache keys to warm</returns>
        Task<string[]> GetKeysToWarmAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates the data for a specific cache key
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Data to cache</returns>
        Task<object> GenerateDataAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Predicts what keys will be needed based on usage patterns
        /// </summary>
        /// <param name="lookAhead">How far ahead to predict</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Predicted cache keys</returns>
        Task<string[]> PredictKeysToWarmAsync(TimeSpan lookAhead, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Predictive cache warming strategy using machine learning patterns
    /// </summary>
    public interface IPredictiveCacheWarmingStrategy : ICacheWarmingStrategy
    {
        /// <summary>
        /// Trains the prediction model with historical usage data
        /// </summary>
        /// <param name="usageData">Historical cache usage data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task TrainPredictionModelAsync(CacheUsageData[] usageData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the confidence score for a prediction
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="timeWindow">Time window for prediction</param>
        /// <returns>Confidence score (0-1)</returns>
        Task<double> GetPredictionConfidenceAsync(string key, TimeSpan timeWindow);

        /// <summary>
        /// Updates the model with real-time usage data
        /// </summary>
        /// <param name="usageEvent">Cache usage event</param>
        Task UpdateModelAsync(CacheUsageEvent usageEvent);
    }

    /// <summary>
    /// Report of cache warming operations
    /// </summary>
    public class CacheWarmingReport
    {
        /// <summary>
        /// Gets or sets the warming operation ID
        /// </summary>
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when the warming started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the warming completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the current status
        /// </summary>
        public CacheWarmingStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the patterns being warmed
        /// </summary>
        public string[] Patterns { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the total number of keys to warm
        /// </summary>
        public int TotalKeys { get; set; }

        /// <summary>
        /// Gets or sets the number of keys successfully warmed
        /// </summary>
        public int WarmedKeys { get; set; }

        /// <summary>
        /// Gets or sets the number of keys that failed to warm
        /// </summary>
        public int FailedKeys { get; set; }

        /// <summary>
        /// Gets or sets the warming progress (0-1)
        /// </summary>
        public double Progress => TotalKeys > 0 ? (double)(WarmedKeys + FailedKeys) / TotalKeys : 0;

        /// <summary>
        /// Gets or sets any errors that occurred
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets performance metrics
        /// </summary>
        public CacheWarmingMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Status of cache warming operations
    /// </summary>
    public enum CacheWarmingStatus
    {
        /// <summary>
        /// Warming is pending
        /// </summary>
        Pending,

        /// <summary>
        /// Warming is in progress
        /// </summary>
        InProgress,

        /// <summary>
        /// Warming completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Warming failed
        /// </summary>
        Failed,

        /// <summary>
        /// Warming was cancelled
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Performance metrics for cache warming
    /// </summary>
    public class CacheWarmingMetrics
    {
        /// <summary>
        /// Gets or sets the total time taken
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        /// <summary>
        /// Gets or sets the average time per key
        /// </summary>
        public TimeSpan AverageTimePerKey { get; set; }

        /// <summary>
        /// Gets or sets the warming throughput (keys per second)
        /// </summary>
        public double ThroughputPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the memory used during warming
        /// </summary>
        public long MemoryUsedBytes { get; set; }

        /// <summary>
        /// Gets or sets the cache hit rate after warming
        /// </summary>
        public double CacheHitRateAfterWarming { get; set; }
    }

    /// <summary>
    /// Historical cache usage data for training prediction models
    /// </summary>
    public class CacheUsageData
    {
        /// <summary>
        /// Gets or sets the cache key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets when the key was accessed
        /// </summary>
        public DateTime AccessedAt { get; set; }

        /// <summary>
        /// Gets or sets the access frequency
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Gets or sets the time of day pattern
        /// </summary>
        public TimeSpan TimeOfDay { get; set; }

        /// <summary>
        /// Gets or sets the day of week pattern
        /// </summary>
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>
        /// Gets or sets additional context data
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Real-time cache usage event
    /// </summary>
    public class CacheUsageEvent
    {
        /// <summary>
        /// Gets or sets the cache key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the event type
        /// </summary>
        public CacheEventType EventType { get; set; }

        /// <summary>
        /// Gets or sets when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the user or session context
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Types of cache events
    /// </summary>
    public enum CacheEventType
    {
        /// <summary>
        /// Cache hit
        /// </summary>
        Hit,

        /// <summary>
        /// Cache miss
        /// </summary>
        Miss,

        /// <summary>
        /// Cache set
        /// </summary>
        Set,

        /// <summary>
        /// Cache eviction
        /// </summary>
        Evicted,

        /// <summary>
        /// Cache expiration
        /// </summary>
        Expired
    }
}
