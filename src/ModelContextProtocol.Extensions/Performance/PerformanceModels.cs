using System.Diagnostics;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// Configuration options for the performance profiler
    /// </summary>
    public class PerformanceProfilerOptions
    {
        /// <summary>
        /// Maximum number of completed profiles to keep in memory
        /// </summary>
        public int MaxCompletedProfiles { get; set; } = 10000;

        /// <summary>
        /// Maximum duration an operation can run before being considered expired
        /// </summary>
        public TimeSpan MaxOperationDuration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Whether to collect detailed system metrics
        /// </summary>
        public bool CollectSystemMetrics { get; set; } = true;

        /// <summary>
        /// Whether to enable automatic cleanup of expired operations
        /// </summary>
        public bool EnableAutoCleanup { get; set; } = true;
    }

    /// <summary>
    /// Represents an active operation being profiled
    /// </summary>
    public class OperationProfile
    {
        public string Id { get; }
        public string Name { get; }
        public DateTime StartedAt { get; }
        public Stopwatch Stopwatch { get; }
        public Dictionary<string, object>? Tags { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }

        public OperationProfile(string name, Dictionary<string, object>? tags = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name ?? throw new ArgumentNullException(nameof(name));
            StartedAt = DateTime.UtcNow;
            Stopwatch = Stopwatch.StartNew();
            Tags = tags;
        }

        public void MarkError(string errorMessage)
        {
            HasError = true;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Represents a completed operation profile
    /// </summary>
    public class CompletedOperationProfile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? Tags { get; set; }
    }

    /// <summary>
    /// Disposable scope for synchronous operations
    /// </summary>
    public class OperationScope : IDisposable
    {
        private readonly OperationProfile _profile;
        private readonly Action<OperationProfile> _onComplete;
        private bool _disposed;

        public OperationScope(OperationProfile profile, Action<OperationProfile> onComplete)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
        }

        public void MarkError(string errorMessage)
        {
            _profile.MarkError(errorMessage);
        }

        public void AddTag(string key, object value)
        {
            _profile.Tags ??= new Dictionary<string, object>();
            _profile.Tags[key] = value;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _profile.Stopwatch.Stop();
            _onComplete(_profile);
        }
    }

    /// <summary>
    /// Disposable scope for asynchronous operations
    /// </summary>
    public class AsyncOperationScope : IAsyncDisposable
    {
        private readonly OperationProfile _profile;
        private readonly Action<OperationProfile> _onComplete;
        private bool _disposed;

        public AsyncOperationScope(OperationProfile profile, Action<OperationProfile> onComplete)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
        }

        public void MarkError(string errorMessage)
        {
            _profile.MarkError(errorMessage);
        }

        public void AddTag(string key, object value)
        {
            _profile.Tags ??= new Dictionary<string, object>();
            _profile.Tags[key] = value;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;

            _disposed = true;
            _profile.Stopwatch.Stop();
            _onComplete(_profile);

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Summary of operation performance
    /// </summary>
    public class OperationSummary
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan P50Duration { get; set; }
        public TimeSpan P95Duration { get; set; }
        public TimeSpan P99Duration { get; set; }
        public int ErrorCount { get; set; }
        public double ErrorRate { get; set; }
        public Dictionary<string, List<object>> Tags { get; set; } = new();
    }

    /// <summary>
    /// Comprehensive performance report
    /// </summary>
    public class PerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan? TimeWindow { get; set; }
        public int TotalOperations { get; set; }
        public int UniqueOperationTypes { get; set; }
        public int ActiveOperations { get; set; }
        public List<OperationSummary> Operations { get; set; } = new();
        public SystemMetrics SystemMetrics { get; set; } = new();
    }

    /// <summary>
    /// Real-time performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        public int ActiveOperations { get; set; }
        public int CompletedOperationsLast5Min { get; set; }
        public TimeSpan AverageResponseTimeLast5Min { get; set; }
        public double ErrorRateLast5Min { get; set; }
        public double ThroughputPerSecond { get; set; }
        public double MemoryUsageMB { get; set; }
    }

    /// <summary>
    /// System-level performance metrics
    /// </summary>
    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double GcMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
    }
}
