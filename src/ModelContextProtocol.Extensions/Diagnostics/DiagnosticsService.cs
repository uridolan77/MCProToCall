using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Extensions.Resilience;

namespace ModelContextProtocol.Extensions.Diagnostics
{
    /// <summary>
    /// Service for generating real-time diagnostics and profiling information
    /// </summary>
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ILogger<DiagnosticsService> _logger;
        private readonly IHostEnvironment _environment;
        private readonly DiagnosticMetricsCollector _metricsCollector;
        private readonly DateTime _startTime;

        public DiagnosticsService(
            ILogger<DiagnosticsService> logger,
            IHostEnvironment environment,
            DiagnosticMetricsCollector metricsCollector = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _metricsCollector = metricsCollector ?? new DiagnosticMetricsCollector();
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Generates a comprehensive diagnostic report
        /// </summary>
        public async Task<DiagnosticReport> GenerateReportAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Generating diagnostic report");

                var report = new DiagnosticReport
                {
                    Timestamp = DateTime.UtcNow,
                    Environment = _environment.EnvironmentName,
                    ApplicationName = _environment.ApplicationName,
                    MachineName = Environment.MachineName,
                    ProcessId = Environment.ProcessId,
                    Uptime = DateTime.UtcNow - _startTime
                };

                // Collect system information
                report.SystemInfo = await GetSystemInfoAsync(cancellationToken);

                // Collect performance metrics
                report.Performance = await GetPerformanceMetricsAsync(cancellationToken);

                // Collect memory information
                report.MemoryInfo = GetMemoryInfo();

                // Collect thread pool information
                report.ThreadPoolInfo = GetThreadPoolInfo();

                // Collect GC information
                report.GarbageCollectionInfo = GetGarbageCollectionInfo();

                // Collect connection information (if available)
                report.Connections = await GetConnectionStatsAsync(cancellationToken);

                // Collect custom metrics
                report.CustomMetrics = _metricsCollector.GetMetrics();

                stopwatch.Stop();
                report.GenerationDuration = stopwatch.Elapsed;

                _logger.LogDebug("Diagnostic report generated in {Duration}ms", stopwatch.ElapsedMilliseconds);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate diagnostic report");
                throw;
            }
        }

        /// <summary>
        /// Gets real-time performance metrics
        /// </summary>
        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new PerformanceMetrics
            {
                RequestsPerSecond = _metricsCollector.GetRequestRate(),
                AverageLatency = _metricsCollector.GetAverageLatency(),
                P50Latency = _metricsCollector.GetPercentileLatency(50),
                P95Latency = _metricsCollector.GetPercentileLatency(95),
                P99Latency = _metricsCollector.GetPercentileLatency(99),
                ErrorRate = _metricsCollector.GetErrorRate(),
                TotalRequests = _metricsCollector.GetTotalRequests(),
                TotalErrors = _metricsCollector.GetTotalErrors(),
                ActiveConnections = _metricsCollector.GetActiveConnections(),
                CpuUsage = GetCpuUsage(),
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Gets system information
        /// </summary>
        public async Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(new SystemInfo
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                RuntimeVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet,
                SystemPageSize = Environment.SystemPageSize,
                TickCount = Environment.TickCount64,
                UserDomainName = Environment.UserDomainName,
                UserName = Environment.UserName,
                HasShutdownStarted = Environment.HasShutdownStarted,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Records a request for metrics collection
        /// </summary>
        public void RecordRequest(TimeSpan duration, bool isError = false)
        {
            _metricsCollector.RecordRequest(duration, isError);
        }

        /// <summary>
        /// Records a connection event
        /// </summary>
        public void RecordConnection(bool isConnected)
        {
            _metricsCollector.RecordConnection(isConnected);
        }

        /// <summary>
        /// Adds a custom metric
        /// </summary>
        public void AddCustomMetric(string name, object value)
        {
            _metricsCollector.AddCustomMetric(name, value);
        }

        private MemoryInfo GetMemoryInfo()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            
            return new MemoryInfo
            {
                TotalMemory = GC.GetTotalMemory(false),
                WorkingSet = Environment.WorkingSet,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                AllocatedBytes = gcInfo.TotalAvailableMemoryBytes,
                HeapSize = gcInfo.HeapSizeBytes,
                FragmentedBytes = gcInfo.FragmentedBytes,
                IsServerGC = GCSettings.IsServerGC,
                LatencyMode = GCSettings.LatencyMode.ToString(),
                Timestamp = DateTime.UtcNow
            };
        }

        private ThreadPoolInfo GetThreadPoolInfo()
        {
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
            ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
            ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);

            return new ThreadPoolInfo
            {
                MaxWorkerThreads = maxWorkerThreads,
                MaxCompletionPortThreads = maxCompletionPortThreads,
                AvailableWorkerThreads = availableWorkerThreads,
                AvailableCompletionPortThreads = availableCompletionPortThreads,
                MinWorkerThreads = minWorkerThreads,
                MinCompletionPortThreads = minCompletionPortThreads,
                ActiveWorkerThreads = maxWorkerThreads - availableWorkerThreads,
                ActiveCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                Timestamp = DateTime.UtcNow
            };
        }

        private GarbageCollectionInfo GetGarbageCollectionInfo()
        {
            return new GarbageCollectionInfo
            {
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalMemory = GC.GetTotalMemory(false),
                IsServerGC = GCSettings.IsServerGC,
                LatencyMode = GCSettings.LatencyMode.ToString(),
                LargeObjectHeapCompactionMode = GCSettings.LargeObjectHeapCompactionMode.ToString(),
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<ConnectionStats> GetConnectionStatsAsync(CancellationToken cancellationToken)
        {
            // This would typically integrate with your connection manager
            return await Task.FromResult(new ConnectionStats
            {
                ActiveConnections = _metricsCollector.GetActiveConnections(),
                TotalConnections = _metricsCollector.GetTotalConnections(),
                ConnectionsPerSecond = _metricsCollector.GetConnectionRate(),
                AverageConnectionDuration = _metricsCollector.GetAverageConnectionDuration(),
                Timestamp = DateTime.UtcNow
            });
        }

        private double GetCpuUsage()
        {
            // This is a simplified CPU usage calculation
            // In a real implementation, you might use PerformanceCounter or similar
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100;
        }
    }

    /// <summary>
    /// Interface for diagnostics service
    /// </summary>
    public interface IDiagnosticsService
    {
        Task<DiagnosticReport> GenerateReportAsync(CancellationToken cancellationToken = default);
        Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);
        Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default);
        void RecordRequest(TimeSpan duration, bool isError = false);
        void RecordConnection(bool isConnected);
        void AddCustomMetric(string name, object value);
    }

    /// <summary>
    /// Comprehensive diagnostic report
    /// </summary>
    public class DiagnosticReport
    {
        public DateTime Timestamp { get; set; }
        public string Environment { get; set; }
        public string ApplicationName { get; set; }
        public string MachineName { get; set; }
        public int ProcessId { get; set; }
        public TimeSpan Uptime { get; set; }
        public TimeSpan GenerationDuration { get; set; }
        public SystemInfo SystemInfo { get; set; }
        public PerformanceMetrics Performance { get; set; }
        public MemoryInfo MemoryInfo { get; set; }
        public ThreadPoolInfo ThreadPoolInfo { get; set; }
        public GarbageCollectionInfo GarbageCollectionInfo { get; set; }
        public ConnectionStats Connections { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        public double RequestsPerSecond { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P50Latency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public double ErrorRate { get; set; }
        public long TotalRequests { get; set; }
        public long TotalErrors { get; set; }
        public int ActiveConnections { get; set; }
        public double CpuUsage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// System information
    /// </summary>
    public class SystemInfo
    {
        public string OperatingSystem { get; set; }
        public int ProcessorCount { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public bool Is64BitProcess { get; set; }
        public string RuntimeVersion { get; set; }
        public long WorkingSet { get; set; }
        public int SystemPageSize { get; set; }
        public long TickCount { get; set; }
        public string UserDomainName { get; set; }
        public string UserName { get; set; }
        public bool HasShutdownStarted { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Memory information
    /// </summary>
    public class MemoryInfo
    {
        public long TotalMemory { get; set; }
        public long WorkingSet { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long AllocatedBytes { get; set; }
        public long HeapSize { get; set; }
        public long FragmentedBytes { get; set; }
        public bool IsServerGC { get; set; }
        public string LatencyMode { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Thread pool information
    /// </summary>
    public class ThreadPoolInfo
    {
        public int MaxWorkerThreads { get; set; }
        public int MaxCompletionPortThreads { get; set; }
        public int AvailableWorkerThreads { get; set; }
        public int AvailableCompletionPortThreads { get; set; }
        public int MinWorkerThreads { get; set; }
        public int MinCompletionPortThreads { get; set; }
        public int ActiveWorkerThreads { get; set; }
        public int ActiveCompletionPortThreads { get; set; }
        public int ThreadCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Garbage collection information
    /// </summary>
    public class GarbageCollectionInfo
    {
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long TotalMemory { get; set; }
        public bool IsServerGC { get; set; }
        public string LatencyMode { get; set; }
        public string LargeObjectHeapCompactionMode { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Connection statistics
    /// </summary>
    public class ConnectionStats
    {
        public int ActiveConnections { get; set; }
        public long TotalConnections { get; set; }
        public double ConnectionsPerSecond { get; set; }
        public TimeSpan AverageConnectionDuration { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
