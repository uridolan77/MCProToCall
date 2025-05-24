using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Testing.Chaos
{
    /// <summary>
    /// Interface for chaos engineering policies
    /// </summary>
    public interface IChaosPolicy
    {
        /// <summary>
        /// Executes an operation with potential chaos injection
        /// </summary>
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation);

        /// <summary>
        /// Executes an operation with potential chaos injection
        /// </summary>
        Task ExecuteAsync(Func<Task> operation);
    }

    /// <summary>
    /// Configurable chaos policy for testing resilience
    /// </summary>
    public class ConfigurableChaosPolicy : IChaosPolicy
    {
        private readonly ILogger<ConfigurableChaosPolicy> _logger;
        private readonly ChaosConfiguration _configuration;
        private readonly Random _random = new Random();
        private long _executionCount;

        public ConfigurableChaosPolicy(
            ILogger<ConfigurableChaosPolicy> logger,
            ChaosConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            var executionNumber = Interlocked.Increment(ref _executionCount);
            
            if (!_configuration.Enabled)
            {
                return await operation();
            }

            _logger.LogTrace("Chaos policy executing operation #{ExecutionNumber}", executionNumber);

            // Check if we should inject failure
            if (ShouldInjectFailure())
            {
                var exception = CreateChaosException();
                _logger.LogDebug("Injecting chaos failure: {Exception}", exception.Message);
                throw exception;
            }

            // Check if we should inject delay
            if (ShouldInjectDelay())
            {
                var delay = GetRandomDelay();
                _logger.LogDebug("Injecting chaos delay: {Delay}ms", delay.TotalMilliseconds);
                await Task.Delay(delay);
            }

            // Check if we should inject timeout
            if (ShouldInjectTimeout())
            {
                var timeout = GetRandomTimeout();
                _logger.LogDebug("Injecting chaos timeout: {Timeout}ms", timeout.TotalMilliseconds);
                
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    return await operation();
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    throw new ChaosTimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms due to chaos injection");
                }
            }

            // Execute normally
            return await operation();
        }

        public async Task ExecuteAsync(Func<Task> operation)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Resets the chaos policy state
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _executionCount, 0);
        }

        private bool ShouldInjectFailure()
        {
            return _random.NextDouble() < _configuration.FailureRate;
        }

        private bool ShouldInjectDelay()
        {
            return _random.NextDouble() < _configuration.DelayRate;
        }

        private bool ShouldInjectTimeout()
        {
            return _random.NextDouble() < _configuration.TimeoutRate;
        }

        private Exception CreateChaosException()
        {
            var exceptionTypes = _configuration.ExceptionTypes;
            if (exceptionTypes.Count == 0)
            {
                return new ChaosInjectionException("Chaos failure injected");
            }

            var exceptionType = exceptionTypes[_random.Next(exceptionTypes.Count)];
            var message = $"Chaos {exceptionType.Name} injected";

            try
            {
                return (Exception)Activator.CreateInstance(exceptionType, message);
            }
            catch
            {
                return new ChaosInjectionException(message);
            }
        }

        private TimeSpan GetRandomDelay()
        {
            var minMs = _configuration.MinDelayMs;
            var maxMs = _configuration.MaxDelayMs;
            var delayMs = _random.Next(minMs, maxMs + 1);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private TimeSpan GetRandomTimeout()
        {
            var minMs = _configuration.MinTimeoutMs;
            var maxMs = _configuration.MaxTimeoutMs;
            var timeoutMs = _random.Next(minMs, maxMs + 1);
            return TimeSpan.FromMilliseconds(timeoutMs);
        }
    }

    /// <summary>
    /// Configuration for chaos engineering
    /// </summary>
    public class ChaosConfiguration
    {
        /// <summary>
        /// Whether chaos injection is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Probability of injecting a failure (0.0 to 1.0)
        /// </summary>
        public double FailureRate { get; set; } = 0.1;

        /// <summary>
        /// Probability of injecting a delay (0.0 to 1.0)
        /// </summary>
        public double DelayRate { get; set; } = 0.2;

        /// <summary>
        /// Probability of injecting a timeout (0.0 to 1.0)
        /// </summary>
        public double TimeoutRate { get; set; } = 0.05;

        /// <summary>
        /// Minimum delay in milliseconds
        /// </summary>
        public int MinDelayMs { get; set; } = 100;

        /// <summary>
        /// Maximum delay in milliseconds
        /// </summary>
        public int MaxDelayMs { get; set; } = 5000;

        /// <summary>
        /// Minimum timeout in milliseconds
        /// </summary>
        public int MinTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// Maximum timeout in milliseconds
        /// </summary>
        public int MaxTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Types of exceptions to inject
        /// </summary>
        public List<Type> ExceptionTypes { get; set; } = new List<Type>
        {
            typeof(InvalidOperationException),
            typeof(TimeoutException),
            typeof(ArgumentException)
        };
    }

    /// <summary>
    /// Builder for chaos configuration
    /// </summary>
    public class ChaosConfigurationBuilder
    {
        private readonly ChaosConfiguration _configuration = new ChaosConfiguration();

        /// <summary>
        /// Enables chaos injection
        /// </summary>
        public ChaosConfigurationBuilder Enable()
        {
            _configuration.Enabled = true;
            return this;
        }

        /// <summary>
        /// Sets the failure injection rate
        /// </summary>
        public ChaosConfigurationBuilder WithFailureRate(double rate)
        {
            if (rate < 0 || rate > 1)
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between 0 and 1");
            
            _configuration.FailureRate = rate;
            return this;
        }

        /// <summary>
        /// Sets the delay injection rate
        /// </summary>
        public ChaosConfigurationBuilder WithDelayRate(double rate)
        {
            if (rate < 0 || rate > 1)
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between 0 and 1");
            
            _configuration.DelayRate = rate;
            return this;
        }

        /// <summary>
        /// Sets the timeout injection rate
        /// </summary>
        public ChaosConfigurationBuilder WithTimeoutRate(double rate)
        {
            if (rate < 0 || rate > 1)
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between 0 and 1");
            
            _configuration.TimeoutRate = rate;
            return this;
        }

        /// <summary>
        /// Sets the delay range
        /// </summary>
        public ChaosConfigurationBuilder WithDelayRange(TimeSpan min, TimeSpan max)
        {
            _configuration.MinDelayMs = (int)min.TotalMilliseconds;
            _configuration.MaxDelayMs = (int)max.TotalMilliseconds;
            return this;
        }

        /// <summary>
        /// Sets the timeout range
        /// </summary>
        public ChaosConfigurationBuilder WithTimeoutRange(TimeSpan min, TimeSpan max)
        {
            _configuration.MinTimeoutMs = (int)min.TotalMilliseconds;
            _configuration.MaxTimeoutMs = (int)max.TotalMilliseconds;
            return this;
        }

        /// <summary>
        /// Adds exception types to inject
        /// </summary>
        public ChaosConfigurationBuilder WithExceptionTypes(params Type[] exceptionTypes)
        {
            _configuration.ExceptionTypes.Clear();
            _configuration.ExceptionTypes.AddRange(exceptionTypes);
            return this;
        }

        /// <summary>
        /// Builds the chaos configuration
        /// </summary>
        public ChaosConfiguration Build()
        {
            return _configuration;
        }
    }

    /// <summary>
    /// Exception thrown by chaos injection
    /// </summary>
    public class ChaosInjectionException : Exception
    {
        public ChaosInjectionException(string message) : base(message) { }
        public ChaosInjectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when chaos timeout is injected
    /// </summary>
    public class ChaosTimeoutException : TimeoutException
    {
        public ChaosTimeoutException(string message) : base(message) { }
        public ChaosTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
