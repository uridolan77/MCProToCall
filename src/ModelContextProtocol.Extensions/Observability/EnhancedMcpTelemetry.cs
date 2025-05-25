using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Observability;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Enhanced MCP telemetry with custom metrics and detailed instrumentation
    /// </summary>
    public class EnhancedMcpTelemetry : McpTelemetry
    {
        private readonly Histogram<double> _certificateValidationDuration;
        private readonly Counter<long> _securityViolations;
        private readonly UpDownCounter<long> _activeConnections;
        private readonly Counter<long> _requestsTotal;
        private readonly Histogram<double> _requestDuration;
        private readonly Counter<long> _errorsTotal;
        private readonly Histogram<double> _hsmOperationDuration;
        private readonly Counter<long> _circuitBreakerEvents;
        private readonly Histogram<double> _rateLimitingDelay;

        public EnhancedMcpTelemetry(ILogger<McpTelemetry> logger) : base(logger)
        {
            var meter = new Meter(MeterName, "1.0.0");

            _certificateValidationDuration = meter.CreateHistogram<double>(
                "mcp.certificate.validation.duration",
                unit: "ms",
                description: "Time taken to validate certificates");

            _securityViolations = meter.CreateCounter<long>(
                "mcp.security.violations.total",
                description: "Total number of security violations detected");

            _activeConnections = meter.CreateUpDownCounter<long>(
                "mcp.connections.active.detailed",
                description: "Active connections by type and endpoint");

            _requestsTotal = meter.CreateCounter<long>(
                "mcp.requests.total",
                description: "Total number of requests processed");

            _requestDuration = meter.CreateHistogram<double>(
                "mcp.request.duration",
                unit: "ms",
                description: "Request processing duration");

            _errorsTotal = meter.CreateCounter<long>(
                "mcp.errors.total",
                description: "Total number of errors encountered");

            _hsmOperationDuration = meter.CreateHistogram<double>(
                "mcp.hsm.operation.duration",
                unit: "ms",
                description: "HSM operation duration");

            _circuitBreakerEvents = meter.CreateCounter<long>(
                "mcp.circuit_breaker.events.total",
                description: "Circuit breaker state change events");

            _rateLimitingDelay = meter.CreateHistogram<double>(
                "mcp.rate_limiting.delay",
                unit: "ms",
                description: "Delay imposed by rate limiting");
        }

        /// <summary>
        /// Records certificate validation metrics
        /// </summary>
        public void RecordCertificateValidation(string validationType, double durationMs, bool successful, string endpoint = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("validation_type", validationType),
                new("result", successful ? "success" : "failure")
            };

            if (!string.IsNullOrEmpty(endpoint))
                tags.Add(new("endpoint", endpoint));

            _certificateValidationDuration.Record(durationMs, tags.ToArray());
        }

        /// <summary>
        /// Records security violation events
        /// </summary>
        public void RecordSecurityViolation(string violationType, string endpoint, string clientId = null, string details = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("violation_type", violationType),
                new("endpoint", endpoint)
            };

            if (!string.IsNullOrEmpty(clientId))
                tags.Add(new("client_id", clientId));

            _securityViolations.Add(1, tags.ToArray());

            // Log the security violation for audit purposes
            Logger.LogWarning("Security violation detected: {ViolationType} from {ClientId} at {Endpoint}. Details: {Details}",
                violationType, clientId ?? "unknown", endpoint, details ?? "none");
        }

        /// <summary>
        /// Records active connection changes
        /// </summary>
        public void RecordConnectionChange(string connectionType, string endpoint, int delta)
        {
            _activeConnections.Add(delta,
                new KeyValuePair<string, object>("connection_type", connectionType),
                new KeyValuePair<string, object>("endpoint", endpoint));
        }

        /// <summary>
        /// Records request processing metrics
        /// </summary>
        public void RecordRequest(string method, string endpoint, double durationMs, bool successful, string errorType = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("method", method),
                new("endpoint", endpoint),
                new("result", successful ? "success" : "failure")
            };

            if (!successful && !string.IsNullOrEmpty(errorType))
                tags.Add(new("error_type", errorType));

            _requestsTotal.Add(1, tags.ToArray());
            _requestDuration.Record(durationMs, tags.ToArray());

            if (!successful)
            {
                _errorsTotal.Add(1,
                    new KeyValuePair<string, object>("method", method),
                    new KeyValuePair<string, object>("endpoint", endpoint),
                    new KeyValuePair<string, object>("error_type", errorType ?? "unknown"));
            }
        }

        /// <summary>
        /// Records HSM operation metrics
        /// </summary>
        public void RecordHsmOperation(string operation, string providerType, double durationMs, bool successful, string keyName = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("operation", operation),
                new("provider_type", providerType),
                new("result", successful ? "success" : "failure")
            };

            if (!string.IsNullOrEmpty(keyName))
                tags.Add(new("key_name", keyName));

            _hsmOperationDuration.Record(durationMs, tags.ToArray());
        }

        /// <summary>
        /// Records circuit breaker events
        /// </summary>
        public void RecordCircuitBreakerEvent(string serviceName, string eventType, string previousState = null, string newState = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("service_name", serviceName),
                new("event_type", eventType)
            };

            if (!string.IsNullOrEmpty(previousState))
                tags.Add(new("previous_state", previousState));

            if (!string.IsNullOrEmpty(newState))
                tags.Add(new("new_state", newState));

            _circuitBreakerEvents.Add(1, tags.ToArray());

            Logger.LogInformation("Circuit breaker event: {EventType} for service {ServiceName}. State: {PreviousState} -> {NewState}",
                eventType, serviceName, previousState ?? "unknown", newState ?? "unknown");
        }

        /// <summary>
        /// Records rate limiting delays
        /// </summary>
        public void RecordRateLimitingDelay(string clientId, string endpoint, double delayMs, string limitType)
        {
            _rateLimitingDelay.Record(delayMs,
                new KeyValuePair<string, object>("client_id", clientId),
                new KeyValuePair<string, object>("endpoint", endpoint),
                new KeyValuePair<string, object>("limit_type", limitType));
        }

        /// <summary>
        /// Records custom business metrics
        /// </summary>
        public void RecordCustomMetric(string metricName, double value, params (string key, object value)[] tags)
        {
            // For custom metrics, we'll use the base telemetry logging
            var tagDict = new Dictionary<string, object>();
            foreach (var (key, tagValue) in tags)
            {
                tagDict[key] = tagValue;
            }

            Logger.LogInformation("Custom metric: {MetricName} = {Value}, Tags: {@Tags}",
                metricName, value, tagDict);
        }

        /// <summary>
        /// Records performance benchmarks
        /// </summary>
        public void RecordPerformanceBenchmark(string benchmarkName, double durationMs, int iterations, string component = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("benchmark_name", benchmarkName),
                new("iterations", iterations)
            };

            if (!string.IsNullOrEmpty(component))
                tags.Add(new("component", component));

            _requestDuration.Record(durationMs, tags.ToArray());

            Logger.LogInformation("Performance benchmark: {BenchmarkName} completed in {Duration}ms over {Iterations} iterations",
                benchmarkName, durationMs, iterations);
        }

        /// <summary>
        /// Records resource utilization metrics
        /// </summary>
        public void RecordResourceUtilization(string resourceType, double utilizationPercent, string resourceId = null)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("resource_type", resourceType),
                new("utilization_percent", utilizationPercent)
            };

            if (!string.IsNullOrEmpty(resourceId))
                tags.Add(new("resource_id", resourceId));

            Logger.LogDebug("Resource utilization: {ResourceType} at {Utilization}%",
                resourceType, utilizationPercent);
        }

        /// <summary>
        /// Records cache performance metrics
        /// </summary>
        public void RecordCacheMetrics(string cacheType, string operation, bool hit, double durationMs = 0)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new("cache_type", cacheType),
                new("operation", operation),
                new("result", hit ? "hit" : "miss")
            };

            if (durationMs > 0)
            {
                _requestDuration.Record(durationMs, tags.ToArray());
            }

            Logger.LogDebug("Cache {Operation}: {Result} for {CacheType}",
                operation, hit ? "hit" : "miss", cacheType);
        }
    }
}
