using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Sliding window rate limiter implementation
    /// </summary>
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<long> _requestTimestamps = new ConcurrentQueue<long>();
        private readonly ILogger<SlidingWindowRateLimiter> _logger;
        private readonly RateLimitOptions _options;

        /// <summary>
        /// Initializes a new instance of the SlidingWindowRateLimiter class
        /// </summary>
        /// <param name="options">Rate limit options</param>
        /// <param name="logger">Logger</param>
        public SlidingWindowRateLimiter(
            IOptions<RateLimitOptions> options,
            ILogger<SlidingWindowRateLimiter> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("Initialized sliding window rate limiter with capacity {Capacity} and window size {WindowSizeMs}ms",
                _options.BucketCapacity, _options.WindowSizeMs);
        }

        /// <summary>
        /// Gets the current number of available permits
        /// </summary>
        public int AvailablePermits
        {
            get
            {
                CleanupExpiredTimestamps();
                return Math.Max(0, _options.BucketCapacity - _requestTimestamps.Count);
            }
        }

        /// <summary>
        /// Gets the maximum number of permits
        /// </summary>
        public int MaxPermits => _options.BucketCapacity;

        /// <summary>
        /// Gets the current rate limit in permits per second
        /// </summary>
        public double PermitsPerSecond => _options.BucketCapacity / (_options.WindowSizeMs / 1000.0);

        /// <summary>
        /// Tries to acquire a permit from the rate limiter
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if a permit was acquired, false otherwise</returns>
        public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                CleanupExpiredTimestamps();

                if (_requestTimestamps.Count < _options.BucketCapacity)
                {
                    _requestTimestamps.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    _logger.LogTrace("Acquired permit, {PermitsUsed}/{MaxPermits} permits used",
                        _requestTimestamps.Count, _options.BucketCapacity);
                    return true;
                }

                _logger.LogDebug("Rate limit exceeded, {PermitsUsed}/{MaxPermits} permits used",
                    _requestTimestamps.Count, _options.BucketCapacity);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
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
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    CleanupExpiredTimestamps();

                    if (_requestTimestamps.Count < _options.BucketCapacity)
                    {
                        _requestTimestamps.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        _logger.LogTrace("Acquired permit, {PermitsUsed}/{MaxPermits} permits used",
                            _requestTimestamps.Count, _options.BucketCapacity);
                        return;
                    }

                    // Calculate how long to wait before the oldest request expires
                    if (_requestTimestamps.TryPeek(out long oldestTimestamp))
                    {
                        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        long expirationTime = oldestTimestamp + _options.WindowSizeMs;
                        long waitTimeMs = Math.Max(0, expirationTime - currentTime);

                        _logger.LogDebug("Rate limit exceeded, waiting {WaitTime}ms for next permit", waitTimeMs);

                        // Release the semaphore while waiting
                        _semaphore.Release();

                        // Wait for the oldest request to expire
                        await Task.Delay((int)waitTimeMs + 10, cancellationToken); // Add a small buffer
                    }
                    else
                    {
                        // This should never happen, but just in case
                        _semaphore.Release();
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch
                {
                    _semaphore.Release();
                    throw;
                }
            }
        }

        /// <summary>
        /// Removes expired timestamps from the queue
        /// </summary>
        private void CleanupExpiredTimestamps()
        {
            long cutoffTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _options.WindowSizeMs;

            // Dequeue expired timestamps
            while (_requestTimestamps.TryPeek(out long timestamp) && timestamp <= cutoffTime)
            {
                _requestTimestamps.TryDequeue(out _);
            }
        }
    }

    // Options are now defined in the RateLimitOptions class
}
