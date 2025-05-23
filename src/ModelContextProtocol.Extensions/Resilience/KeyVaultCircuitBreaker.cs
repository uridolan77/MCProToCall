using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Configuration options for Key Vault circuit breaker
    /// </summary>
    public class KeyVaultCircuitBreakerOptions
    {
        /// <summary>
        /// Whether to enable the circuit breaker
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Number of consecutive failures before opening the circuit
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Duration to keep the circuit open before attempting to close it (in seconds)
        /// </summary>
        public int OpenCircuitDurationSeconds { get; set; } = 60;

        /// <summary>
        /// Minimum number of requests in the sampling duration before circuit can trip
        /// </summary>
        public int MinimumThroughput { get; set; } = 10;

        /// <summary>
        /// Sampling duration for calculating failure rate (in seconds)
        /// </summary>
        public int SamplingDurationSeconds { get; set; } = 30;

        /// <summary>
        /// Failure rate threshold (0.0 to 1.0) that will trip the circuit
        /// </summary>
        public double FailureRateThreshold { get; set; } = 0.5;

        /// <summary>
        /// Whether to use fallback values when circuit is open
        /// </summary>
        public bool UseFallbackWhenOpen { get; set; } = true;
    }

    /// <summary>
    /// Circuit breaker implementation specifically for Azure Key Vault operations
    /// </summary>
    public class KeyVaultCircuitBreaker
    {
        private readonly ILogger<KeyVaultCircuitBreaker> _logger;
        private readonly KeyVaultCircuitBreakerOptions _options;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly object _statsLock = new object();
        private CircuitBreakerStats _stats;

        public KeyVaultCircuitBreaker(
            ILogger<KeyVaultCircuitBreaker> logger,
            IOptions<KeyVaultCircuitBreakerOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _stats = new CircuitBreakerStats();

            if (_options.EnableCircuitBreaker)
            {
                _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
                _logger.LogInformation("Initialized Key Vault circuit breaker with {FailureThreshold} failure threshold",
                    _options.FailureThreshold);
            }
            else
            {
                _circuitBreakerPolicy = Policy.NoOpAsync();
                _logger.LogInformation("Key Vault circuit breaker is disabled");
            }
        }

        /// <summary>
        /// Executes a Key Vault operation with circuit breaker protection
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="fallback">Fallback operation if circuit is open</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <returns>Result of the operation or fallback</returns>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<Task<T>> fallback = null,
            string operationName = "KeyVault")
        {
            if (!_options.EnableCircuitBreaker)
            {
                return await operation();
            }

            try
            {
                var result = await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogTrace("Executing Key Vault operation: {OperationName}", operationName);

                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var operationResult = await operation();
                        RecordSuccess(DateTime.UtcNow - startTime);
                        return operationResult;
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(DateTime.UtcNow - startTime, ex);
                        throw;
                    }
                });

                return result;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning("Key Vault circuit breaker is open, operation {OperationName} rejected", operationName);

                if (_options.UseFallbackWhenOpen && fallback != null)
                {
                    _logger.LogInformation("Executing fallback for operation {OperationName}", operationName);
                    return await fallback();
                }

                throw new KeyVaultCircuitOpenException(
                    $"Key Vault circuit breaker is open for operation '{operationName}'. " +
                    "The service may be experiencing issues.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Key Vault operation {OperationName}", operationName);
                throw;
            }
        }

        /// <summary>
        /// Gets the current circuit breaker state
        /// </summary>
        public CircuitBreakerState GetState()
        {
            if (!_options.EnableCircuitBreaker)
                return CircuitBreakerState.Closed;

            if (_circuitBreakerPolicy is CircuitBreakerPolicy policy)
            {
                return ConvertPollyCircuitState(policy.CircuitState);
            }

            return CircuitBreakerState.Closed;
        }

        /// <summary>
        /// Converts Polly CircuitState to our CircuitBreakerState
        /// </summary>
        private static CircuitBreakerState ConvertPollyCircuitState(Polly.CircuitBreaker.CircuitState pollyState)
        {
            return pollyState switch
            {
                Polly.CircuitBreaker.CircuitState.Closed => CircuitBreakerState.Closed,
                Polly.CircuitBreaker.CircuitState.Open => CircuitBreakerState.Open,
                Polly.CircuitBreaker.CircuitState.HalfOpen => CircuitBreakerState.HalfOpen,
                _ => CircuitBreakerState.Closed
            };
        }

        /// <summary>
        /// Gets circuit breaker statistics
        /// </summary>
        public CircuitBreakerStats GetStats()
        {
            lock (_statsLock)
            {
                return new CircuitBreakerStats
                {
                    TotalRequests = _stats.TotalRequests,
                    SuccessfulRequests = _stats.SuccessfulRequests,
                    FailedRequests = _stats.FailedRequests,
                    CircuitOpenCount = _stats.CircuitOpenCount,
                    AverageResponseTime = _stats.AverageResponseTime,
                    LastFailureTime = _stats.LastFailureTime,
                    LastSuccessTime = _stats.LastSuccessTime,
                    CurrentState = GetState()
                };
            }
        }

        /// <summary>
        /// Manually opens the circuit breaker
        /// </summary>
        public void TripCircuit()
        {
            if (_circuitBreakerPolicy is CircuitBreakerPolicy policy)
            {
                policy.Isolate();
                _logger.LogWarning("Circuit breaker manually tripped");

                lock (_statsLock)
                {
                    _stats.CircuitOpenCount++;
                }
            }
        }

        /// <summary>
        /// Manually closes the circuit breaker
        /// </summary>
        public void ResetCircuit()
        {
            if (_circuitBreakerPolicy is CircuitBreakerPolicy policy)
            {
                policy.Reset();
                _logger.LogInformation("Circuit breaker manually reset");
            }
        }

        private IAsyncPolicy CreateCircuitBreakerPolicy()
        {
            return Policy
                .Handle<Exception>(ShouldTripCircuit)
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: _options.FailureRateThreshold,
                    samplingDuration: TimeSpan.FromSeconds(_options.SamplingDurationSeconds),
                    minimumThroughput: _options.MinimumThroughput,
                    durationOfBreak: TimeSpan.FromSeconds(_options.OpenCircuitDurationSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(exception,
                            "Key Vault circuit breaker opened for {Duration}s due to failure threshold exceeded",
                            duration.TotalSeconds);

                        lock (_statsLock)
                        {
                            _stats.CircuitOpenCount++;
                        }
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Key Vault circuit breaker reset - service appears healthy");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Key Vault circuit breaker half-open - testing service health");
                    });
        }

        private bool ShouldTripCircuit(Exception exception)
        {
            // Trip circuit on Azure service errors and timeouts
            return exception is Azure.RequestFailedException azureEx &&
                   (azureEx.Status >= 500 || azureEx.Status == 408 || azureEx.Status == 429) ||
                   exception is TaskCanceledException ||
                   exception is TimeoutException ||
                   exception is System.Net.Http.HttpRequestException;
        }

        private void RecordSuccess(TimeSpan responseTime)
        {
            lock (_statsLock)
            {
                _stats.TotalRequests++;
                _stats.SuccessfulRequests++;
                _stats.LastSuccessTime = DateTime.UtcNow;
                UpdateAverageResponseTime(responseTime);
            }
        }

        private void RecordFailure(TimeSpan responseTime, Exception exception)
        {
            lock (_statsLock)
            {
                _stats.TotalRequests++;
                _stats.FailedRequests++;
                _stats.LastFailureTime = DateTime.UtcNow;
                _stats.LastFailureException = exception.GetType().Name;
                UpdateAverageResponseTime(responseTime);
            }
        }

        private void UpdateAverageResponseTime(TimeSpan responseTime)
        {
            // Simple moving average calculation
            if (_stats.AverageResponseTime == TimeSpan.Zero)
            {
                _stats.AverageResponseTime = responseTime;
            }
            else
            {
                var totalMs = (_stats.AverageResponseTime.TotalMilliseconds * (_stats.TotalRequests - 1) +
                              responseTime.TotalMilliseconds) / _stats.TotalRequests;
                _stats.AverageResponseTime = TimeSpan.FromMilliseconds(totalMs);
            }
        }
    }

    /// <summary>
    /// Statistics for circuit breaker operations
    /// </summary>
    public class CircuitBreakerStats
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long CircuitOpenCount { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public string LastFailureException { get; set; }
        public CircuitBreakerState CurrentState { get; set; }

        public double FailureRate => TotalRequests > 0 ? (double)FailedRequests / TotalRequests : 0.0;
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0.0;
    }

    /// <summary>
    /// Exception thrown when Key Vault circuit breaker is open
    /// </summary>
    public class KeyVaultCircuitOpenException : Exception
    {
        public KeyVaultCircuitOpenException(string message) : base(message) { }
        public KeyVaultCircuitOpenException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Circuit breaker state enumeration
    /// </summary>
    public enum CircuitBreakerState
    {
        /// <summary>
        /// Circuit is closed - requests are allowed through
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open - requests are blocked
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open - testing if service has recovered
        /// </summary>
        HalfOpen
    }
}
