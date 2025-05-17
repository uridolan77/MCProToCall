using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Factory for creating HTTP clients with resilience policies
    /// </summary>
    public class ResilientHttpClientFactory : IHttpClientFactory
    {
        private readonly IHttpClientFactory _innerFactory;
        private readonly ILogger<ResilientHttpClientFactory> _logger;
        private readonly ResilienceOptions _options;

        /// <summary>
        /// Initializes a new instance of the ResilientHttpClientFactory class
        /// </summary>
        /// <param name="innerFactory">The inner HTTP client factory</param>
        /// <param name="options">Resilience options</param>
        /// <param name="logger">Logger</param>
        public ResilientHttpClientFactory(
            IHttpClientFactory innerFactory,
            IOptions<ResilienceOptions> options,
            ILogger<ResilientHttpClientFactory> logger)
        {
            _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates an HTTP client with resilience policies
        /// </summary>
        /// <param name="name">The name of the client</param>
        /// <returns>An HTTP client</returns>
        public HttpClient CreateClient(string name)
        {
            var client = _innerFactory.CreateClient(name);
            
            // The actual resilience policies are applied at the HttpMessageHandler level
            // through the HttpClientBuilder in the DI configuration
            
            return client;
        }

        /// <summary>
        /// Creates a retry policy for HTTP requests
        /// </summary>
        /// <returns>A retry policy</returns>
        public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(ResilienceOptions options, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // HttpRequestException, 5XX and 408 status codes
                .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests) // 429 status code
                .WaitAndRetryAsync(
                    retryCount: options.MaxRetryCount,
                    sleepDurationProvider: (retryAttempt, response, context) =>
                    {
                        // Use exponential backoff with jitter
                        var baseDelay = options.RetryBackoffFactor * Math.Pow(2, retryAttempt - 1);
                        var jitter = new Random().NextDouble() * 0.2 - 0.1; // -10% to +10%
                        var delay = baseDelay * (1 + jitter);
                        
                        return TimeSpan.FromSeconds(Math.Min(delay, options.MaxRetryDelaySeconds));
                    },
                    onRetryAsync: async (outcome, timespan, retryAttempt, context) =>
                    {
                        if (outcome.Exception != null)
                        {
                            logger.LogWarning(outcome.Exception, 
                                "Retry {RetryAttempt}/{MaxRetry} after {RetryDelay}s due to exception",
                                retryAttempt, options.MaxRetryCount, timespan.TotalSeconds);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Retry {RetryAttempt}/{MaxRetry} after {RetryDelay}s due to status code {StatusCode}",
                                retryAttempt, options.MaxRetryCount, timespan.TotalSeconds, 
                                (int)outcome.Result.StatusCode);
                            
                            // Log response body on error for debugging
                            if (options.LogResponseBodyOnError)
                            {
                                var content = await outcome.Result.Content.ReadAsStringAsync();
                                logger.LogDebug("Error response body: {ResponseBody}", 
                                    content.Length > 1000 ? content.Substring(0, 1000) + "..." : content);
                            }
                        }
                    });
        }

        /// <summary>
        /// Creates a circuit breaker policy for HTTP requests
        /// </summary>
        /// <returns>A circuit breaker policy</returns>
        public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy(ResilienceOptions options, ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: options.CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                    onBreak: (outcome, timespan) =>
                    {
                        if (outcome.Exception != null)
                        {
                            logger.LogError(outcome.Exception, 
                                "Circuit breaker opened for {BreakDuration}s due to exception", 
                                timespan.TotalSeconds);
                        }
                        else
                        {
                            logger.LogError(
                                "Circuit breaker opened for {BreakDuration}s due to status code {StatusCode}", 
                                timespan.TotalSeconds, (int)outcome.Result.StatusCode);
                        }
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogInformation("Circuit breaker half-open, testing connectivity");
                    });
        }

        /// <summary>
        /// Creates a timeout policy for HTTP requests
        /// </summary>
        /// <returns>A timeout policy</returns>
        public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy(ResilienceOptions options, ILogger logger)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout: TimeSpan.FromSeconds(options.TimeoutSeconds),
                timeoutStrategy: TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    logger.LogWarning("Request timed out after {Timeout}s", timespan.TotalSeconds);
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Creates a combined resilience policy for HTTP requests
        /// </summary>
        /// <returns>A combined policy</returns>
        public static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(ResilienceOptions options, ILogger logger)
        {
            // Create individual policies
            var retryPolicy = CreateRetryPolicy(options, logger);
            var circuitBreakerPolicy = CreateCircuitBreakerPolicy(options, logger);
            var timeoutPolicy = CreateTimeoutPolicy(options, logger);

            // Combine policies
            // Timeout -> Retry -> Circuit Breaker
            return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
        }
    }
}
