using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Configuration options for connection pooling
    /// </summary>
    public class ConnectionPoolOptions
    {
        /// <summary>
        /// Maximum number of connections per server
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = 50;

        /// <summary>
        /// Connection lifetime in seconds
        /// </summary>
        public int ConnectionLifetimeSeconds { get; set; } = 600;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Pool cleanup interval in seconds
        /// </summary>
        public int PoolCleanupIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Maximum number of idle connections to keep
        /// </summary>
        public int MaxIdleConnections { get; set; } = 20;

        /// <summary>
        /// Whether to enable HTTP/2
        /// </summary>
        public bool EnableHttp2 { get; set; } = true;

        /// <summary>
        /// Whether to enable connection multiplexing
        /// </summary>
        public bool EnableMultiplexing { get; set; } = true;
    }

    /// <summary>
    /// Enhanced connection pool for HTTP clients with lifecycle management
    /// </summary>
    public class McpConnectionPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, PooledHttpClient> _clients;
        private readonly ConnectionPoolOptions _options;
        private readonly ILogger<McpConnectionPool> _logger;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _cleanupSemaphore;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpConnectionPool"/> class
        /// </summary>
        public McpConnectionPool(
            IOptions<ConnectionPoolOptions> options,
            ILogger<McpConnectionPool> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clients = new ConcurrentDictionary<string, PooledHttpClient>();
            _cleanupSemaphore = new SemaphoreSlim(1, 1);

            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredClients, null,
                TimeSpan.FromSeconds(_options.PoolCleanupIntervalSeconds),
                TimeSpan.FromSeconds(_options.PoolCleanupIntervalSeconds));

            _logger.LogInformation("Initialized connection pool with {MaxConnections} max connections per server",
                _options.MaxConnectionsPerServer);
        }

        /// <summary>
        /// Gets a client for the specified endpoint
        /// </summary>
        /// <param name="endpoint">Endpoint URL</param>
        /// <returns>HTTP client</returns>
        public HttpClient GetClient(string endpoint)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(McpConnectionPool));

            var pooledClient = _clients.GetOrAdd(endpoint, key => CreatePooledHttpClient(key));

            // Update last access time
            pooledClient.UpdateLastAccess();

            return pooledClient.HttpClient;
        }

        /// <summary>
        /// Gets connection pool statistics
        /// </summary>
        public ConnectionPoolStats GetStats()
        {
            var totalConnections = _clients.Count;
            var activeConnections = 0;
            var idleConnections = 0;

            foreach (var client in _clients.Values)
            {
                if (client.IsActive)
                    activeConnections++;
                else
                    idleConnections++;
            }

            return new ConnectionPoolStats
            {
                TotalConnections = totalConnections,
                ActiveConnections = activeConnections,
                IdleConnections = idleConnections,
                MaxConnectionsPerServer = _options.MaxConnectionsPerServer
            };
        }

        /// <summary>
        /// Closes all connections in the pool
        /// </summary>
        public async Task CloseAllConnectionsAsync()
        {
            _logger.LogInformation("Closing all connections in the pool...");

            var clients = _clients.Values.ToList();
            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing pooled client");
                }
            }

            _clients.Clear();
            _logger.LogInformation("Closed {ClientCount} pooled connections", clients.Count);

            await Task.CompletedTask; // Make it async for consistency
        }

        private PooledHttpClient CreatePooledHttpClient(string endpoint)
        {
            _logger.LogDebug("Creating new HTTP client for endpoint: {Endpoint}", endpoint);

            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
                PooledConnectionLifetime = TimeSpan.FromSeconds(_options.ConnectionLifetimeSeconds),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(60),
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                UseProxy = true
            };

            // Enable HTTP/2 if requested
            if (_options.EnableHttp2)
            {
                handler.SslOptions.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
            }

            var client = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = new Uri(endpoint),
                Timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
            };

            // Configure default headers for connection reuse
            client.DefaultRequestHeaders.ConnectionClose = false;
            if (_options.EnableMultiplexing)
            {
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            }

            return new PooledHttpClient(client, _logger);
        }

        private async void CleanupExpiredClients(object state)
        {
            if (_disposed)
                return;

            await _cleanupSemaphore.WaitAsync();
            try
            {
                var expiredKeys = new List<string>();
                var connectionLifetime = TimeSpan.FromSeconds(_options.ConnectionLifetimeSeconds);

                foreach (var kvp in _clients)
                {
                    if (kvp.Value.IsExpired(connectionLifetime) ||
                        (!kvp.Value.IsActive && _clients.Count > _options.MaxIdleConnections))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    if (_clients.TryRemove(key, out var expiredClient))
                    {
                        _logger.LogDebug("Removing expired client for endpoint: {Endpoint}", key);
                        expiredClient.Dispose();
                    }
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired connections", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection pool cleanup");
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _logger.LogInformation("Disposing connection pool");

            _cleanupTimer?.Dispose();
            _cleanupSemaphore?.Dispose();

            foreach (var client in _clients.Values)
            {
                client.Dispose();
            }

            _clients.Clear();
        }
    }

    /// <summary>
    /// Wrapper for HttpClient with pooling metadata
    /// </summary>
    internal class PooledHttpClient : IDisposable
    {
        private readonly ILogger _logger;
        private DateTime _lastAccessTime;
        private long _requestCount;

        public HttpClient HttpClient { get; }
        public DateTime CreatedTime { get; }
        public bool IsActive => (DateTime.UtcNow - _lastAccessTime).TotalMinutes < 5;

        public PooledHttpClient(HttpClient httpClient, ILogger logger)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CreatedTime = DateTime.UtcNow;
            _lastAccessTime = DateTime.UtcNow;
        }

        public void UpdateLastAccess()
        {
            _lastAccessTime = DateTime.UtcNow;
            Interlocked.Increment(ref _requestCount);
        }

        public bool IsExpired(TimeSpan maxLifetime)
        {
            return (DateTime.UtcNow - CreatedTime) > maxLifetime;
        }

        public void Dispose()
        {
            try
            {
                _logger.LogDebug("Disposing pooled HTTP client (Requests: {RequestCount}, Lifetime: {Lifetime})",
                    _requestCount, DateTime.UtcNow - CreatedTime);

                HttpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing pooled HTTP client");
            }
        }
    }

    /// <summary>
    /// Connection pool statistics
    /// </summary>
    public class ConnectionPoolStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int MaxConnectionsPerServer { get; set; }
    }
}
