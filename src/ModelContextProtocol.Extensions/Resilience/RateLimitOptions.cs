namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Configuration options for rate limiting
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Whether rate limiting is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of requests per minute
        /// </summary>
        public int RequestsPerMinute { get; set; } = 100;

        /// <summary>
        /// Maximum number of requests per day
        /// </summary>
        public int RequestsPerDay { get; set; } = 10000;

        /// <summary>
        /// Burst capacity for token bucket
        /// </summary>
        public int BurstSize { get; set; } = 20;

        /// <summary>
        /// Time window for sliding window rate limiter
        /// </summary>
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Number of buckets for sliding window
        /// </summary>
        public int BucketCount { get; set; } = 10;

        /// <summary>
        /// Whether to enable per-client rate limiting
        /// </summary>
        public bool EnablePerClientLimiting { get; set; } = true;

        /// <summary>
        /// Default client identifier when none is provided
        /// </summary>
        public string DefaultClientId { get; set; } = "default";

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
