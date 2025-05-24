using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ModelContextProtocol.Extensions.Diagnostics
{
    /// <summary>
    /// Collects and maintains diagnostic metrics
    /// </summary>
    public class DiagnosticMetricsCollector
    {
        private readonly ConcurrentQueue<RequestMetric> _requestMetrics = new();
        private readonly ConcurrentDictionary<string, object> _customMetrics = new();
        private readonly object _lockObject = new object();
        
        private long _totalRequests;
        private long _totalErrors;
        private long _totalConnections;
        private int _activeConnections;
        private readonly DateTime _startTime = DateTime.UtcNow;
        
        // Sliding window for recent metrics (last 5 minutes)
        private readonly TimeSpan _metricsWindow = TimeSpan.FromMinutes(5);
        
        // Connection tracking
        private readonly ConcurrentQueue<ConnectionMetric> _connectionMetrics = new();

        /// <summary>
        /// Records a request with its duration and error status
        /// </summary>
        public void RecordRequest(TimeSpan duration, bool isError = false)
        {
            var metric = new RequestMetric
            {
                Timestamp = DateTime.UtcNow,
                Duration = duration,
                IsError = isError
            };

            _requestMetrics.Enqueue(metric);
            Interlocked.Increment(ref _totalRequests);
            
            if (isError)
            {
                Interlocked.Increment(ref _totalErrors);
            }

            // Clean old metrics
            CleanOldMetrics();
        }

        /// <summary>
        /// Records a connection event
        /// </summary>
        public void RecordConnection(bool isConnected)
        {
            var metric = new ConnectionMetric
            {
                Timestamp = DateTime.UtcNow,
                IsConnected = isConnected
            };

            _connectionMetrics.Enqueue(metric);
            Interlocked.Increment(ref _totalConnections);

            if (isConnected)
            {
                Interlocked.Increment(ref _activeConnections);
            }
            else
            {
                Interlocked.Decrement(ref _activeConnections);
            }

            // Clean old connection metrics
            CleanOldConnectionMetrics();
        }

        /// <summary>
        /// Adds a custom metric
        /// </summary>
        public void AddCustomMetric(string name, object value)
        {
            _customMetrics.AddOrUpdate(name, value, (key, oldValue) => value);
        }

        /// <summary>
        /// Gets the current request rate (requests per second)
        /// </summary>
        public double GetRequestRate()
        {
            var recentMetrics = GetRecentRequestMetrics();
            if (!recentMetrics.Any())
                return 0;

            var timeSpan = DateTime.UtcNow - recentMetrics.Min(m => m.Timestamp);
            return timeSpan.TotalSeconds > 0 ? recentMetrics.Count / timeSpan.TotalSeconds : 0;
        }

        /// <summary>
        /// Gets the average latency
        /// </summary>
        public TimeSpan GetAverageLatency()
        {
            var recentMetrics = GetRecentRequestMetrics();
            if (!recentMetrics.Any())
                return TimeSpan.Zero;

            var totalTicks = recentMetrics.Sum(m => m.Duration.Ticks);
            return new TimeSpan(totalTicks / recentMetrics.Count);
        }

        /// <summary>
        /// Gets the percentile latency
        /// </summary>
        public TimeSpan GetPercentileLatency(int percentile)
        {
            var recentMetrics = GetRecentRequestMetrics();
            if (!recentMetrics.Any())
                return TimeSpan.Zero;

            var sortedDurations = recentMetrics.Select(m => m.Duration).OrderBy(d => d).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sortedDurations.Count) - 1;
            index = Math.Max(0, Math.Min(index, sortedDurations.Count - 1));
            
            return sortedDurations[index];
        }

        /// <summary>
        /// Gets the error rate (percentage)
        /// </summary>
        public double GetErrorRate()
        {
            var recentMetrics = GetRecentRequestMetrics();
            if (!recentMetrics.Any())
                return 0;

            var errorCount = recentMetrics.Count(m => m.IsError);
            return (double)errorCount / recentMetrics.Count * 100;
        }

        /// <summary>
        /// Gets the total number of requests
        /// </summary>
        public long GetTotalRequests()
        {
            return _totalRequests;
        }

        /// <summary>
        /// Gets the total number of errors
        /// </summary>
        public long GetTotalErrors()
        {
            return _totalErrors;
        }

        /// <summary>
        /// Gets the number of active connections
        /// </summary>
        public int GetActiveConnections()
        {
            return _activeConnections;
        }

        /// <summary>
        /// Gets the total number of connections
        /// </summary>
        public long GetTotalConnections()
        {
            return _totalConnections;
        }

        /// <summary>
        /// Gets the connection rate (connections per second)
        /// </summary>
        public double GetConnectionRate()
        {
            var recentMetrics = GetRecentConnectionMetrics();
            if (!recentMetrics.Any())
                return 0;

            var timeSpan = DateTime.UtcNow - recentMetrics.Min(m => m.Timestamp);
            return timeSpan.TotalSeconds > 0 ? recentMetrics.Count / timeSpan.TotalSeconds : 0;
        }

        /// <summary>
        /// Gets the average connection duration
        /// </summary>
        public TimeSpan GetAverageConnectionDuration()
        {
            var recentMetrics = GetRecentConnectionMetrics();
            var connections = recentMetrics.Where(m => m.IsConnected).ToList();
            var disconnections = recentMetrics.Where(m => !m.IsConnected).ToList();

            if (!connections.Any() || !disconnections.Any())
                return TimeSpan.Zero;

            var durations = new List<TimeSpan>();
            
            foreach (var connection in connections)
            {
                var disconnection = disconnections.FirstOrDefault(d => d.Timestamp > connection.Timestamp);
                if (disconnection != null)
                {
                    durations.Add(disconnection.Timestamp - connection.Timestamp);
                }
            }

            if (!durations.Any())
                return TimeSpan.Zero;

            var totalTicks = durations.Sum(d => d.Ticks);
            return new TimeSpan(totalTicks / durations.Count);
        }

        /// <summary>
        /// Gets all custom metrics
        /// </summary>
        public Dictionary<string, object> GetMetrics()
        {
            return new Dictionary<string, object>(_customMetrics);
        }

        /// <summary>
        /// Gets comprehensive metrics summary
        /// </summary>
        public MetricsSummary GetSummary()
        {
            return new MetricsSummary
            {
                TotalRequests = _totalRequests,
                TotalErrors = _totalErrors,
                TotalConnections = _totalConnections,
                ActiveConnections = _activeConnections,
                RequestsPerSecond = GetRequestRate(),
                ErrorRate = GetErrorRate(),
                AverageLatency = GetAverageLatency(),
                P95Latency = GetPercentileLatency(95),
                P99Latency = GetPercentileLatency(99),
                ConnectionsPerSecond = GetConnectionRate(),
                AverageConnectionDuration = GetAverageConnectionDuration(),
                Uptime = DateTime.UtcNow - _startTime,
                Timestamp = DateTime.UtcNow,
                CustomMetrics = GetMetrics()
            };
        }

        private List<RequestMetric> GetRecentRequestMetrics()
        {
            var cutoff = DateTime.UtcNow - _metricsWindow;
            return _requestMetrics.Where(m => m.Timestamp >= cutoff).ToList();
        }

        private List<ConnectionMetric> GetRecentConnectionMetrics()
        {
            var cutoff = DateTime.UtcNow - _metricsWindow;
            return _connectionMetrics.Where(m => m.Timestamp >= cutoff).ToList();
        }

        private void CleanOldMetrics()
        {
            var cutoff = DateTime.UtcNow - _metricsWindow;
            
            while (_requestMetrics.TryPeek(out var metric) && metric.Timestamp < cutoff)
            {
                _requestMetrics.TryDequeue(out _);
            }
        }

        private void CleanOldConnectionMetrics()
        {
            var cutoff = DateTime.UtcNow - _metricsWindow;
            
            while (_connectionMetrics.TryPeek(out var metric) && metric.Timestamp < cutoff)
            {
                _connectionMetrics.TryDequeue(out _);
            }
        }
    }

    /// <summary>
    /// Request metric data
    /// </summary>
    internal class RequestMetric
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsError { get; set; }
    }

    /// <summary>
    /// Connection metric data
    /// </summary>
    internal class ConnectionMetric
    {
        public DateTime Timestamp { get; set; }
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// Comprehensive metrics summary
    /// </summary>
    public class MetricsSummary
    {
        public long TotalRequests { get; set; }
        public long TotalErrors { get; set; }
        public long TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public double RequestsPerSecond { get; set; }
        public double ErrorRate { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public double ConnectionsPerSecond { get; set; }
        public TimeSpan AverageConnectionDuration { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            return $"Metrics Summary: Requests={TotalRequests} ({RequestsPerSecond:F1}/s), " +
                   $"Errors={TotalErrors} ({ErrorRate:F1}%), " +
                   $"Connections={ActiveConnections}/{TotalConnections}, " +
                   $"Latency=Avg:{AverageLatency.TotalMilliseconds:F1}ms P95:{P95Latency.TotalMilliseconds:F1}ms, " +
                   $"Uptime={Uptime:dd\\.hh\\:mm\\:ss}";
        }
    }
}
