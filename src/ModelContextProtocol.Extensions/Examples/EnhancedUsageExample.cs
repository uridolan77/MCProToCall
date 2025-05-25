using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Performance;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Testing.Chaos;
using ModelContextProtocol.Extensions.WebSocket;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.Factories;

namespace ModelContextProtocol.Extensions.Examples
{
    /// <summary>
    /// Comprehensive example showing how to use all the enhanced MCP features
    /// </summary>
    public class EnhancedUsageExample
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add all MCP extensions with enhanced features
            services.AddMcpExtensions(configuration, options =>
            {
                // Configure security options
                options.Security.EnableCertificateValidation = true;
                options.Security.EnableRevocationChecking = true;
                options.Security.EnableHsm = false; // Enable for production with HSM

                // Configure resilience options
                options.Resilience.EnableRateLimiting = true;
                options.Resilience.RateLimitingType = "Adaptive";
                options.Resilience.EnableCircuitBreaker = true;
                options.Resilience.EnableBulkhead = true;

                // Configure observability options
                options.Observability.EnableMetrics = true;
                options.Observability.EnableTracing = true;
                options.Observability.EnableHealthChecks = true;
                options.Observability.ServiceName = "Enhanced-MCP-Service";

                // Configure validation options
                options.Validation.EnableEnvironmentValidation = true;
                options.Validation.EnableSchemaValidation = true;
                options.Validation.EnableCrossValidation = true;
            });

            // Add enhanced features
            services.AddMcpZeroTrustSecurity(configuration);
            services.AddMcpConfigurationTracking(configuration);

