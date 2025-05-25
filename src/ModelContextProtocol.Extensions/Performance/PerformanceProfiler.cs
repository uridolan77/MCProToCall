using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// High-performance profiler for tracking operation performance
    /// </summary>
    public class PerformanceProfiler : IDisposable
    {
        private readonly ConcurrentDictionary<string, OperationProfile> _activeProfiles;
        private readonly ConcurrentQueue<CompletedOperationProfile> _completedProfiles;
        private readonly ILogger<PerformanceProfiler> _logger;
        private readonly Timer _cleanupTimer;
        private readonly PerformanceProfilerOptions _options;
        private bool _disposed;

        public PerformanceProfiler(
            PerformanceProfilerOptions options,
            ILogger<PerformanceProfiler> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _activeProfiles = new ConcurrentDictionary<string, OperationProfile>();
            _completedProfiles = new ConcurrentQueue<CompletedOperationProfile>();

            // Setup cleanup timer to prevent memory leaks
            _cleanupTimer = new Timer(CleanupExpiredProfiles, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Starts profiling an operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="tags">Optional tags for categorization</param>
        /// <returns>Disposable scope that ends profiling when disposed</returns>
        public IDisposable StartOperation(string operationName, Dictionary<string, object>? tags = null)
        {
            ThrowIfDisposed();

            var profile = new OperationProfile(operationName, tags);
            _activeProfiles.TryAdd(profile.Id, profile);

            _logger.LogDebug("Started profiling operation {OperationName} with ID {ProfileId}", 
                operationName, profile.Id);

            return new OperationScope(profile, CompleteOperation);
        }

        /// <summary>
        /// Starts profiling an async operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="tags">Optional tags for categorization</param>
        /// <returns>Disposable scope that ends profiling when disposed</returns>
        public IAsyncDisposable StartAsyncOperation(string operationName, Dictionary<string, object>? tags = null)
        {
            ThrowIfDisposed();

            var profile = new OperationProfile(operationName, tags);
            _activeProfiles.TryAdd(profile.Id, profile);

            _logger.LogDebug("Started profiling async operation {OperationName} with ID {ProfileId}", 
                operationName, profile.Id);

            return new AsyncOperationScope(profile, CompleteOperation);
        }

        /// <summary>
        /// Generates a comprehensive performance report
        /// </summary>
        /// <param name="timeWindow">Time window for the report (null for all data)</param>
        /// <returns>Performance report</returns>
        public PerformanceReport GenerateReport(TimeSpan? timeWindow = null)
        {
            ThrowIfDisposed();

            var cutoffTime = timeWindow.HasValue ? DateTime.UtcNow - timeWindow.Value : DateTime.MinValue;
            var relevantProfiles = _completedProfiles
                .Where(p => p.CompletedAt >= cutoffTime)
                .ToList();

            var operations = relevantProfiles
                .GroupBy(p => p.Name)
                .Select(g => new OperationSummary
                {
                    Name = g.Key,
                    Count = g.Count(),
                    TotalDuration = TimeSpan.FromTicks(g.Sum(p => p.Duration.Ticks)),
                    AverageDuration = TimeSpan.FromTicks((long)g.Average(p => p.Duration.Ticks)),
                    MinDuration = g.Min(p => p.Duration),
                    MaxDuration = g.Max(p => p.Duration),
                    P50Duration = Percentile(g.Select(p => p.Duration), 0.50),
                    P95Duration = Percentile(g.Select(p => p.Duration), 0.95),
                    P99Duration = Percentile(g.Select(p => p.Duration), 0.99),
                    ErrorCount = g.Count(p => p.HasError),
                    ErrorRate = (double)g.Count(p => p.HasError) / g.Count(),
                    Tags = g.SelectMany(p => p.Tags ?? new Dictionary<string, object>())
                           .GroupBy(kvp => kvp.Key)
                           .ToDictionary(grp => grp.Key, grp => grp.Select(kvp => kvp.Value).Distinct().ToList())
                })
                .OrderByDescending(o => o.Count)
                .ToList();

            return new PerformanceReport
            {
                GeneratedAt = DateTime.UtcNow,
                TimeWindow = timeWindow,
                TotalOperations = relevantProfiles.Count,
                UniqueOperationTypes = operations.Count,
                Operations = operations,
                ActiveOperations = _activeProfiles.Count,
                SystemMetrics = CollectSystemMetrics()
            };
        }

        /// <summary>
        /// Gets real-time performance metrics
        /// </summary>
        /// <returns>Current performance metrics</returns>
        public PerformanceMetrics GetCurrentMetrics()
        {
            ThrowIfDisposed();

            var recentProfiles = _completedProfiles
                .Where(p => p.CompletedAt >= DateTime.UtcNow - TimeSpan.FromMinutes(5))
                .ToList();

            return new PerformanceMetrics
            {
                ActiveOperations = _activeProfiles.Count,
                CompletedOperationsLast5Min = recentProfiles.Count,
                AverageResponseTimeLast5Min = recentProfiles.Any() 
                    ? TimeSpan.FromTicks((long)recentProfiles.Average(p => p.Duration.Ticks))
                    : TimeSpan.Zero,
                ErrorRateLast5Min = recentProfiles.Any() 
                    ? (double)recentProfiles.Count(p => p.HasError) / recentProfiles.Count
                    : 0.0,
                ThroughputPerSecond = recentProfiles.Count / 300.0, // 5 minutes = 300 seconds
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
            };
        }

        /// <summary>
        /// Clears all profiling data
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();

            _activeProfiles.Clear();
            while (_completedProfiles.TryDequeue(out _)) { }

            _logger.LogInformation("Performance profiler data cleared");
        }

        private void CompleteOperation(OperationProfile profile)
        {
            if (_activeProfiles.TryRemove(profile.Id, out _))
            {
                var completedProfile = new CompletedOperationProfile
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    StartedAt = profile.StartedAt,
                    CompletedAt = DateTime.UtcNow,
                    Duration = profile.Stopwatch.Elapsed,
                    HasError = profile.HasError,
                    ErrorMessage = profile.ErrorMessage,
                    Tags = profile.Tags
                };

                _completedProfiles.Enqueue(completedProfile);

                // Limit the number of completed profiles to prevent memory issues
                while (_completedProfiles.Count > _options.MaxCompletedProfiles)
                {
                    _completedProfiles.TryDequeue(out _);
                }

                _logger.LogDebug("Completed profiling operation {OperationName} in {Duration}ms", 
                    profile.Name, profile.Stopwatch.ElapsedMilliseconds);
            }
        }

        private void CleanupExpiredProfiles(object? state)
        {
            try
            {
                var expiredThreshold = DateTime.UtcNow - _options.MaxOperationDuration;
                var expiredProfiles = _activeProfiles
                    .Where(kvp => kvp.Value.StartedAt < expiredThreshold)
                    .ToList();

                foreach (var kvp in expiredProfiles)
                {
                    if (_activeProfiles.TryRemove(kvp.Key, out var profile))
                    {
                        profile.HasError = true;
                        profile.ErrorMessage = "Operation timed out";
                        CompleteOperation(profile);

                        _logger.LogWarning("Operation {OperationName} timed out after {Duration}", 
                            profile.Name, DateTime.UtcNow - profile.StartedAt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during profile cleanup");
            }
        }

        private static TimeSpan Percentile(IEnumerable<TimeSpan> values, double percentile)
        {
            var sorted = values.OrderBy(x => x.Ticks).ToArray();
            if (sorted.Length == 0) return TimeSpan.Zero;

            var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
            index = Math.Max(0, Math.Min(sorted.Length - 1, index));
            
            return sorted[index];
        }

        private SystemMetrics CollectSystemMetrics()
        {
            var process = Process.GetCurrentProcess();
            
            return new SystemMetrics
            {
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0),
                GcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };
        }

        private double GetCpuUsage()
        {
            // This is a simplified CPU usage calculation
            // In a real implementation, you might want to use performance counters
            try
            {
                var process = Process.GetCurrentProcess();
                return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PerformanceProfiler));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cleanupTimer?.Dispose();

            // Complete any remaining active operations
            foreach (var kvp in _activeProfiles)
            {
                CompleteOperation(kvp.Value);
            }

            _logger.LogInformation("Performance profiler disposed");
        }
    }
}
