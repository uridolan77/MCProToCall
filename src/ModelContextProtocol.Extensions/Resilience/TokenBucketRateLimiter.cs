using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Token bucket rate limiter implementation
    /// </summary>
    public class TokenBucketRateLimiter : IRateLimiter
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly ILogger<TokenBucketRateLimiter> _logger;
        private readonly RateLimitOptions _options;

        private double _tokens;
        private long _lastRefillTimestampMs;

        /// <summary>
        /// Initializes a new instance of the TokenBucketRateLimiter class
        /// </summary>
        /// <param name="options">Rate limit options</param>
        /// <param name="logger">Logger</param>
        public TokenBucketRateLimiter(
            IOptions<RateLimitOptions> options,
            ILogger<TokenBucketRateLimiter> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize the bucket with the maximum number of tokens
            _tokens = _options.BucketCapacity;
            _lastRefillTimestampMs = _stopwatch.ElapsedMilliseconds;

            _logger.LogDebug("Initialized token bucket rate limiter with capacity {Capacity} and refill rate {RefillRate} tokens/second",
                _options.BucketCapacity, _options.RefillRate);
        }

        /// <summary>
        /// Gets the current number of available permits
        /// </summary>
        public int AvailablePermits => (int)Math.Floor(_tokens);

        /// <summary>
        /// Gets the maximum number of permits
        /// </summary>
        public int MaxPermits => _options.BucketCapacity;

        /// <summary>
        /// Gets the current rate limit in permits per second
        /// </summary>
        public double PermitsPerSecond => _options.RefillRate;

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
                RefillTokens();

                if (_tokens >= 1)
                {
                    _tokens -= 1;
                    _logger.LogTrace("Acquired token, {AvailableTokens} tokens remaining", _tokens);
                    return true;
                }

                _logger.LogDebug("Rate limit exceeded, no tokens available");
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
                    RefillTokens();

                    if (_tokens >= 1)
                    {
                        _tokens -= 1;
                        _logger.LogTrace("Acquired token, {AvailableTokens} tokens remaining", _tokens);
                        return;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                // Calculate how long to wait before the next token is available
                double timeToNextTokenMs = (1.0 / _options.RefillRate) * 1000;

                // Add a small buffer to ensure the token is available
                timeToNextTokenMs += 10;

                _logger.LogDebug("Rate limit exceeded, waiting {WaitTime}ms for next token", timeToNextTokenMs);

                // Wait for the next token to be available
                await Task.Delay((int)timeToNextTokenMs, cancellationToken);
            }
        }

        /// <summary>
        /// Refills tokens based on elapsed time
        /// </summary>
        private void RefillTokens()
        {
            long currentTimeMs = _stopwatch.ElapsedMilliseconds;
            long elapsedMs = currentTimeMs - _lastRefillTimestampMs;

            if (elapsedMs <= 0)
            {
                return;
            }

            // Calculate how many tokens to add based on the elapsed time and refill rate
            double tokensToAdd = elapsedMs * (_options.RefillRate / 1000.0);

            if (tokensToAdd > 0)
            {
                _tokens = Math.Min(_options.BucketCapacity, _tokens + tokensToAdd);
                _lastRefillTimestampMs = currentTimeMs;

                _logger.LogTrace("Refilled {TokensAdded} tokens, now have {AvailableTokens} tokens",
                    tokensToAdd, _tokens);
            }
        }
    }

    /// <summary>
    /// Options for configuring rate limiting
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Maximum number of tokens in the bucket
        /// </summary>
        public int BucketCapacity { get; set; } = 60;

        /// <summary>
        /// Rate at which tokens are refilled (tokens per second)
        /// </summary>
        public double RefillRate { get; set; } = 10;

        /// <summary>
        /// Size of the sliding window in milliseconds
        /// </summary>
        public int WindowSizeMs { get; set; } = 60000; // 1 minute
    }
}
