namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Options for configuring resilience policies
    /// </summary>
    public class ResilienceOptions
    {
        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// Base delay factor for retry backoff in seconds
        /// </summary>
        public double RetryBackoffFactor { get; set; } = 1.0;

        /// <summary>
        /// Maximum delay between retries in seconds
        /// </summary>
        public double MaxRetryDelaySeconds { get; set; } = 30.0;

        /// <summary>
        /// Number of failures before the circuit breaker opens
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Duration the circuit stays open before moving to half-open state in seconds
        /// </summary>
        public int CircuitBreakerDurationSeconds { get; set; } = 30;

        /// <summary>
        /// Timeout for requests in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to log response bodies on error
        /// </summary>
        public bool LogResponseBodyOnError { get; set; } = false;

        /// <summary>
        /// Whether to enable the circuit breaker
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Whether to enable retries
        /// </summary>
        public bool EnableRetries { get; set; } = true;

        /// <summary>
        /// Whether to enable timeouts
        /// </summary>
        public bool EnableTimeouts { get; set; } = true;
    }
}
