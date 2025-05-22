using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// MCP telemetry implementation
    /// </summary>
    public class McpTelemetry : IMcpTelemetry
    {
        /// <summary>
        /// Activity source name for tracing
        /// </summary>
        public const string ActivitySourceName = "ModelContextProtocol";
        
        /// <summary>
        /// Meter name for metrics
        /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="McpTelemetry"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
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

        /// <inheritdoc/>
        public Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            var activity = _activitySource.StartActivity(name, kind);
            activity?.SetTag("mcp.component", "server");
            return activity;
        }

        /// <inheritdoc/>
        public void RecordRequestReceived(string method)
        {
            _requestCounter.Add(1, 
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("status", "received"));

            _logger.LogInformation("MCP request received: {Method}", method);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void RecordError(string method, string errorType)
        {
            _errorCounter.Add(1,
                new KeyValuePair<string, object>("method", method),
                new KeyValuePair<string, object>("error_type", errorType));

            _logger.LogWarning("MCP error occurred: {Method} - {ErrorType}", method, errorType);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void RecordSecurityEvent(string eventType, string details)
        {
            _securityEventCounter.Add(1,
                new KeyValuePair<string, object>("event_type", eventType));

            _logger.LogWarning("Security event: {EventType} - {Details}", eventType, details);
        }
    }
}
