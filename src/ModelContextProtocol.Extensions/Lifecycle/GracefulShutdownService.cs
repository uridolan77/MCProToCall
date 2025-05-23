using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.WebSocket;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Core.Performance;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Lifecycle
{
    /// <summary>
    /// Service that manages graceful shutdown of all MCP components
    /// </summary>
    public class GracefulShutdownService : IHostedService, IDisposable
    {
        private readonly ILogger<GracefulShutdownService> _logger;
        private readonly ShutdownOptions _options;
        private readonly List<IDisposable> _disposables;
        private readonly List<IAsyncDisposable> _asyncDisposables;
        private readonly WebSocketConnectionManager _webSocketManager;
        private readonly ConfigurationHotReloadService _configService;
        private readonly McpConnectionPool _connectionPool;
        private CancellationTokenSource _shutdownCts;
        private bool _disposed;

        public GracefulShutdownService(
            ILogger<GracefulShutdownService> logger,
            IOptions<ShutdownOptions> options,
            WebSocketConnectionManager webSocketManager = null,
            ConfigurationHotReloadService configService = null,
            McpConnectionPool connectionPool = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _webSocketManager = webSocketManager;
            _configService = configService;
            _connectionPool = connectionPool;
            _disposables = new List<IDisposable>();
            _asyncDisposables = new List<IAsyncDisposable>();
            _shutdownCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Start the graceful shutdown service
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Graceful shutdown service started");

            // Register for application shutdown
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the graceful shutdown service and initiate graceful shutdown
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initiating graceful shutdown...");

            if (!_options.EnableGracefulShutdown)
            {
                _logger.LogInformation("Graceful shutdown is disabled, performing immediate shutdown");
                return;
            }

            try
            {
                // Signal shutdown initiation
                _shutdownCts.Cancel();

                // Create combined cancellation token
                using var timeoutCts = new CancellationTokenSource(_options.GracefulShutdownTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                var shutdownTasks = new List<Task>();

                // Shutdown WebSocket connections gracefully
                if (_webSocketManager != null)
                {
                    shutdownTasks.Add(ShutdownWebSocketManager(combinedCts.Token));
                }

                // Shutdown configuration hot-reload service
                if (_configService != null)
                {
                    shutdownTasks.Add(ShutdownConfigurationService(combinedCts.Token));
                }

                // Shutdown connection pool
                if (_connectionPool != null)
                {
                    shutdownTasks.Add(ShutdownConnectionPool(combinedCts.Token));
                }

                // Wait for all shutdown tasks to complete
                try
                {
                    await Task.WhenAll(shutdownTasks);
                    _logger.LogInformation("Graceful shutdown completed successfully");
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("Graceful shutdown timed out after {Timeout}, forcing shutdown",
                        _options.GracefulShutdownTimeout);
                    await ForceShutdown();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during graceful shutdown, forcing shutdown");
                    await ForceShutdown();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during shutdown process");
            }
        }

        /// <summary>
        /// Register a disposable resource for cleanup during shutdown
        /// </summary>
        public void RegisterDisposable(IDisposable disposable)
        {
            if (disposable != null)
            {
                _disposables.Add(disposable);
            }
        }

        /// <summary>
        /// Register an async disposable resource for cleanup during shutdown
        /// </summary>
        public void RegisterAsyncDisposable(IAsyncDisposable asyncDisposable)
        {
            if (asyncDisposable != null)
            {
                _asyncDisposables.Add(asyncDisposable);
            }
        }

        /// <summary>
        /// Shutdown WebSocket connection manager gracefully
        /// </summary>
        private async Task ShutdownWebSocketManager(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Shutting down WebSocket connection manager...");

                // Close all connections gracefully
                await _webSocketManager.CloseAllConnectionsAsync();

                _logger.LogInformation("WebSocket connection manager shutdown completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("WebSocket manager shutdown was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down WebSocket connection manager");
            }
        }

        /// <summary>
        /// Shutdown configuration hot-reload service gracefully
        /// </summary>
        private async Task ShutdownConfigurationService(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Shutting down configuration hot-reload service...");

                // Stop the configuration service if it implements IHostedService
                if (_configService is IHostedService hostedService)
                {
                    await hostedService.StopAsync(cancellationToken);
                }

                _logger.LogInformation("Configuration service shutdown completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Configuration service shutdown was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down configuration service");
            }
        }

        /// <summary>
        /// Shutdown connection pool gracefully
        /// </summary>
        private async Task ShutdownConnectionPool(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Shutting down connection pool...");

                // Close all pooled connections
                await _connectionPool.CloseAllConnectionsAsync();

                _logger.LogInformation("Connection pool shutdown completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Connection pool shutdown was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down connection pool");
            }
        }

        /// <summary>
        /// Force shutdown all resources when graceful shutdown fails
        /// </summary>
        private async Task ForceShutdown()
        {
            _logger.LogWarning("Performing force shutdown...");

            using var forceTimeoutCts = new CancellationTokenSource(_options.ForceShutdownTimeout);

            try
            {
                // Dispose async disposables first
                var asyncTasks = new List<Task>();
                foreach (var asyncDisposable in _asyncDisposables)
                {
                    asyncTasks.Add(DisposeAsyncSafely(asyncDisposable, forceTimeoutCts.Token));
                }

                await Task.WhenAll(asyncTasks);

                // Dispose synchronous disposables
                foreach (var disposable in _disposables)
                {
                    DisposeSafely(disposable);
                }

                _logger.LogInformation("Force shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during force shutdown");
            }
        }

        /// <summary>
        /// Safely dispose an async disposable with timeout
        /// </summary>
        private async Task DisposeAsyncSafely(IAsyncDisposable asyncDisposable, CancellationToken cancellationToken)
        {
            try
            {
                await asyncDisposable.DisposeAsync().AsTask().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Async dispose operation timed out for {Type}", asyncDisposable.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing {Type}", asyncDisposable.GetType().Name);
            }
        }

        /// <summary>
        /// Safely dispose a disposable
        /// </summary>
        private void DisposeSafely(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing {Type}", disposable.GetType().Name);
            }
        }

        /// <summary>
        /// Handle process exit event
        /// </summary>
        private void OnProcessExit(object sender, EventArgs e)
        {
            _logger.LogInformation("Process exit detected, initiating shutdown...");
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Handle Ctrl+C cancellation
        /// </summary>
        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _logger.LogInformation("Cancellation requested (Ctrl+C), initiating graceful shutdown...");
            e.Cancel = true; // Prevent immediate termination
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get shutdown statistics
        /// </summary>
        public ShutdownStatistics GetShutdownStatistics()
        {
            return new ShutdownStatistics
            {
                IsShutdownInitiated = _shutdownCts.Token.IsCancellationRequested,
                RegisteredDisposables = _disposables.Count,
                RegisteredAsyncDisposables = _asyncDisposables.Count,
                GracefulShutdownTimeout = _options.GracefulShutdownTimeout,
                ForceShutdownTimeout = _options.ForceShutdownTimeout
            };
        }

        /// <summary>
        /// Dispose the graceful shutdown service
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Unregister event handlers
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                Console.CancelKeyPress -= OnCancelKeyPress;

                // Dispose cancellation token source
                _shutdownCts?.Dispose();

                // Clear collections
                _disposables.Clear();
                _asyncDisposables.Clear();

                _disposed = true;
                _logger.LogDebug("GracefulShutdownService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing GracefulShutdownService");
            }
        }
    }

    /// <summary>
    /// Statistics about the shutdown process
    /// </summary>
    public class ShutdownStatistics
    {
        public bool IsShutdownInitiated { get; set; }
        public int RegisteredDisposables { get; set; }
        public int RegisteredAsyncDisposables { get; set; }
        public TimeSpan GracefulShutdownTimeout { get; set; }
        public TimeSpan ForceShutdownTimeout { get; set; }
    }
}
