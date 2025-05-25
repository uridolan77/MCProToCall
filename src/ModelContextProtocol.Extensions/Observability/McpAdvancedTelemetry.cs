using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Advanced telemetry service with business metrics and alerting
    /// </summary>
    public class McpAdvancedTelemetry : IDisposable
    {
        private readonly IAlertingService _alerting;
        private readonly ILogger<McpAdvancedTelemetry> _logger;
        private readonly McpAdvancedTelemetryOptions _options;
        private readonly Meter _businessMeter;
        private readonly Dictionary<string, Counter<long>> _businessCounters = new();
        private readonly Dictionary<string, Histogram<double>> _businessHistograms = new();
        private readonly Dictionary<string, ObservableGauge<double>> _businessGauges = new();

        public ActivitySource ActivitySource { get; }

        public McpAdvancedTelemetry(
            IAlertingService alerting,
            ILogger<McpAdvancedTelemetry> logger,
            IOptions<McpAdvancedTelemetryOptions> options,
            ActivitySource activitySource,
            Meter meter)
        {
            _alerting = alerting ?? throw new ArgumentNullException(nameof(alerting));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            ActivitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

            _businessMeter = new Meter(_options.BusinessMeterName, _options.Version);
        }

        /// <summary>
        /// Records a business metric with automatic alerting
        /// </summary>
        public async Task RecordBusinessMetricAsync(
            string metricName,
            double value,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(metricName))
                throw new ArgumentException("Metric name cannot be null or empty", nameof(metricName));

            try
            {
                // Record the metric
                var tagPairs = ConvertToTagPairs(tags);

                if (!_businessHistograms.TryGetValue(metricName, out var histogram))
                {
                    histogram = _businessMeter.CreateHistogram<double>(
                        metricName,
                        description: $"Business metric: {metricName}");
                    _businessHistograms[metricName] = histogram;
                }

                histogram.Record(value, tagPairs);

                // Check if alert should be triggered
                if (_options.EnableAutoAlerting && await _alerting.ShouldTriggerAlertAsync(metricName, value, cancellationToken))
                {
                    await _alerting.TriggerAlertAsync("BusinessMetricThreshold",
                        new { MetricName = metricName, Value = value, Tags = tags },
                        DetermineAlertSeverity(metricName, value),
                        cancellationToken);
                }

                _logger.LogDebug("Recorded business metric: {MetricName} = {Value}", metricName, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording business metric: {MetricName}", metricName);
                throw;
            }
        }

        /// <summary>
        /// Records a business counter metric
        /// </summary>
        public async Task RecordBusinessCounterAsync(
            string metricName,
            long value = 1,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(metricName))
                throw new ArgumentException("Metric name cannot be null or empty", nameof(metricName));

            try
            {
                var tagPairs = ConvertToTagPairs(tags);

                if (!_businessCounters.TryGetValue(metricName, out var counter))
                {
                    counter = _businessMeter.CreateCounter<long>(
                        metricName,
                        description: $"Business counter: {metricName}");
                    _businessCounters[metricName] = counter;
                }

                counter.Add(value, tagPairs);

                // Check for alerting on counter values
                if (_options.EnableAutoAlerting && await _alerting.ShouldTriggerAlertAsync(metricName, value, cancellationToken))
                {
                    await _alerting.TriggerAlertAsync("BusinessCounterThreshold",
                        new { MetricName = metricName, Value = value, Tags = tags },
                        AlertSeverity.Warning,
                        cancellationToken);
                }

                _logger.LogDebug("Recorded business counter: {MetricName} += {Value}", metricName, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording business counter: {MetricName}", metricName);
                throw;
            }
        }

        /// <summary>
        /// Records a custom event with structured data
        /// </summary>
        public async Task RecordBusinessEventAsync(
            string eventName,
            object eventData,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));

            try
            {
                using var activity = ActivitySource.StartActivity($"business.event.{eventName}");

                if (activity != null)
                {
                    activity.SetTag("event.name", eventName);
                    activity.SetTag("event.timestamp", DateTimeOffset.UtcNow.ToString("O"));

                    if (tags != null)
                    {
                        foreach (var tag in tags)
                        {
                            activity.SetTag($"event.{tag.Key}", tag.Value?.ToString());
                        }
                    }

                    // Add event data as activity tags (limited to prevent excessive data)
                    if (eventData != null)
                    {
                        var eventDataString = System.Text.Json.JsonSerializer.Serialize(eventData);
                        if (eventDataString.Length <= _options.MaxEventDataLength)
                        {
                            activity.SetTag("event.data", eventDataString);
                        }
                        else
                        {
                            activity.SetTag("event.data.truncated", "true");
                            activity.SetTag("event.data.length", eventDataString.Length.ToString());
                        }
                    }
                }

                // Record as a counter metric
                await RecordBusinessCounterAsync($"events.{eventName}", 1, tags, cancellationToken);

                _logger.LogInformation("Recorded business event: {EventName}", eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording business event: {EventName}", eventName);
                throw;
            }
        }

        /// <summary>
        /// Creates a business operation scope with automatic metrics
        /// </summary>
        public BusinessOperationScope StartBusinessOperation(
            string operationName,
            Dictionary<string, object>? tags = null)
        {
            return new BusinessOperationScope(this, operationName, tags);
        }

        /// <summary>
        /// Records SLA metrics (availability, latency, error rate)
        /// </summary>
        public async Task RecordSlaMetricsAsync(
            string serviceName,
            bool isSuccess,
            TimeSpan duration,
            Dictionary<string, object>? tags = null,
            CancellationToken cancellationToken = default)
        {
            var baseTags = new Dictionary<string, object>
            {
                ["service"] = serviceName,
                ["success"] = isSuccess.ToString().ToLower()
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    baseTags[tag.Key] = tag.Value;
                }
            }

            // Record availability
            await RecordBusinessCounterAsync("sla.requests.total", 1, baseTags, cancellationToken);

            if (isSuccess)
            {
                await RecordBusinessCounterAsync("sla.requests.success", 1, baseTags, cancellationToken);
            }
            else
            {
                await RecordBusinessCounterAsync("sla.requests.error", 1, baseTags, cancellationToken);
            }

            // Record latency
            await RecordBusinessMetricAsync("sla.latency", duration.TotalMilliseconds, baseTags, cancellationToken);
        }

        private KeyValuePair<string, object?>[] ConvertToTagPairs(Dictionary<string, object>? tags)
        {
            if (tags == null || tags.Count == 0)
                return Array.Empty<KeyValuePair<string, object?>>();

            var pairs = new KeyValuePair<string, object?>[tags.Count];
            int index = 0;
            foreach (var tag in tags)
            {
                pairs[index++] = new KeyValuePair<string, object?>(tag.Key, tag.Value);
            }
            return pairs;
        }

        private AlertSeverity DetermineAlertSeverity(string metricName, double value)
        {
            // Simple heuristic - in practice, this would be configurable per metric
            if (metricName.Contains("error") || metricName.Contains("failure"))
            {
                return value > 10 ? AlertSeverity.Critical : AlertSeverity.Warning;
            }

            if (metricName.Contains("latency") || metricName.Contains("duration"))
            {
                return value > 5000 ? AlertSeverity.Error : AlertSeverity.Warning;
            }

            return AlertSeverity.Info;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _businessMeter?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Business operation scope for automatic metrics collection
    /// </summary>
    public class BusinessOperationScope : IDisposable
    {
        private readonly McpAdvancedTelemetry _telemetry;
        private readonly string _operationName;
        private readonly Dictionary<string, object>? _tags;
        private readonly Stopwatch _stopwatch;
        private readonly Activity? _activity;
        private bool _disposed;

        internal BusinessOperationScope(
            McpAdvancedTelemetry telemetry,
            string operationName,
            Dictionary<string, object>? tags)
        {
            _telemetry = telemetry;
            _operationName = operationName;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
            _activity = telemetry.ActivitySource.StartActivity($"business.operation.{operationName}");
        }

        public async Task CompleteAsync(bool success = true, string? errorMessage = null)
        {
            if (_disposed) return;

            _stopwatch.Stop();

            try
            {
                // Record operation metrics
                var operationTags = new Dictionary<string, object>
                {
                    ["operation"] = _operationName,
                    ["success"] = success.ToString().ToLower()
                };

                if (_tags != null)
                {
                    foreach (var tag in _tags)
                    {
                        operationTags[tag.Key] = tag.Value;
                    }
                }

                if (!success && !string.IsNullOrEmpty(errorMessage))
                {
                    operationTags["error"] = errorMessage;
                }

                await _telemetry.RecordBusinessCounterAsync($"operations.{_operationName}.total", 1, operationTags);
                await _telemetry.RecordBusinessMetricAsync($"operations.{_operationName}.duration",
                    _stopwatch.Elapsed.TotalMilliseconds, operationTags);

                if (success)
                {
                    await _telemetry.RecordBusinessCounterAsync($"operations.{_operationName}.success", 1, operationTags);
                }
                else
                {
                    await _telemetry.RecordBusinessCounterAsync($"operations.{_operationName}.error", 1, operationTags);
                }

                _activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error, errorMessage);
            }
            catch (Exception ex)
            {
                // Don't throw from dispose/complete
                System.Diagnostics.Debug.WriteLine($"Error completing business operation scope: {ex}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await CompleteAsync());
                _activity?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Configuration options for advanced telemetry
    /// </summary>
    public class McpAdvancedTelemetryOptions
    {
        public string BusinessMeterName { get; set; } = "mcp.business";
        public string Version { get; set; } = "1.0.0";
        public bool EnableAutoAlerting { get; set; } = true;
        public int MaxEventDataLength { get; set; } = 1024;
        public TimeSpan MetricFlushInterval { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableSlaMetrics { get; set; } = true;
    }
}
