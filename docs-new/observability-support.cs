using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Extension methods for configuring OpenTelemetry
    /// </summary>
    public static class ObservabilityExtensions
    {
        public static IServiceCollection AddMcpObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName = "MCP-Service")
        {
            var otlpEndpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
            
            // Configure OpenTelemetry
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: serviceName)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("service.version", "1.0.0"),
                        new KeyValuePair<string, object>("deployment.environment", 
                            configuration["Environment"] ?? "production")
                    }))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(McpTelemetry.ActivitySourceName)
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddConsoleExporter())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(McpTelemetry.MeterName)
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddConsoleExporter());

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddOpenTelemetry(logging =>
                {
                    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName));
                    logging.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                });
            });

            // Register telemetry service
            services.AddSingleton<IMcpTelemetry, McpTelemetry>();

            return services;
        }
    }

    /// <summary>
    /// Interface for MCP telemetry operations
    /// </summary>
    public interface IMcpTelemetry
    {
        Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
        void RecordRequestReceived(string method);
        void RecordRequestCompleted(string method, bool success, double durationMs);
        void RecordError(string method, string errorType);
        void RecordConnectionEvent(string eventType, string clientId);
        void RecordSecurityEvent(string eventType, string details);
    }

    /// <summary>
    /// MCP telemetry implementation
    /// </summary>
    public class McpTelemetry : IMcpTelemetry
    {
        public const string ActivitySourceName = "ModelContextProtocol";
        public const string MeterName = "ModelContextProtocol";

        private readonly ActivitySource _activitySource;
        private readonly Meter _meter;
        private readonly ILogger<McpTelemetry> _logger;

        // Metrics
        private readonly Counter<long> _requestCounter;
        private readonly Histogram<double> _requestDuration;
        private readonly Counter<long> _errorCounter;
        private readonly Counter<long> _connectionCounter;
        private readonly Counter<long> _securityEventCounter;
        private readonly UpDownCounter<int> _activeConnections;

        public McpTelemetry(ILogger<McpTelemetry> logger)
        {
            _logger = logger;
            _activitySource = new ActivitySource(ActivitySourceName, "1.0.0");
            _meter = new Meter(MeterName, "1.0.0");

            // Initialize metrics
            _requestCounter = _meter.CreateCounter<long>(
                "mcp.requests.total",
                description: "Total number of MCP requests");

            _requestDuration = _meter.CreateHistogram<double>(
                "mcp.request.duration",
                unit: "ms",
                description: "Duration of MCP request processing");

            _errorCounter = _meter.CreateCounter<long>(
                "mcp.errors.total",
                description: "Total number of MCP errors");

            _connectionCounter = _meter.CreateCounter<long>(
                "mcp.connections.total",
                description: "Total number of connection events");

            _securityEventCounter = _meter.CreateCounter<long>(
                "mcp.security.events.total",
                description: "Total number of security events");

            _activeConnections = _meter.CreateUpDownCounter<int>(
                "mcp.connections.active",
                description: "Number of active connections");
        }

        public Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            var activity = _activitySource.StartActivity(name, kind);
            activity?.SetTag("mcp.component", "server");
            return activity;
        }

        public void RecordRequestReceived(string method)
        {
            _requestCounter.Add(1, 
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("status", "received"));

            _logger.LogInformation("MCP request received: {Method}", method);
        }

        public void RecordRequestCompleted(string method, bool success, double durationMs)
        {
            var status = success ? "success" : "failure";
            
            _requestCounter.Add(1,
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("status", "completed"),
                new KeyValuePair<string, object>("result", status));

            _requestDuration.Record(durationMs,
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("status", status));

            _logger.LogInformation("MCP request completed: {Method} [{Status}] in {Duration}ms", 
                method, status, durationMs);
        }

        public void RecordError(string method, string errorType)
        {
            _errorCounter.Add(1,
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("error_type", errorType));

            _logger.LogWarning("MCP error occurred: {Method} - {ErrorType}", method, errorType);
        }

        public void RecordConnectionEvent(string eventType, string clientId)
        {
            _connectionCounter.Add(1,
                new KeyValuePair<string, object>("event_type", eventType),
                new KeyValuePair<string, object>("client_id", clientId));

            if (eventType == "connected")
            {
                _activeConnections.Add(1);
            }
            else if (eventType == "disconnected")
            {
                _activeConnections.Add(-1);
            }

            _logger.LogInformation("Connection event: {EventType} for client {ClientId}", 
                eventType, clientId);
        }

        public void RecordSecurityEvent(string eventType, string details)
        {
            _securityEventCounter.Add(1,
                new KeyValuePair<string, object>("event_type", eventType));

            _logger.LogWarning("Security event: {EventType} - {Details}", eventType, details);
        }
    }

    /// <summary>
    /// Middleware for automatic telemetry collection
    /// </summary>
    public class TelemetryMiddleware
    {
        private readonly IMcpTelemetry _telemetry;
        private readonly ILogger<TelemetryMiddleware> _logger;

        public TelemetryMiddleware(
            IMcpTelemetry telemetry,
            ILogger<TelemetryMiddleware> logger)
        {
            _telemetry = telemetry;
            _logger = logger;
        }

        public async Task<JsonRpcResponse> InvokeAsync(
            JsonRpcRequest request,
            Func<JsonRpcRequest, Task<JsonRpcResponse>> next)
        {
            var stopwatch = Stopwatch.StartNew();
            
            using var activity = _telemetry.StartActivity($"mcp.request.{request.Method}");
            activity?.SetTag("mcp.method", request.Method);
            activity?.SetTag("mcp.request_id", request.Id);

            _telemetry.RecordRequestReceived(request.Method);

            try
            {
                var response = await next(request);
                
                stopwatch.Stop();
                
                var isSuccess = response is not JsonRpcErrorResponse;
                _telemetry.RecordRequestCompleted(request.Method, isSuccess, stopwatch.ElapsedMilliseconds);
                
                activity?.SetTag("mcp.status", isSuccess ? "success" : "error");
                
                if (response is JsonRpcErrorResponse errorResponse)
                {
                    activity?.SetTag("mcp.error_code", errorResponse.Error.Code);
                    activity?.SetTag("mcp.error_message", errorResponse.Error.Message);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _telemetry.RecordRequestCompleted(request.Method, false, stopwatch.ElapsedMilliseconds);
                _telemetry.RecordError(request.Method, ex.GetType().Name);
                
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                
                throw;
            }
        }
    }

    /// <summary>
    /// Health check service for MCP
    /// </summary>
    public class McpHealthCheck : IHealthCheck
    {
        private readonly IMcpServer _server;
        private readonly ILogger<McpHealthCheck> _logger;

        public McpHealthCheck(IMcpServer server, ILogger<McpHealthCheck> logger)
        {
            _server = server;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if server is responding
                var testRequest = new JsonRpcRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    Method = "system.ping",
                    Params = JsonDocument.Parse("{}").RootElement
                };

                var response = await _server.HandleRequestAsync(testRequest);
                
                if (response is JsonRpcErrorResponse)
                {
                    return HealthCheckResult.Degraded("Server is responding but returned an error");
                }

                return HealthCheckResult.Healthy("MCP server is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return HealthCheckResult.Unhealthy("MCP server is not responding", ex);
            }
        }
    }
}