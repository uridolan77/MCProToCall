using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Testing.Chaos
{
    /// <summary>
    /// Middleware for injecting chaos faults for testing resilience
    /// </summary>
    public class ChaosMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ChaosOptions _options;
        private readonly ILogger<ChaosMiddleware> _logger;
        private readonly Random _random;

        public ChaosMiddleware(
            RequestDelegate next,
            IOptions<ChaosOptions> options,
            ILogger<ChaosMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
            _random = new Random();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only inject faults if chaos is enabled and we're not in production
            if (!_options.Enabled || _options.Environment == "Production")
            {
                await _next(context);
                return;
            }

            // Check if this request should have a fault injected
            if (ShouldInjectFault(context))
            {
                var fault = SelectFault();

                _logger.LogWarning(
                    "Injecting chaos fault {FaultType} for request {RequestId}",
                    fault.GetType().Name, context.TraceIdentifier);

                try
                {
                    await fault.ExecuteAsync(context);
                    return; // Fault handled the request
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Chaos fault {FaultType} threw an exception for request {RequestId}",
                        fault.GetType().Name, context.TraceIdentifier);

                    // Continue with normal processing if fault fails
                }
            }

            await _next(context);
        }

        private bool ShouldInjectFault(HttpContext context)
        {
            // Don't inject faults for health checks or monitoring endpoints
            var path = context.Request.Path.Value?.ToLowerInvariant();
            if (path != null && (_options.ExcludedPaths.Any(p => path.Contains(p))))
            {
                return false;
            }

            // Check probability
            if (_random.NextDouble() >= _options.FaultProbability)
            {
                return false;
            }

            // Check if this endpoint is targeted
            if (_options.TargetedEndpoints.Any() &&
                !_options.TargetedEndpoints.Any(e => path?.Contains(e) == true))
            {
                return false;
            }

            return true;
        }

        private IChaosFault SelectFault()
        {
            if (!_options.Faults.Any())
            {
                return new LatencyFault(TimeSpan.FromMilliseconds(100));
            }

            var totalWeight = _options.Faults.Sum(f => f.Weight);
            var randomValue = _random.NextDouble() * totalWeight;
            var currentWeight = 0.0;

            foreach (var faultConfig in _options.Faults)
            {
                currentWeight += faultConfig.Weight;
                if (randomValue <= currentWeight)
                {
                    return CreateFault(faultConfig);
                }
            }

            // Fallback to first fault
            return CreateFault(_options.Faults.First());
        }

        private IChaosFault CreateFault(ChaosFaultConfiguration config)
        {
            return config.Type.ToLowerInvariant() switch
            {
                "latency" => new LatencyFault(TimeSpan.FromMilliseconds(GetDoubleParameter(config.Parameters, "delayMs", 1000))),
                "error" => new ErrorFault(GetIntParameter(config.Parameters, "statusCode", 500)),
                "timeout" => new TimeoutFault(TimeSpan.FromMilliseconds(GetDoubleParameter(config.Parameters, "timeoutMs", 30000))),
                "exception" => new ExceptionFault(GetStringParameter(config.Parameters, "message", "Chaos exception")),
                "memory" => new MemoryPressureFault(GetIntParameter(config.Parameters, "sizeMb", 100)),
                _ => new LatencyFault(TimeSpan.FromMilliseconds(100))
            };
        }

        private double GetDoubleParameter(Dictionary<string, object> parameters, string key, double defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return value switch
                {
                    double d => d,
                    int i => i,
                    string s when double.TryParse(s, out var parsed) => parsed,
                    _ => defaultValue
                };
            }
            return defaultValue;
        }

        private int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return value switch
                {
                    int i => i,
                    double d => (int)d,
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => defaultValue
                };
            }
            return defaultValue;
        }

        private string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// Configuration options for chaos engineering
    /// </summary>
    public class ChaosOptions
    {
        /// <summary>
        /// Whether chaos engineering is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Environment name (chaos is disabled in Production)
        /// </summary>
        public string Environment { get; set; } = "Development";

        /// <summary>
        /// Probability of injecting a fault (0.0 to 1.0)
        /// </summary>
        public double FaultProbability { get; set; } = 0.1;

        /// <summary>
        /// Paths to exclude from chaos injection
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new()
        {
            "/health",
            "/metrics",
            "/ready",
            "/live"
        };

        /// <summary>
        /// Specific endpoints to target (empty means all endpoints)
        /// </summary>
        public List<string> TargetedEndpoints { get; set; } = new();

        /// <summary>
        /// Available chaos faults with their weights
        /// </summary>
        public List<ChaosFaultConfiguration> Faults { get; set; } = new()
        {
            new() { Type = "latency", Weight = 0.4, Parameters = new() { ["delayMs"] = 500 } },
            new() { Type = "error", Weight = 0.3, Parameters = new() { ["statusCode"] = 500 } },
            new() { Type = "timeout", Weight = 0.2, Parameters = new() { ["timeoutMs"] = 10000 } },
            new() { Type = "exception", Weight = 0.1, Parameters = new() { ["message"] = "Chaos exception" } }
        };
    }

    /// <summary>
    /// Configuration for a specific chaos fault
    /// </summary>
    public class ChaosFaultConfiguration
    {
        /// <summary>
        /// Type of fault (latency, error, timeout, exception, memory)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Weight for random selection (higher = more likely)
        /// </summary>
        public double Weight { get; set; } = 1.0;

        /// <summary>
        /// Parameters specific to the fault type
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Interface for chaos faults
    /// </summary>
    public interface IChaosFault
    {
        /// <summary>
        /// Executes the chaos fault
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Task representing the fault execution</returns>
        Task ExecuteAsync(HttpContext context);
    }
}
