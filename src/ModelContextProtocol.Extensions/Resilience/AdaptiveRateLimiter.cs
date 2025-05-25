using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Adaptive rate limiter that adjusts limits based on system performance and error rates
    /// </summary>
    public class AdaptiveRateLimiter : IRateLimiter, IDisposable
    {
        private readonly ILogger<AdaptiveRateLimiter> _logger;        private readonly AdaptiveRateLimitOptions _options;
        private readonly ConcurrentDictionary<string, ClientMetrics> _clientMetrics;
        private readonly Timer _adjustmentTimer;
        private volatile int _currentLimit;
        private double _currentErrorRate;
        private long _currentResponseTimeTicks;        private readonly object _adjustmentLock = new object();
        private bool _disposed;

        /// <summary>
        /// Gets the current response time
        /// </summary>
        private TimeSpan CurrentResponseTime => new TimeSpan(Interlocked.Read(ref _currentResponseTimeTicks));

        public AdaptiveRateLimiter(
            ILogger<AdaptiveRateLimiter> logger,
            IOptions<AdaptiveRateLimitOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _clientMetrics = new ConcurrentDictionary<string, ClientMetrics>();
            _currentLimit = _options.InitialLimit;

            // Start adjustment timer
            _adjustmentTimer = new Timer(AdjustLimits, null,
                _options.AdjustmentInterval, _options.AdjustmentInterval);

            _logger.LogInformation("Adaptive rate limiter initialized with initial limit: {InitialLimit}",
                _options.InitialLimit);
        }

        /// <summary>
        /// Gets the current number of available permits
        /// </summary>
        public int AvailablePermits => Math.Max(0, _currentLimit - _clientMetrics.Values.Sum(m => m.RequestTimes.Count));

        /// <summary>
        /// Gets the maximum number of permits
        /// </summary>
        public int MaxPermits => _currentLimit;

        /// <summary>
        /// Gets the current rate limit in permits per second
        /// </summary>
        public double PermitsPerSecond => _currentLimit / _options.TimeWindow.TotalSeconds;

        /// <summary>
        /// Tries to acquire a permit from the rate limiter
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if a permit was acquired, false otherwise</returns>
        public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
        {
            var result = await IsAllowedAsync("default", cancellationToken);
            return result.IsAllowed;
        }

        /// <summary>
        /// Acquires a permit from the rate limiter, waiting if necessary
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task that completes when a permit is acquired</returns>
        public async Task AcquireAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (await TryAcquireAsync(cancellationToken))
                    return;

                await Task.Delay(100, cancellationToken); // Wait 100ms before retrying
            }
        }

        /// <summary>
        /// Check if a request should be allowed for the specified client
        /// </summary>
        public async Task<RateLimitResult> IsAllowedAsync(string clientId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentNullException(nameof(clientId));

            var metrics = _clientMetrics.GetOrAdd(clientId, _ => new ClientMetrics());
            var now = DateTime.UtcNow;

            lock (metrics.Lock)
            {
                // Clean up old entries
                CleanupOldEntries(metrics, now);

                // Check current rate
                var currentCount = metrics.RequestTimes.Count;
                if (currentCount >= _currentLimit)
                {
                    _logger.LogDebug("Rate limit exceeded for client {ClientId}: {CurrentCount}/{CurrentLimit}",
                        clientId, currentCount, _currentLimit);

                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        CurrentLimit = _currentLimit,
                        RemainingRequests = 0,
                        RetryAfter = CalculateRetryAfter(metrics, now)
                    };
                }

                // Allow request and track it
                metrics.RequestTimes.Add(now);

                return new RateLimitResult
                {
                    IsAllowed = true,
                    CurrentLimit = _currentLimit,
                    RemainingRequests = _currentLimit - currentCount - 1,
                    RetryAfter = TimeSpan.Zero
                };
            }
        }

        /// <summary>
        /// Record the completion of a request with its outcome
        /// </summary>
        public void RecordRequestCompletion(string clientId, bool wasSuccessful, TimeSpan responseTime)
        {
            if (string.IsNullOrEmpty(clientId))
                return;

            var metrics = _clientMetrics.GetOrAdd(clientId, _ => new ClientMetrics());

            lock (metrics.Lock)
            {
                metrics.TotalRequests++;
                if (!wasSuccessful)
                {
                    metrics.ErrorCount++;
                }

                metrics.TotalResponseTime += responseTime;

                // Update global metrics for adjustment
                UpdateGlobalMetrics(wasSuccessful, responseTime);
            }
        }

        /// <summary>
        /// Update global metrics used for rate limit adjustment
        /// </summary>
        private void UpdateGlobalMetrics(bool wasSuccessful, TimeSpan responseTime)
        {
            // Calculate rolling error rate
            var totalRequests = 0;
            var totalErrors = 0;
            var totalResponseTimeMs = 0.0;

            foreach (var kvp in _clientMetrics)
            {
                var metrics = kvp.Value;
                lock (metrics.Lock)
                {
                    totalRequests += metrics.TotalRequests;
                    totalErrors += metrics.ErrorCount;
                    totalResponseTimeMs += metrics.TotalResponseTime.TotalMilliseconds;
                }
            }            if (totalRequests > 0)
            {
                _currentErrorRate = (double)totalErrors / totalRequests;
                var averageResponseTimeMs = totalResponseTimeMs / totalRequests;
                Interlocked.Exchange(ref _currentResponseTimeTicks, TimeSpan.FromMilliseconds(averageResponseTimeMs).Ticks);
            }
        }

        /// <summary>
        /// Adjust rate limits based on current system performance
        /// </summary>
        private void AdjustLimits(object state)
        {
            if (_disposed)
                return;

            lock (_adjustmentLock)
            {
                try
                {
                    var previousLimit = _currentLimit;
                    var newLimit = CalculateNewLimit();

                    if (newLimit != _currentLimit)
                    {
                        _currentLimit = newLimit;
                        _logger.LogInformation(                            "Rate limit adjusted from {PreviousLimit} to {NewLimit} " +
                            "(ErrorRate: {ErrorRate:P2}, AvgResponseTime: {ResponseTime}ms)",
                            previousLimit, newLimit, _currentErrorRate, CurrentResponseTime.TotalMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adjusting rate limits");
                }
            }
        }

        /// <summary>
        /// Calculate new rate limit based on current metrics
        /// </summary>
        private int CalculateNewLimit()
        {
            var newLimit = _currentLimit;

            // Decrease limit if error rate is too high
            if (_currentErrorRate > _options.ErrorRateThreshold)
            {
                var decreaseFactor = Math.Min(_options.MaxDecreaseFactor,
                    1.0 + (_currentErrorRate - _options.ErrorRateThreshold) * 2);
                newLimit = (int)Math.Max(_options.MinLimit, _currentLimit / decreaseFactor);
            }            // Decrease limit if response time is too high
            else if (CurrentResponseTime.TotalMilliseconds > _options.ResponseTimeThresholdMs)
            {
                var decreaseFactor = Math.Min(_options.MaxDecreaseFactor,
                    1.0 + (CurrentResponseTime.TotalMilliseconds - _options.ResponseTimeThresholdMs) / _options.ResponseTimeThresholdMs);
                newLimit = (int)Math.Max(_options.MinLimit, _currentLimit / decreaseFactor);
            }            // Increase limit if system is performing well
            else if (_currentErrorRate < _options.ErrorRateThreshold * 0.5 &&
                     CurrentResponseTime.TotalMilliseconds < _options.ResponseTimeThresholdMs * 0.8)
            {
                newLimit = (int)Math.Min(_options.MaxLimit, _currentLimit * _options.IncreaseFactor);
            }

            return newLimit;
        }

        /// <summary>
        /// Clean up old request entries outside the time window
        /// </summary>
        private void CleanupOldEntries(ClientMetrics metrics, DateTime now)
        {
            var cutoff = now.Subtract(_options.TimeWindow);

            for (int i = metrics.RequestTimes.Count - 1; i >= 0; i--)
            {
                if (metrics.RequestTimes[i] < cutoff)
                {
                    metrics.RequestTimes.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Calculate when the client should retry
        /// </summary>
        private TimeSpan CalculateRetryAfter(ClientMetrics metrics, DateTime now)
        {
            if (metrics.RequestTimes.Count == 0)
                return TimeSpan.Zero;

            var oldestRequest = metrics.RequestTimes[0];
            var retryAfter = oldestRequest.Add(_options.TimeWindow).Subtract(now);

            return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Get current rate limit statistics
        /// </summary>
        public AdaptiveRateLimitStatistics GetStatistics()
        {            var stats = new AdaptiveRateLimitStatistics
            {
                CurrentLimit = _currentLimit,
                CurrentErrorRate = _currentErrorRate,
                CurrentResponseTime = CurrentResponseTime,
                TotalClients = _clientMetrics.Count,
                InitialLimit = _options.InitialLimit,
                MinLimit = _options.MinLimit,
                MaxLimit = _options.MaxLimit
            };

            var totalRequests = 0;
            var totalErrors = 0;

            foreach (var kvp in _clientMetrics)
            {
                var metrics = kvp.Value;
                lock (metrics.Lock)
                {
                    totalRequests += metrics.TotalRequests;
                    totalErrors += metrics.ErrorCount;
                }
            }

            stats.TotalRequests = totalRequests;
            stats.TotalErrors = totalErrors;

            return stats;
        }

        /// <summary>
        /// Reset metrics for all clients
        /// </summary>
        public void ResetMetrics()
        {
            _logger.LogInformation("Resetting adaptive rate limiter metrics");

            foreach (var kvp in _clientMetrics)
            {
                var metrics = kvp.Value;
                lock (metrics.Lock)
                {
                    metrics.RequestTimes.Clear();
                    metrics.TotalRequests = 0;
                    metrics.ErrorCount = 0;
                    metrics.TotalResponseTime = TimeSpan.Zero;
                }
            }            _currentErrorRate = 0;
            Interlocked.Exchange(ref _currentResponseTimeTicks, 0);
            _currentLimit = _options.InitialLimit;
        }

        /// <summary>
        /// Dispose the adaptive rate limiter
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _adjustmentTimer?.Dispose();
            _clientMetrics.Clear();
            _disposed = true;

            _logger.LogDebug("AdaptiveRateLimiter disposed");
        }
    }

    /// <summary>
    /// Configuration options for adaptive rate limiting
    /// </summary>
    public class AdaptiveRateLimitOptions
    {
        public int InitialLimit { get; set; } = 100;
        public int MinLimit { get; set; } = 10;
        public int MaxLimit { get; set; } = 1000;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan AdjustmentInterval { get; set; } = TimeSpan.FromSeconds(30);
        public double ErrorRateThreshold { get; set; } = 0.05; // 5%
        public double ResponseTimeThresholdMs { get; set; } = 1000; // 1 second
        public double IncreaseFactor { get; set; } = 1.1;
        public double MaxDecreaseFactor { get; set; } = 2.0;
    }

    /// <summary>
    /// Result of a rate limit check
    /// </summary>
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int CurrentLimit { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan RetryAfter { get; set; }
    }

    /// <summary>
    /// Statistics for adaptive rate limiting
    /// </summary>
    public class AdaptiveRateLimitStatistics
    {
        public int CurrentLimit { get; set; }
        public double CurrentErrorRate { get; set; }
        public TimeSpan CurrentResponseTime { get; set; }
        public int TotalClients { get; set; }
        public int TotalRequests { get; set; }
        public int TotalErrors { get; set; }
        public int InitialLimit { get; set; }
        public int MinLimit { get; set; }
        public int MaxLimit { get; set; }
    }

    /// <summary>
    /// Metrics for a specific client
    /// </summary>
    internal class ClientMetrics
    {
        public readonly object Lock = new object();
        public readonly List<DateTime> RequestTimes = new List<DateTime>();
        public int TotalRequests { get; set; }
        public int ErrorCount { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
    }
}
