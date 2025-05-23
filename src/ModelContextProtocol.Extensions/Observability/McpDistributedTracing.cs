using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Configuration options for distributed tracing
    /// </summary>
    public class DistributedTracingOptions
    {
        /// <summary>
        /// Whether to enable distributed tracing
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Service name for tracing
        /// </summary>
        public string ServiceName { get; set; } = "mcp-server";

        /// <summary>
        /// Service version for tracing
        /// </summary>
        public string ServiceVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Sampling ratio (0.0 to 1.0)
        /// </summary>
        public double SamplingRatio { get; set; } = 0.1;

        /// <summary>
        /// Whether to trace database operations
        /// </summary>
        public bool TraceDatabaseOperations { get; set; } = true;

        /// <summary>
        /// Whether to trace HTTP requests
        /// </summary>
        public bool TraceHttpRequests { get; set; } = true;

        /// <summary>
        /// Whether to trace MCP operations
        /// </summary>
        public bool TraceMcpOperations { get; set; } = true;

        /// <summary>
        /// Maximum number of attributes per span
        /// </summary>
        public int MaxAttributesPerSpan { get; set; } = 128;

        /// <summary>
        /// OTLP endpoint for exporting traces
        /// </summary>
        public string OtlpEndpoint { get; set; }

        /// <summary>
        /// Custom resource attributes
        /// </summary>
        public Dictionary<string, string> ResourceAttributes { get; set; } = new();
    }

    /// <summary>
    /// Enhanced distributed tracing for MCP operations
    /// </summary>
    public class McpDistributedTracing
    {
        private readonly ActivitySource _activitySource;
        private readonly ILogger<McpDistributedTracing> _logger;
        private readonly DistributedTracingOptions _options;

        public McpDistributedTracing(
            ILogger<McpDistributedTracing> logger,
            IOptions<DistributedTracingOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            _activitySource = new ActivitySource(
                _options.ServiceName,
                _options.ServiceVersion);

            _logger.LogInformation("Initialized distributed tracing for service {ServiceName} v{ServiceVersion}",
                _options.ServiceName, _options.ServiceVersion);
        }

        /// <summary>
        /// Starts a new MCP operation span
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="kind">Activity kind</param>
        /// <returns>Activity instance or null if not sampling</returns>
        public Activity StartMcpOperation(string operationName, ActivityKind kind = ActivityKind.Internal)
        {
            if (!_options.EnableTracing || !_options.TraceMcpOperations)
                return null;

            var activity = _activitySource.StartActivity($"mcp.{operationName}", kind);

            if (activity != null)
            {
                activity.SetTag("mcp.operation", operationName);
                activity.SetTag("service.name", _options.ServiceName);
                activity.SetTag("service.version", _options.ServiceVersion);

                _logger.LogTrace("Started MCP operation span: {OperationName} (TraceId: {TraceId})",
                    operationName, activity.TraceId);
            }

            return activity;
        }

        /// <summary>
        /// Starts a new database operation span
        /// </summary>
        /// <param name="operation">Database operation name</param>
        /// <param name="tableName">Table or collection name</param>
        /// <param name="connectionString">Connection string (will be sanitized)</param>
        /// <returns>Activity instance or null if not sampling</returns>
        public Activity StartDatabaseOperation(string operation, string tableName = null, string connectionString = null)
        {
            if (!_options.EnableTracing || !_options.TraceDatabaseOperations)
                return null;

            var activity = _activitySource.StartActivity($"db.{operation}", ActivityKind.Client);

            if (activity != null)
            {
                activity.SetTag("db.operation", operation);
                activity.SetTag("db.system", "sqlserver");

                if (!string.IsNullOrEmpty(tableName))
                    activity.SetTag("db.name", tableName);

                if (!string.IsNullOrEmpty(connectionString))
                {
                    var sanitizedConnectionString = SanitizeConnectionString(connectionString);
                    activity.SetTag("db.connection_string", sanitizedConnectionString);
                }

                _logger.LogTrace("Started database operation span: {Operation} on {Table}",
                    operation, tableName ?? "unknown");
            }

            return activity;
        }

        /// <summary>
        /// Starts a new HTTP request span
        /// </summary>
        /// <param name="method">HTTP method</param>
        /// <param name="url">Request URL</param>
        /// <returns>Activity instance or null if not sampling</returns>
        public Activity StartHttpRequest(string method, string url)
        {
            if (!_options.EnableTracing || !_options.TraceHttpRequests)
                return null;

            var activity = _activitySource.StartActivity($"http.{method.ToLower()}", ActivityKind.Client);

            if (activity != null)
            {
                activity.SetTag("http.method", method);
                activity.SetTag("http.url", url);
                activity.SetTag("http.scheme", GetSchemeFromUrl(url));

                _logger.LogTrace("Started HTTP request span: {Method} {Url}",
                    method, url);
            }

            return activity;
        }

        /// <summary>
        /// Records an exception in the current span
        /// </summary>
        /// <param name="exception">Exception to record</param>
        /// <param name="activity">Activity to record the exception in (optional, uses current if null)</param>
        public void RecordException(Exception exception, Activity activity = null)
        {
            var currentActivity = activity ?? Activity.Current;
            if (currentActivity == null)
                return;

            currentActivity.SetStatus(ActivityStatusCode.Error, exception.Message);
            currentActivity.SetTag("error", true);
            currentActivity.SetTag("error.type", exception.GetType().Name);
            currentActivity.SetTag("error.message", exception.Message);

            if (exception.StackTrace != null)
                currentActivity.SetTag("error.stack", exception.StackTrace);

            // Add exception event
            var tags = new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.StackTrace
            };

            currentActivity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));

            _logger.LogTrace("Recorded exception in span: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }

        /// <summary>
        /// Sets the status of the current operation
        /// </summary>
        /// <param name="status">Status code</param>
        /// <param name="description">Optional status description</param>
        /// <param name="activity">Activity to set status on (optional, uses current if null)</param>
        public void SetOperationStatus(ActivityStatusCode status, string description = null, Activity activity = null)
        {
            var currentActivity = activity ?? Activity.Current;
            if (currentActivity == null)
                return;

            currentActivity.SetStatus(status, description);

            if (status == ActivityStatusCode.Error)
                currentActivity.SetTag("error", true);

            _logger.LogTrace("Set operation status: {Status} - {Description}",
                status, description ?? "N/A");
        }

        /// <summary>
        /// Adds custom attributes to the current span
        /// </summary>
        /// <param name="attributes">Dictionary of attributes to add</param>
        /// <param name="activity">Activity to add attributes to (optional, uses current if null)</param>
        public void AddAttributes(Dictionary<string, object> attributes, Activity activity = null)
        {
            var currentActivity = activity ?? Activity.Current;
            if (currentActivity == null || attributes == null)
                return;

            var attributeCount = 0;
            foreach (var kvp in attributes)
            {
                if (attributeCount >= _options.MaxAttributesPerSpan)
                {
                    _logger.LogWarning("Reached maximum attributes per span ({Max}), skipping remaining attributes",
                        _options.MaxAttributesPerSpan);
                    break;
                }

                object value = kvp.Value switch
                {
                    string s => s,
                    int i => i,
                    long l => l,
                    double d => d,
                    bool b => b,
                    _ => JsonSerializer.Serialize(kvp.Value)
                };

                currentActivity.SetTag(kvp.Key, value);
                attributeCount++;
            }

            _logger.LogTrace("Added {Count} attributes to current span", attributeCount);
        }

        /// <summary>
        /// Creates a resource for OpenTelemetry configuration
        /// </summary>
        /// <returns>Configured resource</returns>
        public Resource CreateResource()
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(_options.ServiceName, _options.ServiceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment",
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown"),
                    new KeyValuePair<string, object>("host.name", Environment.MachineName),
                    new KeyValuePair<string, object>("process.pid", Environment.ProcessId)
                });

            // Add custom resource attributes
            foreach (var attr in _options.ResourceAttributes)
            {
                resourceBuilder.AddAttributes(new[]
                {
                    new KeyValuePair<string, object>(attr.Key, attr.Value)
                });
            }

            return resourceBuilder.Build();
        }

        private string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return connectionString;

            // Remove sensitive information from connection string
            var parts = connectionString.Split(';');
            var sanitized = new List<string>();

            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLowerInvariant();

                    // Skip sensitive keys
                    if (key.Contains("password") || key.Contains("pwd") ||
                        key.Contains("secret") || key.Contains("key"))
                    {
                        sanitized.Add($"{keyValue[0]}=***");
                    }
                    else
                    {
                        sanitized.Add(part);
                    }
                }
                else
                {
                    sanitized.Add(part);
                }
            }

            return string.Join(';', sanitized);
        }

        private string GetSchemeFromUrl(string url)
        {
            try
            {
                return new Uri(url).Scheme;
            }
            catch
            {
                return "unknown";
            }
        }

        public void Dispose()
        {
            _activitySource?.Dispose();
        }
    }
}
