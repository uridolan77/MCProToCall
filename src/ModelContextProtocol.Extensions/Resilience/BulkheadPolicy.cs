using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// Implements bulkhead isolation pattern to prevent resource exhaustion
    /// </summary>
    public class BulkheadPolicy<T> : IBulkheadPolicy<T>, IDisposable
    {
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly SemaphoreSlim _queueSemaphore;
        private readonly ILogger<BulkheadPolicy<T>> _logger;
        private readonly BulkheadOptions _options;
        private readonly string _bulkheadName;
        private bool _disposed;

        // Metrics
        private long _totalRequests;
        private long _rejectedRequests;
        private long _timeoutRequests;
        private long _successfulRequests;
        private long _currentExecutions;
        private long _currentQueueSize;

        public BulkheadPolicy(
            ILogger<BulkheadPolicy<T>> logger,
            IOptions<TlsOptions> tlsOptions,
            string bulkheadName = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = tlsOptions?.Value?.BulkheadOptions ?? new BulkheadOptions();
            _bulkheadName = bulkheadName ?? typeof(T).Name;

            _executionSemaphore = new SemaphoreSlim(_options.MaxConcurrentExecutions, _options.MaxConcurrentExecutions);
            _queueSemaphore = new SemaphoreSlim(_options.MaxQueueSize, _options.MaxQueueSize);

            _logger.LogInformation("Bulkhead policy '{BulkheadName}' initialized with {MaxExecutions} max executions and {MaxQueue} max queue size",
                _bulkheadName, _options.MaxConcurrentExecutions, _options.MaxQueueSize);
        }

        /// <summary>
        /// Executes an operation within the bulkhead
        /// </summary>
        public async Task<T> ExecuteAsync(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (_disposed) throw new ObjectDisposedException(nameof(BulkheadPolicy<T>));

            Interlocked.Increment(ref _totalRequests);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(_options.QueueTimeoutSeconds));

            // Try to enter the queue
            if (!await _queueSemaphore.WaitAsync(0, linkedCts.Token))
            {
                Interlocked.Increment(ref _rejectedRequests);
                _logger.LogWarning("Bulkhead '{BulkheadName}' queue is full. Request rejected. Queue size: {QueueSize}",
                    _bulkheadName, _options.MaxQueueSize);
                
                throw new BulkheadRejectedException($"Bulkhead '{_bulkheadName}' queue is full");
            }

            Interlocked.Increment(ref _currentQueueSize);

            try
            {
                // Wait for execution slot
                try
                {
                    await _executionSemaphore.WaitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException) when (linkedCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref _timeoutRequests);
                    _logger.LogWarning("Bulkhead '{BulkheadName}' queue timeout after {Timeout} seconds",
                        _bulkheadName, _options.QueueTimeoutSeconds);
                    
                    throw new BulkheadTimeoutException($"Bulkhead '{_bulkheadName}' queue timeout");
                }

                Interlocked.Increment(ref _currentExecutions);
                Interlocked.Decrement(ref _currentQueueSize);

                try
                {
                    _logger.LogTrace("Executing operation in bulkhead '{BulkheadName}'. Current executions: {CurrentExecutions}",
                        _bulkheadName, _currentExecutions);

                    var result = await operation(cancellationToken);
                    
                    Interlocked.Increment(ref _successfulRequests);
                    _logger.LogTrace("Operation completed successfully in bulkhead '{BulkheadName}'", _bulkheadName);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Operation failed in bulkhead '{BulkheadName}'", _bulkheadName);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref _currentExecutions);
                    _executionSemaphore.Release();
                }
            }
            finally
            {
                if (_currentQueueSize > 0)
                {
                    Interlocked.Decrement(ref _currentQueueSize);
                }
                _queueSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes an operation within the bulkhead (non-generic version)
        /// </summary>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async ct =>
            {
                await operation(ct);
                return default(T);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets current bulkhead metrics
        /// </summary>
        public BulkheadMetrics GetMetrics()
        {
            return new BulkheadMetrics
            {
                BulkheadName = _bulkheadName,
                TotalRequests = _totalRequests,
                RejectedRequests = _rejectedRequests,
                TimeoutRequests = _timeoutRequests,
                SuccessfulRequests = _successfulRequests,
                CurrentExecutions = _currentExecutions,
                CurrentQueueSize = _currentQueueSize,
                MaxConcurrentExecutions = _options.MaxConcurrentExecutions,
                MaxQueueSize = _options.MaxQueueSize,
                QueueTimeoutSeconds = _options.QueueTimeoutSeconds,
                AvailableExecutionSlots = _executionSemaphore.CurrentCount,
                AvailableQueueSlots = _queueSemaphore.CurrentCount
            };
        }

        /// <summary>
        /// Checks if the bulkhead can accept new requests
        /// </summary>
        public bool CanAcceptRequest()
        {
            return _queueSemaphore.CurrentCount > 0;
        }

        /// <summary>
        /// Gets the current load factor (0.0 to 1.0)
        /// </summary>
        public double GetLoadFactor()
        {
            var executionLoad = 1.0 - ((double)_executionSemaphore.CurrentCount / _options.MaxConcurrentExecutions);
            var queueLoad = 1.0 - ((double)_queueSemaphore.CurrentCount / _options.MaxQueueSize);
            
            // Return the higher of the two loads
            return Math.Max(executionLoad, queueLoad);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogInformation("Disposing bulkhead policy '{BulkheadName}'. Final metrics: {Metrics}",
                    _bulkheadName, GetMetrics());

                _executionSemaphore?.Dispose();
                _queueSemaphore?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Interface for bulkhead policy
    /// </summary>
    public interface IBulkheadPolicy<T>
    {
        Task<T> ExecuteAsync(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
        Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
        BulkheadMetrics GetMetrics();
        bool CanAcceptRequest();
        double GetLoadFactor();
    }

    /// <summary>
    /// Bulkhead metrics for monitoring
    /// </summary>
    public class BulkheadMetrics
    {
        public string BulkheadName { get; set; }
        public long TotalRequests { get; set; }
        public long RejectedRequests { get; set; }
        public long TimeoutRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long CurrentExecutions { get; set; }
        public long CurrentQueueSize { get; set; }
        public int MaxConcurrentExecutions { get; set; }
        public int MaxQueueSize { get; set; }
        public int QueueTimeoutSeconds { get; set; }
        public int AvailableExecutionSlots { get; set; }
        public int AvailableQueueSlots { get; set; }

        public double RejectionRate => TotalRequests > 0 ? (double)RejectedRequests / TotalRequests : 0;
        public double TimeoutRate => TotalRequests > 0 ? (double)TimeoutRequests / TotalRequests : 0;
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
        public double ExecutionUtilization => MaxConcurrentExecutions > 0 ? (double)CurrentExecutions / MaxConcurrentExecutions : 0;
        public double QueueUtilization => MaxQueueSize > 0 ? (double)CurrentQueueSize / MaxQueueSize : 0;

        public override string ToString()
        {
            return $"Bulkhead '{BulkheadName}': Total={TotalRequests}, Success={SuccessfulRequests}, " +
                   $"Rejected={RejectedRequests}, Timeout={TimeoutRequests}, " +
                   $"CurrentExec={CurrentExecutions}/{MaxConcurrentExecutions}, " +
                   $"CurrentQueue={CurrentQueueSize}/{MaxQueueSize}";
        }
    }

    /// <summary>
    /// Exception thrown when bulkhead rejects a request
    /// </summary>
    public class BulkheadRejectedException : Exception
    {
        public BulkheadRejectedException(string message) : base(message) { }
        public BulkheadRejectedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when bulkhead times out
    /// </summary>
    public class BulkheadTimeoutException : Exception
    {
        public BulkheadTimeoutException(string message) : base(message) { }
        public BulkheadTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
