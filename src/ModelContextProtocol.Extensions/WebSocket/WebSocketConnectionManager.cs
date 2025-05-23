using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.WebSocket
{
    /// <summary>
    /// Configuration options for WebSocket connections
    /// </summary>
    public class WebSocketOptions
    {
        /// <summary>
        /// Heartbeat interval in seconds
        /// </summary>
        public int HeartbeatIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Maximum number of missed heartbeats before considering connection dead
        /// </summary>
        public int MaxMissedHeartbeats { get; set; } = 3;

        /// <summary>
        /// Maximum message size in bytes
        /// </summary>
        public int MaxMessageSizeBytes { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Receive buffer size in bytes
        /// </summary>
        public int ReceiveBufferSizeBytes { get; set; } = 4096;

        /// <summary>
        /// Whether to enable automatic reconnection
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// Maximum reconnection attempts
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// Reconnection delay in seconds
        /// </summary>
        public int ReconnectDelaySeconds { get; set; } = 5;
    }

    /// <summary>
    /// Represents a managed WebSocket connection with heartbeat and state tracking
    /// </summary>
    public class ManagedWebSocketConnection : IDisposable
    {
        private readonly System.Net.WebSockets.WebSocket _webSocket;
        private readonly ILogger<ManagedWebSocketConnection> _logger;
        private readonly WebSocketOptions _options;
        private readonly Timer _heartbeatTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private volatile bool _isDisposed;
        private DateTime _lastHeartbeatReceived;
        private DateTime _lastHeartbeatSent;
        private int _missedHeartbeats;
        private readonly object _stateLock = new object();

        public string ConnectionId { get; }
        public DateTime ConnectedTime { get; }
        public string RemoteEndpoint { get; }
        public WebSocketState State => _webSocket.State;
        public bool IsHealthy => State == WebSocketState.Open && _missedHeartbeats < _options.MaxMissedHeartbeats;

        /// <summary>
        /// Event fired when connection state changes
        /// </summary>
        public event Action<ManagedWebSocketConnection, WebSocketState> StateChanged;

        /// <summary>
        /// Event fired when heartbeat fails
        /// </summary>
        public event Action<ManagedWebSocketConnection> HeartbeatFailed;

        public ManagedWebSocketConnection(
            System.Net.WebSockets.WebSocket webSocket,
            string connectionId,
            string remoteEndpoint,
            IOptions<WebSocketOptions> options,
            ILogger<ManagedWebSocketConnection> logger)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            RemoteEndpoint = remoteEndpoint ?? throw new ArgumentNullException(nameof(remoteEndpoint));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConnectedTime = DateTime.UtcNow;
            _lastHeartbeatReceived = ConnectedTime;
            _lastHeartbeatSent = ConnectedTime;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start heartbeat timer
            _heartbeatTimer = new Timer(SendHeartbeat, null,
                TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds),
                TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));

            _logger.LogInformation("WebSocket connection {ConnectionId} established from {RemoteEndpoint}",
                ConnectionId, RemoteEndpoint);
        }

        /// <summary>
        /// Sends a heartbeat ping to the client
        /// </summary>
        private async void SendHeartbeat(object state)
        {
            if (_isDisposed || _webSocket.State != WebSocketState.Open)
                return;

            try
            {
                lock (_stateLock)
                {
                    // Check if we've missed too many heartbeats
                    var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeatReceived;
                    if (timeSinceLastHeartbeat.TotalSeconds > _options.HeartbeatIntervalSeconds * _options.MaxMissedHeartbeats)
                    {
                        _missedHeartbeats++;
                        _logger.LogWarning("Missed heartbeat {MissedCount}/{MaxMissed} for connection {ConnectionId}",
                            _missedHeartbeats, _options.MaxMissedHeartbeats, ConnectionId);

                        if (_missedHeartbeats >= _options.MaxMissedHeartbeats)
                        {
                            _logger.LogWarning("Connection {ConnectionId} considered dead after {MissedHeartbeats} missed heartbeats",
                                ConnectionId, _missedHeartbeats);
                            HeartbeatFailed?.Invoke(this);
                            return;
                        }
                    }
                }

                // Send ping frame
                var buffer = new ArraySegment<byte>(new byte[0]);
                await _webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);

                _lastHeartbeatSent = DateTime.UtcNow;
                _logger.LogDebug("Sent heartbeat ping to connection {ConnectionId}", ConnectionId);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("Connection {ConnectionId} closed during heartbeat", ConnectionId);
                HeartbeatFailed?.Invoke(this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat to connection {ConnectionId}", ConnectionId);
                HeartbeatFailed?.Invoke(this);
            }
        }

        /// <summary>
        /// Records a received heartbeat (pong)
        /// </summary>
        public void RecordHeartbeatReceived()
        {
            lock (_stateLock)
            {
                _lastHeartbeatReceived = DateTime.UtcNow;
                _missedHeartbeats = 0;
                _logger.LogDebug("Received heartbeat pong from connection {ConnectionId}", ConnectionId);
            }
        }

        /// <summary>
        /// Gets connection statistics
        /// </summary>
        public WebSocketConnectionStats GetStats()
        {
            return new WebSocketConnectionStats
            {
                ConnectionId = ConnectionId,
                RemoteEndpoint = RemoteEndpoint,
                ConnectedTime = ConnectedTime,
                LastHeartbeatReceived = _lastHeartbeatReceived,
                LastHeartbeatSent = _lastHeartbeatSent,
                MissedHeartbeats = _missedHeartbeats,
                State = _webSocket.State,
                IsHealthy = IsHealthy,
                UpTime = DateTime.UtcNow - ConnectedTime
            };
        }

        /// <summary>
        /// Sends a message through the WebSocket connection
        /// </summary>
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (_isDisposed || _webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket connection is not open");

            await _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        /// <summary>
        /// Closes the WebSocket connection gracefully
        /// </summary>
        public async Task CloseAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Server closing connection")
        {
            if (_isDisposed || _webSocket.State != WebSocketState.Open)
                return;

            try
            {
                _logger.LogInformation("Closing WebSocket connection {ConnectionId}", ConnectionId);
                await _webSocket.CloseAsync(closeStatus, statusDescription, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection {ConnectionId}", ConnectionId);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _heartbeatTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing WebSocket connection {ConnectionId}", ConnectionId);
                }
            }

            _webSocket.Dispose();
            _logger.LogInformation("WebSocket connection {ConnectionId} disposed", ConnectionId);
        }
    }

    /// <summary>
    /// Statistics for a WebSocket connection
    /// </summary>
    public class WebSocketConnectionStats
    {
        public string ConnectionId { get; set; }
        public string RemoteEndpoint { get; set; }
        public DateTime ConnectedTime { get; set; }
        public DateTime LastHeartbeatReceived { get; set; }
        public DateTime LastHeartbeatSent { get; set; }
        public int MissedHeartbeats { get; set; }
        public WebSocketState State { get; set; }
        public bool IsHealthy { get; set; }
        public TimeSpan UpTime { get; set; }
    }

    /// <summary>
    /// Manages WebSocket connections with heartbeat monitoring and connection tracking
    /// </summary>
    public class WebSocketConnectionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, ManagedWebSocketConnection> _connections;
        private readonly ILogger<WebSocketConnectionManager> _logger;
        private readonly WebSocketOptions _options;
        private readonly Timer _cleanupTimer;
        private volatile bool _isDisposed;

        /// <summary>
        /// Event fired when a connection is established
        /// </summary>
        public event Action<ManagedWebSocketConnection> ConnectionEstablished;

        /// <summary>
        /// Event fired when a connection is closed
        /// </summary>
        public event Action<ManagedWebSocketConnection> ConnectionClosed;

        public WebSocketConnectionManager(
            IOptions<WebSocketOptions> options,
            ILogger<WebSocketConnectionManager> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connections = new ConcurrentDictionary<string, ManagedWebSocketConnection>();

            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupDeadConnections, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInformation("WebSocket connection manager initialized with heartbeat interval: {HeartbeatInterval}s",
                _options.HeartbeatIntervalSeconds);
        }

        /// <summary>
        /// Registers a new WebSocket connection
        /// </summary>
        public ManagedWebSocketConnection RegisterConnection(
            System.Net.WebSockets.WebSocket webSocket,
            string remoteEndpoint)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WebSocketConnectionManager));

            var connectionId = Guid.NewGuid().ToString("N")[..8];
            var loggerFactory = _logger.GetType().Assembly.CreateInstance("Microsoft.Extensions.Logging.LoggerFactory") as ILoggerFactory;
            var connectionLogger = loggerFactory?.CreateLogger<ManagedWebSocketConnection>() ??
                throw new InvalidOperationException("Cannot create logger for connection");

            var connection = new ManagedWebSocketConnection(
                webSocket,
                connectionId,
                remoteEndpoint,
                Microsoft.Extensions.Options.Options.Create(_options),
                connectionLogger);

            // Wire up events
            connection.HeartbeatFailed += OnHeartbeatFailed;
            connection.StateChanged += OnConnectionStateChanged;

            if (_connections.TryAdd(connectionId, connection))
            {
                _logger.LogInformation("Registered WebSocket connection {ConnectionId} from {RemoteEndpoint}",
                    connectionId, remoteEndpoint);
                ConnectionEstablished?.Invoke(connection);
                return connection;
            }
            else
            {
                connection.Dispose();
                throw new InvalidOperationException($"Failed to register connection {connectionId}");
            }
        }

        /// <summary>
        /// Removes a connection from management
        /// </summary>
        public void UnregisterConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                _logger.LogInformation("Unregistered WebSocket connection {ConnectionId}", connectionId);
                ConnectionClosed?.Invoke(connection);
                connection.Dispose();
            }
        }

        /// <summary>
        /// Gets a connection by ID
        /// </summary>
        public ManagedWebSocketConnection GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        /// <summary>
        /// Gets all active connections
        /// </summary>
        public IReadOnlyCollection<ManagedWebSocketConnection> GetActiveConnections()
        {
            return _connections.Values.Where(c => c.IsHealthy).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets connection statistics
        /// </summary>
        public WebSocketManagerStats GetStats()
        {
            var connections = _connections.Values.ToList();
            return new WebSocketManagerStats
            {
                TotalConnections = connections.Count,
                HealthyConnections = connections.Count(c => c.IsHealthy),
                UnhealthyConnections = connections.Count(c => !c.IsHealthy),
                ConnectionStats = connections.Select(c => c.GetStats()).ToList()
            };
        }

        /// <summary>
        /// Broadcasts a message to all healthy connections
        /// </summary>
        public async Task BroadcastMessageAsync(byte[] message, WebSocketMessageType messageType = WebSocketMessageType.Text)
        {
            var healthyConnections = GetActiveConnections();
            var tasks = healthyConnections.Select(async connection =>
            {
                try
                {
                    await connection.SendAsync(
                        new ArraySegment<byte>(message),
                        messageType,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting to connection {ConnectionId}", connection.ConnectionId);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogDebug("Broadcasted message to {ConnectionCount} connections", healthyConnections.Count);
        }

        /// <summary>
        /// Closes all connections gracefully
        /// </summary>
        public async Task CloseAllConnectionsAsync()
        {
            _logger.LogInformation("Closing all WebSocket connections...");

            var connections = _connections.Values.ToList();
            var tasks = connections.Select(async connection =>
            {
                try
                {
                    await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutdown");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing connection {ConnectionId}", connection.ConnectionId);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation("Closed {ConnectionCount} WebSocket connections", connections.Count);
        }

        private void OnHeartbeatFailed(ManagedWebSocketConnection connection)
        {
            _logger.LogWarning("Heartbeat failed for connection {ConnectionId}, removing from management",
                connection.ConnectionId);
            UnregisterConnection(connection.ConnectionId);
        }

        private void OnConnectionStateChanged(ManagedWebSocketConnection connection, WebSocketState newState)
        {
            _logger.LogDebug("Connection {ConnectionId} state changed to {State}",
                connection.ConnectionId, newState);

            if (newState == WebSocketState.Closed || newState == WebSocketState.Aborted)
            {
                UnregisterConnection(connection.ConnectionId);
            }
        }

        private async void CleanupDeadConnections(object state)
        {
            if (_isDisposed)
                return;

            var deadConnections = _connections.Values
                .Where(c => !c.IsHealthy || c.State != WebSocketState.Open)
                .ToList();

            foreach (var connection in deadConnections)
            {
                _logger.LogInformation("Cleaning up dead connection {ConnectionId}", connection.ConnectionId);
                UnregisterConnection(connection.ConnectionId);
            }

            if (deadConnections.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} dead connections", deadConnections.Count);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cleanupTimer?.Dispose();

            var connections = _connections.Values.ToList();
            foreach (var connection in connections)
            {
                connection.Dispose();
            }

            _connections.Clear();
            _logger.LogInformation("WebSocket connection manager disposed");
        }
    }

    /// <summary>
    /// Statistics for the WebSocket connection manager
    /// </summary>
    public class WebSocketManagerStats
    {
        public int TotalConnections { get; set; }
        public int HealthyConnections { get; set; }
        public int UnhealthyConnections { get; set; }
        public List<WebSocketConnectionStats> ConnectionStats { get; set; } = new();
    }
}
