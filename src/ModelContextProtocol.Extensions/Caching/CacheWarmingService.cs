using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ModelContextProtocol.Extensions.Caching
{
    /// <summary>
    /// Implementation of cache warming service with predictive capabilities
    /// </summary>
    public class CacheWarmingService : ICacheWarmingService, IDisposable
    {
        private readonly IDistributedMcpCache _cache;
        private readonly ILogger<CacheWarmingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, ICacheWarmingStrategy> _strategies = new();
        private readonly ConcurrentDictionary<string, CacheWarmingReport> _activeOperations = new();
        private readonly ConcurrentDictionary<string, Timer> _scheduledWarmings = new();
        private readonly SemaphoreSlim _warmingSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
        private readonly Timer _cleanupTimer;
        private bool _predictiveWarmingEnabled;
        private TimeSpan _predictiveLookAhead = TimeSpan.FromHours(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheWarmingService"/> class
        /// </summary>
        public CacheWarmingService(
            IDistributedMcpCache cache,
            ILogger<CacheWarmingService> logger,
            IServiceProvider serviceProvider)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Clean up completed operations every 5 minutes
            _cleanupTimer = new Timer(CleanupCompletedOperations, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Register default strategies
            RegisterDefaultStrategies();
        }

        /// <inheritdoc />
        public async Task<CacheWarmingReport> WarmCacheAsync(string[] patterns, CancellationToken cancellationToken = default)
        {
            var report = new CacheWarmingReport
            {
                Patterns = patterns,
                StartedAt = DateTime.UtcNow,
                Status = CacheWarmingStatus.InProgress
            };

            _activeOperations[report.OperationId] = report;

            try
            {
                _logger.LogInformation("Starting cache warming for patterns: {Patterns}", string.Join(", ", patterns));

                var allKeys = new List<string>();
                
                // Collect all keys from all strategies
                foreach (var pattern in patterns)
                {
                    if (_strategies.TryGetValue(pattern, out var strategy))
                    {
                        var keys = await strategy.GetKeysToWarmAsync(pattern, cancellationToken);
                        allKeys.AddRange(keys);
                    }
                    else
                    {
                        // Use default pattern matching
                        var keys = await GetKeysFromPatternAsync(pattern, cancellationToken);
                        allKeys.AddRange(keys);
                    }
                }

                report.TotalKeys = allKeys.Count;
                var startTime = DateTime.UtcNow;

                // Warm cache in parallel batches
                var batchSize = Math.Max(1, Environment.ProcessorCount * 2);
                var batches = allKeys.Chunk(batchSize);

                await foreach (var batch in batches.ToAsyncEnumerable())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        report.Status = CacheWarmingStatus.Cancelled;
                        break;
                    }

                    var tasks = batch.Select(key => WarmSingleKeyAsync(key, report, cancellationToken));
                    await Task.WhenAll(tasks);
                }

                var endTime = DateTime.UtcNow;
                report.CompletedAt = endTime;
                report.Status = report.Status == CacheWarmingStatus.Cancelled ? 
                    CacheWarmingStatus.Cancelled : CacheWarmingStatus.Completed;

                // Calculate metrics
                report.Metrics.TotalTime = endTime - startTime;
                report.Metrics.AverageTimePerKey = report.TotalKeys > 0 ? 
                    TimeSpan.FromTicks(report.Metrics.TotalTime.Ticks / report.TotalKeys) : TimeSpan.Zero;
                report.Metrics.ThroughputPerSecond = report.Metrics.TotalTime.TotalSeconds > 0 ? 
                    report.TotalKeys / report.Metrics.TotalTime.TotalSeconds : 0;

                _logger.LogInformation("Cache warming completed. Warmed {WarmedKeys}/{TotalKeys} keys in {Duration}ms",
                    report.WarmedKeys, report.TotalKeys, report.Metrics.TotalTime.TotalMilliseconds);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache warming failed for operation {OperationId}", report.OperationId);
                report.Status = CacheWarmingStatus.Failed;
                report.Errors.Add(ex.Message);
                report.CompletedAt = DateTime.UtcNow;
                return report;
            }
        }

        /// <inheritdoc />
        public Task<CacheWarmingReport> GetWarmingStatusAsync()
        {
            // Return the most recent active operation or create a summary
            var activeOp = _activeOperations.Values
                .Where(op => op.Status == CacheWarmingStatus.InProgress)
                .OrderByDescending(op => op.StartedAt)
                .FirstOrDefault();

            if (activeOp != null)
            {
                return Task.FromResult(activeOp);
            }

            // Create summary of recent operations
            var recentOps = _activeOperations.Values
                .Where(op => op.StartedAt > DateTime.UtcNow.AddHours(-1))
                .ToArray();

            var summary = new CacheWarmingReport
            {
                OperationId = "summary",
                Status = CacheWarmingStatus.Completed,
                StartedAt = recentOps.Length > 0 ? recentOps.Min(op => op.StartedAt) : DateTime.UtcNow,
                CompletedAt = recentOps.Length > 0 ? recentOps.Max(op => op.CompletedAt ?? DateTime.UtcNow) : DateTime.UtcNow,
                TotalKeys = recentOps.Sum(op => op.TotalKeys),
                WarmedKeys = recentOps.Sum(op => op.WarmedKeys),
                FailedKeys = recentOps.Sum(op => op.FailedKeys)
            };

            return Task.FromResult(summary);
        }

        /// <inheritdoc />
        public void RegisterWarmingStrategy(string pattern, ICacheWarmingStrategy strategy)
        {
            _strategies[pattern] = strategy;
            _logger.LogDebug("Registered warming strategy for pattern: {Pattern}", pattern);
        }

        /// <inheritdoc />
        public async Task EnablePredictiveWarmingAsync(bool enabled, TimeSpan lookAheadWindow = default)
        {
            _predictiveWarmingEnabled = enabled;
            if (lookAheadWindow != default)
            {
                _predictiveLookAhead = lookAheadWindow;
            }

            _logger.LogInformation("Predictive cache warming {Status} with look-ahead window: {Window}",
                enabled ? "enabled" : "disabled", _predictiveLookAhead);

            if (enabled)
            {
                // Start predictive warming background task
                _ = Task.Run(async () => await RunPredictiveWarmingAsync());
            }
        }

        /// <inheritdoc />
        public Task ScheduleWarmingAsync(string[] patterns, TimeSpan interval, CancellationToken cancellationToken = default)
        {
            var scheduleId = string.Join("|", patterns);
            
            // Cancel existing schedule if it exists
            if (_scheduledWarmings.TryRemove(scheduleId, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new scheduled warming
            var timer = new Timer(async _ =>
            {
                try
                {
                    await WarmCacheAsync(patterns, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled cache warming failed for patterns: {Patterns}", 
                        string.Join(", ", patterns));
                }
            }, null, interval, interval);

            _scheduledWarmings[scheduleId] = timer;

            _logger.LogInformation("Scheduled cache warming for patterns {Patterns} every {Interval}",
                string.Join(", ", patterns), interval);

            return Task.CompletedTask;
        }

        private async Task<string[]> GetKeysFromPatternAsync(string pattern, CancellationToken cancellationToken)
        {
            // This is a simplified implementation - in practice, you'd query your cache or data source
            // to find keys matching the pattern
            var keys = new List<string>();
            
            // For demo purposes, generate some sample keys
            if (pattern.Contains("*"))
            {
                var basePattern = pattern.Replace("*", "");
                for (int i = 1; i <= 10; i++)
                {
                    keys.Add($"{basePattern}{i}");
                }
            }
            else
            {
                keys.Add(pattern);
            }

            return keys.ToArray();
        }

        private async Task WarmSingleKeyAsync(string key, CacheWarmingReport report, CancellationToken cancellationToken)
        {
            await _warmingSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check if key is already cached
                var existingValue = await _cache.GetAsync<object>(key, cancellationToken);
                if (existingValue != null)
                {
                    Interlocked.Increment(ref report.WarmedKeys);
                    return;
                }

                // Find appropriate strategy to generate data
                var strategy = _strategies.Values.FirstOrDefault();
                if (strategy != null)
                {
                    var data = await strategy.GenerateDataAsync(key, cancellationToken);
                    await _cache.SetAsync(key, data, new CacheOptions
                    {
                        SlidingExpiration = TimeSpan.FromHours(1)
                    }, cancellationToken);
                    
                    Interlocked.Increment(ref report.WarmedKeys);
                }
                else
                {
                    // Generate default data
                    var defaultData = new { key, warmedAt = DateTime.UtcNow, source = "cache-warming" };
                    await _cache.SetAsync(key, defaultData, new CacheOptions
                    {
                        SlidingExpiration = TimeSpan.FromHours(1)
                    }, cancellationToken);
                    
                    Interlocked.Increment(ref report.WarmedKeys);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm cache key: {Key}", key);
                Interlocked.Increment(ref report.FailedKeys);
                report.Errors.Add($"Key {key}: {ex.Message}");
            }
            finally
            {
                _warmingSemaphore.Release();
            }
        }

        private async Task RunPredictiveWarmingAsync()
        {
            while (_predictiveWarmingEnabled)
            {
                try
                {
                    var predictiveStrategies = _strategies.Values.OfType<IPredictiveCacheWarmingStrategy>();
                    var allPredictedKeys = new List<string>();

                    foreach (var strategy in predictiveStrategies)
                    {
                        var predictedKeys = await strategy.PredictKeysToWarmAsync(_predictiveLookAhead);
                        allPredictedKeys.AddRange(predictedKeys);
                    }

                    if (allPredictedKeys.Count > 0)
                    {
                        _logger.LogDebug("Predictive warming identified {Count} keys to warm", allPredictedKeys.Count);
                        await WarmCacheAsync(new[] { string.Join("|", allPredictedKeys) });
                    }

                    // Wait before next prediction cycle
                    await Task.Delay(TimeSpan.FromMinutes(15));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Predictive cache warming failed");
                    await Task.Delay(TimeSpan.FromMinutes(5)); // Shorter delay on error
                }
            }
        }

        private void RegisterDefaultStrategies()
        {
            // Register a simple default strategy
            RegisterWarmingStrategy("default", new DefaultCacheWarmingStrategy());
        }

        private void CleanupCompletedOperations(object state)
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            var toRemove = _activeOperations
                .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var key in toRemove)
            {
                _activeOperations.TryRemove(key, out _);
            }

            if (toRemove.Length > 0)
            {
                _logger.LogDebug("Cleaned up {Count} completed cache warming operations", toRemove.Length);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _warmingSemaphore?.Dispose();
            
            foreach (var timer in _scheduledWarmings.Values)
            {
                timer?.Dispose();
            }
            _scheduledWarmings.Clear();
        }
    }

    /// <summary>
    /// Default cache warming strategy
    /// </summary>
    public class DefaultCacheWarmingStrategy : ICacheWarmingStrategy
    {
        public Task<string[]> GetKeysToWarmAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // Simple pattern matching - in practice, this would query your data source
            var keys = new List<string>();
            
            if (pattern.Contains("*"))
            {
                var basePattern = pattern.Replace("*", "");
                for (int i = 1; i <= 5; i++)
                {
                    keys.Add($"{basePattern}{i}");
                }
            }
            else
            {
                keys.Add(pattern);
            }

            return Task.FromResult(keys.ToArray());
        }

        public Task<object> GenerateDataAsync(string key, CancellationToken cancellationToken = default)
        {
            // Generate sample data for the key
            var data = new
            {
                key,
                data = $"Sample data for {key}",
                generatedAt = DateTime.UtcNow,
                source = "default-strategy"
            };

            return Task.FromResult<object>(data);
        }

        public Task<string[]> PredictKeysToWarmAsync(TimeSpan lookAhead, CancellationToken cancellationToken = default)
        {
            // Simple prediction - in practice, this would use ML models
            var predictedKeys = new[]
            {
                $"predicted:key:{DateTime.UtcNow.Hour}",
                $"predicted:key:{DateTime.UtcNow.DayOfWeek}",
                $"predicted:key:common"
            };

            return Task.FromResult(predictedKeys);
        }
    }
}