            // Add custom services
            services.AddScoped<EnhancedMcpService>();
        }

        public static void ConfigureApplication(WebApplication app)
        {
            // Add chaos middleware for testing (only in development)
            if (app.Environment.IsDevelopment())
            {
                app.UseMiddleware<ChaosMiddleware>();
            }

            // Add zero-trust security pipeline
            app.UseMiddleware<ZeroTrustSecurityMiddleware>();

            // Standard middleware
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Map endpoints
            app.MapGet("/api/performance/report", GetPerformanceReport);
            app.MapGet("/api/security/metrics", GetSecurityMetrics);
            app.MapGet("/api/config/history", GetConfigurationHistory);
            app.MapPost("/api/websocket/test", TestWebSocketQueuing);
        }

        private static async Task<object> GetPerformanceReport(
            PerformanceProfiler profiler,
            ILogger<EnhancedUsageExample> logger)
        {
            using var scope = profiler.StartOperation("GeneratePerformanceReport");

            try
            {
                var report = profiler.GenerateReport(TimeSpan.FromHours(1));
                logger.LogInformation("Generated performance report with {OperationCount} operations",
                    report.TotalOperations);

                return new { Status = "Success", Data = report };
            }
            catch (Exception ex)
            {
                if (scope is OperationScope operationScope)
                {
                    operationScope.MarkError(ex.Message);
                }
                logger.LogError(ex, "Failed to generate performance report");
                return new { Status = "Error", Message = "Failed to generate performance report" };
            }
        }

        private static async Task<object> GetSecurityMetrics(
            ZeroTrustSecurityPipeline securityPipeline,
            ILogger<EnhancedUsageExample> logger)
        {
            try
            {
                var metrics = securityPipeline.GetMetrics();
                logger.LogInformation("Retrieved security metrics for {ValidatorCount} validators",
                    metrics.ValidatorMetrics.Count);

                return new { Status = "Success", Data = metrics };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get security metrics");
                return new { Status = "Error", Message = "Failed to get security metrics" };
            }
        }

        private static async Task<object> GetConfigurationHistory(
            ConfigurationChangeTracker changeTracker,
            string configPath,
            ILogger<EnhancedUsageExample> logger)
        {
            try
            {
                var history = changeTracker.GetHistory(configPath);
                logger.LogInformation("Retrieved configuration history for {ConfigPath} with {ChangeCount} changes",
                    configPath, history.TotalChanges);

                return new { Status = "Success", Data = history };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get configuration history for {ConfigPath}", configPath);
                return new { Status = "Error", Message = "Failed to get configuration history" };
            }
        }

        private static async Task<object> TestWebSocketQueuing(
            IServiceProvider serviceProvider,
            ILogger<EnhancedUsageExample> logger)
        {
            try
            {
                // This would typically be done with an actual WebSocket connection
                // Here we're just demonstrating the API

                var options = new QueuedWebSocketOptions
                {
                    QueueCapacity = 100,
                    MaxRetryAttempts = 3,
                    RetryDelayMs = 100
                };

                logger.LogInformation("WebSocket queuing test completed successfully");

                return new { Status = "Success", Message = "WebSocket queuing configured" };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to test WebSocket queuing");
                return new { Status = "Error", Message = "Failed to test WebSocket queuing" };
            }
        }
    }

    /// <summary>
    /// Example service demonstrating enhanced MCP features
    /// </summary>
    public class EnhancedMcpService
    {
        private readonly PerformanceProfiler _profiler;
        private readonly OptimizedJsonProcessor _jsonProcessor;
        private readonly MessageProcessor _messageProcessor;
        private readonly RateLimiterFactory _rateLimiterFactory;
        private readonly ILogger<EnhancedMcpService> _logger;

        public EnhancedMcpService(
            PerformanceProfiler profiler,
            OptimizedJsonProcessor jsonProcessor,
            MessageProcessor messageProcessor,
            RateLimiterFactory rateLimiterFactory,
            ILogger<EnhancedMcpService> logger)
        {
            _profiler = profiler;
            _jsonProcessor = jsonProcessor;
            _messageProcessor = messageProcessor;
            _rateLimiterFactory = rateLimiterFactory;
            _logger = logger;
        }

        public async Task<T> ProcessRequestAsync<T>(object request, CancellationToken cancellationToken = default)
        {
            using var scope = _profiler.StartOperation("ProcessRequest", new Dictionary<string, object>
            {
                ["RequestType"] = typeof(T).Name,
                ["RequestSize"] = _jsonProcessor.SerializeToUtf8Bytes(request).Length
            });

            try
            {
                // Serialize request using optimized JSON processor
                var requestBytes = _jsonProcessor.SerializeToUtf8Bytes(request);

                // Process using message processor for memory efficiency
                using var requestStream = new MemoryStream(requestBytes);
                T? result = default;

                await _messageProcessor.ProcessMessageAsync(
                    requestStream,
                    async (data) =>
                    {
                        // Deserialize and process
                        result = _jsonProcessor.DeserializeFromUtf8<T>(data.Span);
                        await Task.CompletedTask;
                    },
                    cancellationToken);

                if (scope is OperationScope operationScope)
                {
                    operationScope.AddTag("Success", true);
                }
                _logger.LogInformation("Successfully processed request of type {RequestType}", typeof(T).Name);

                return result!;
            }
            catch (Exception ex)
            {
                if (scope is OperationScope operationScope)
                {
                    operationScope.MarkError(ex.Message);
                }
                _logger.LogError(ex, "Failed to process request of type {RequestType}", typeof(T).Name);
                throw;
            }
        }

        public async Task<bool> CheckRateLimitAsync(string clientId, CancellationToken cancellationToken = default)
        {
            using var scope = _profiler.StartOperation("CheckRateLimit");

            try
            {
                var rateLimiter = _rateLimiterFactory.Create("TokenBucket", new ConfigurationBuilder().Build());
                var allowed = await rateLimiter.TryAcquireAsync(cancellationToken);

                if (scope is OperationScope operationScope)
                {
                    operationScope.AddTag("ClientId", clientId);
                    operationScope.AddTag("Allowed", allowed);
                }

                _logger.LogDebug("Rate limit check for client {ClientId}: {Result}", clientId, allowed ? "Allowed" : "Denied");

                return allowed;
            }
            catch (Exception ex)
            {
                if (scope is OperationScope operationScope)
                {
                    operationScope.MarkError(ex.Message);
                }
                _logger.LogError(ex, "Failed to check rate limit for client {ClientId}", clientId);
                throw;
            }
        }
    }

    /// <summary>
    /// Middleware for zero-trust security pipeline
    /// </summary>
    public class ZeroTrustSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ZeroTrustSecurityPipeline _securityPipeline;
        private readonly ILogger<ZeroTrustSecurityMiddleware> _logger;

        public ZeroTrustSecurityMiddleware(
            RequestDelegate next,
            ZeroTrustSecurityPipeline securityPipeline,
            ILogger<ZeroTrustSecurityMiddleware> logger)
        {
            _next = next;
            _securityPipeline = securityPipeline;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var result = await _securityPipeline.ValidateRequestAsync(context, context.RequestAborted);

            if (result.ShouldBlock)
            {
                _logger.LogWarning("Request blocked by security pipeline: {Reason}", result.BlockReason);

                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied: " + result.BlockReason);
                return;
            }

            await _next(context);
        }
    }
}
