using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Streaming;

namespace EnhancedSample
{
    /// <summary>
    /// Enhanced WebSocket client with advanced features like connection pooling, retry logic, and metrics
    /// </summary>
    public class EnhancedStreamingClient : StreamingClient
    {
        private readonly ClientMetrics _metrics = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private readonly Timer _metricsTimer;
        private bool _isConnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedStreamingClient"/> class
        /// </summary>
        /// <param name="serverUri">Server URI</param>
        public EnhancedStreamingClient(string serverUri) : base(serverUri)
        {
            _metricsTimer = new Timer(LogMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Gets the current client metrics
        /// </summary>
        public ClientMetrics Metrics => _metrics;

        /// <summary>
        /// Connects to the server with retry logic
        /// </summary>
        public override async Task ConnectAsync()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (_isConnected) return;

                const int maxRetries = 3;
                var delay = TimeSpan.FromSeconds(1);

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        await base.ConnectAsync();
                        _isConnected = true;
                        _metrics.ConnectionAttempts++;
                        _metrics.LastConnectedAt = DateTime.UtcNow;
                        Console.WriteLine($"✅ Connected on attempt {attempt}");
                        return;
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _metrics.ConnectionFailures++;
                        Console.WriteLine($"⚠️ Connection attempt {attempt} failed: {ex.Message}");
                        Console.WriteLine($"   Retrying in {delay.TotalSeconds} seconds...");
                        await Task.Delay(delay);
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 1.5); // Exponential backoff
                    }
                }

                throw new InvalidOperationException($"Failed to connect after {maxRetries} attempts");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Calls a method with enhanced error handling and metrics
        /// </summary>
        public override async Task<TResult> CallMethodAsync<TResult>(string method, object parameters = null)
        {
            var stopwatch = Stopwatch.StartNew();
            _metrics.TotalRequests++;
            _metrics.ActiveRequests++;

            try
            {
                var result = await base.CallMethodAsync<TResult>(method, parameters);
                stopwatch.Stop();
                
                _metrics.SuccessfulRequests++;
                _metrics.TotalResponseTime += stopwatch.Elapsed;
                _metrics.LastRequestAt = DateTime.UtcNow;

                // Track method-specific metrics
                if (!_metrics.MethodMetrics.ContainsKey(method))
                {
                    _metrics.MethodMetrics[method] = new MethodMetrics { MethodName = method };
                }
                
                var methodMetrics = _metrics.MethodMetrics[method];
                methodMetrics.CallCount++;
                methodMetrics.TotalResponseTime += stopwatch.Elapsed;
                methodMetrics.LastCallAt = DateTime.UtcNow;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metrics.FailedRequests++;
                _metrics.LastErrorAt = DateTime.UtcNow;
                _metrics.LastError = ex.Message;

                // Track method-specific errors
                if (_metrics.MethodMetrics.ContainsKey(method))
                {
                    _metrics.MethodMetrics[method].ErrorCount++;
                }

                throw;
            }
            finally
            {
                _metrics.ActiveRequests--;
            }
        }

        /// <summary>
        /// Gets a summary of client performance
        /// </summary>
        public ClientPerformanceSummary GetPerformanceSummary()
        {
            var totalTime = _metrics.TotalResponseTime;
            var successfulRequests = _metrics.SuccessfulRequests;

            return new ClientPerformanceSummary
            {
                TotalRequests = _metrics.TotalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = _metrics.FailedRequests,
                ActiveRequests = _metrics.ActiveRequests,
                AverageResponseTime = successfulRequests > 0 ? totalTime.TotalMilliseconds / successfulRequests : 0,
                ErrorRate = _metrics.TotalRequests > 0 ? (double)_metrics.FailedRequests / _metrics.TotalRequests : 0,
                ConnectionAttempts = _metrics.ConnectionAttempts,
                ConnectionFailures = _metrics.ConnectionFailures,
                LastConnectedAt = _metrics.LastConnectedAt,
                LastRequestAt = _metrics.LastRequestAt,
                LastErrorAt = _metrics.LastErrorAt,
                LastError = _metrics.LastError,
                MethodMetrics = new Dictionary<string, MethodMetrics>(_metrics.MethodMetrics)
            };
        }

        private void LogMetrics(object state)
        {
            if (_metrics.TotalRequests == 0) return;

            var summary = GetPerformanceSummary();
            Console.WriteLine($"\n📊 Client Metrics Summary:");
            Console.WriteLine($"   Total Requests: {summary.TotalRequests}");
            Console.WriteLine($"   Success Rate: {(1 - summary.ErrorRate):P2}");
            Console.WriteLine($"   Avg Response Time: {summary.AverageResponseTime:F1}ms");
            Console.WriteLine($"   Active Requests: {summary.ActiveRequests}");
            
            if (summary.MethodMetrics.Count > 0)
            {
                Console.WriteLine($"   Top Methods:");
                foreach (var kvp in summary.MethodMetrics)
                {
                    var method = kvp.Value;
                    var avgTime = method.CallCount > 0 ? method.TotalResponseTime.TotalMilliseconds / method.CallCount : 0;
                    Console.WriteLine($"     {kvp.Key}: {method.CallCount} calls, {avgTime:F1}ms avg");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _metricsTimer?.Dispose();
                _connectionSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Client metrics for tracking performance and usage
    /// </summary>
    public class ClientMetrics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int ActiveRequests { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public int ConnectionAttempts { get; set; }
        public int ConnectionFailures { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public DateTime? LastRequestAt { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public string LastError { get; set; }
        public Dictionary<string, MethodMetrics> MethodMetrics { get; set; } = new();
    }

    /// <summary>
    /// Method-specific metrics
    /// </summary>
    public class MethodMetrics
    {
        public string MethodName { get; set; }
        public int CallCount { get; set; }
        public int ErrorCount { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public DateTime? LastCallAt { get; set; }
    }

    /// <summary>
    /// Performance summary for reporting
    /// </summary>
    public class ClientPerformanceSummary
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int ActiveRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public double ErrorRate { get; set; }
        public int ConnectionAttempts { get; set; }
        public int ConnectionFailures { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public DateTime? LastRequestAt { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public string LastError { get; set; }
        public Dictionary<string, MethodMetrics> MethodMetrics { get; set; }
    }
}
