using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Manages TLS connections
    /// </summary>
    public class TlsConnectionManager
    {
        private readonly ILogger<TlsConnectionManager> _logger;
        private readonly ICertificateValidator _certificateValidator;
        private readonly ICertificatePinningService _certificatePinningService;
        private readonly X509Certificate2 _serverCertificate;
        private readonly bool _requireClientCertificate;
        private readonly ConcurrentDictionary<string, TlsConnectionInfo> _connections = new ConcurrentDictionary<string, TlsConnectionInfo>();
        private readonly ConcurrentDictionary<string, int> _connectionCountsByIp = new ConcurrentDictionary<string, int>();
        private readonly int _maxConnectionsPerIp;

        /// <summary>
        /// Creates a new TLS connection manager
        /// </summary>
        public TlsConnectionManager(
            ILogger<TlsConnectionManager> logger,
            ICertificateValidator certificateValidator,
            ICertificatePinningService certificatePinningService,
            X509Certificate2 serverCertificate,
            bool requireClientCertificate,
            int maxConnectionsPerIp = 10)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
            _certificatePinningService = certificatePinningService ?? throw new ArgumentNullException(nameof(certificatePinningService));
            _serverCertificate = serverCertificate ?? throw new ArgumentNullException(nameof(serverCertificate));
            _requireClientCertificate = requireClientCertificate;
            _maxConnectionsPerIp = maxConnectionsPerIp;
        }

        /// <summary>
        /// Registers a new TLS connection
        /// </summary>
        public async Task<bool> RegisterConnectionAsync(HttpListenerContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string connectionId = GetConnectionId(context);
            string clientIp = context.Request.RemoteEndPoint.Address.ToString();
            _logger.LogInformation("Registering TLS connection {ConnectionId} from {ClientIp}", connectionId, clientIp);

            // Check connection limit for this IP
            if (!TryAddConnection(clientIp))
            {
                _logger.LogWarning("Connection limit exceeded for IP {ClientIp}", clientIp);
                return false;
            }

            // Check if client certificate is required
            if (_requireClientCertificate)
            {
                X509Certificate2 clientCertificate = await GetClientCertificateAsync(context);
                if (clientCertificate == null)
                {
                    _logger.LogWarning("Client certificate required but not provided for connection {ConnectionId}", connectionId);
                    DecrementConnectionCount(clientIp);
                    return false;
                }

                // Validate client certificate
                bool isValid = await _certificateValidator.ValidateCertificateAsync(clientCertificate);
                if (!isValid)
                {
                    _logger.LogWarning("Client certificate validation failed for connection {ConnectionId}", connectionId);
                    DecrementConnectionCount(clientIp);
                    return false;
                }

                // Check if certificate is pinned
                bool isPinned = await _certificatePinningService.ValidatePinAsync(clientCertificate);

                // Store connection info
                var connectionInfo = new TlsConnectionInfo
                {
                    ConnectionId = connectionId,
                    ClientCertificate = clientCertificate,
                    ClientAddress = context.Request.RemoteEndPoint,
                    EstablishedTime = DateTimeOffset.UtcNow,
                    IsCertificatePinned = isPinned
                };

                _connections[connectionId] = connectionInfo;
                _logger.LogInformation("TLS connection {ConnectionId} registered successfully", connectionId);
                return true;
            }
            else
            {
                // No client certificate required, just register the connection
                var connectionInfo = new TlsConnectionInfo
                {
                    ConnectionId = connectionId,
                    ClientAddress = context.Request.RemoteEndPoint,
                    EstablishedTime = DateTimeOffset.UtcNow
                };

                _connections[connectionId] = connectionInfo;
                _logger.LogInformation("TLS connection {ConnectionId} registered successfully (no client certificate)", connectionId);
                return true;
            }
        }

        /// <summary>
        /// Tries to add a connection for the specified IP address
        /// </summary>
        public bool TryAddConnection(string ipAddress)
        {
            int count = _connectionCountsByIp.AddOrUpdate(
                ipAddress,
                1,
                (_, currentCount) => currentCount + 1);

            if (count > _maxConnectionsPerIp)
            {
                _connectionCountsByIp.AddOrUpdate(
                    ipAddress,
                    0,
                    (_, currentCount) => currentCount - 1);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the client certificate from the context
        /// </summary>
        private Task<X509Certificate2> GetClientCertificateAsync(HttpListenerContext context)
        {
            try
            {
                // In a real implementation, we would extract the client certificate from the TLS handshake
                // For now, we'll just return null
                return Task.FromResult<X509Certificate2>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting client certificate");
                return Task.FromResult<X509Certificate2>(null);
            }
        }

        /// <summary>
        /// Gets a unique ID for the connection
        /// </summary>
        private string GetConnectionId(HttpListenerContext context)
        {
            return $"{context.Request.RemoteEndPoint}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Gets information about a TLS connection
        /// </summary>
        public TlsConnectionInfo GetConnectionInfo(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                return connectionInfo;
            }

            return null;
        }

        /// <summary>
        /// Removes a TLS connection
        /// </summary>
        public bool RemoveConnection(string connectionId)
        {
            _logger.LogInformation("Removing TLS connection {ConnectionId}", connectionId);

            if (_connections.TryRemove(connectionId, out var connectionInfo))
            {
                if (connectionInfo.ClientAddress != null)
                {
                    string ipAddress = connectionInfo.ClientAddress.Address.ToString();
                    _connectionCountsByIp.AddOrUpdate(
                        ipAddress,
                        0,
                        (_, currentCount) => Math.Max(0, currentCount - 1));
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Decrements the connection count for the specified IP address
        /// </summary>
        private void DecrementConnectionCount(string ipAddress)
        {
            _connectionCountsByIp.AddOrUpdate(
                ipAddress,
                0,
                (_, currentCount) => Math.Max(0, currentCount - 1));
        }

        /// <summary>
        /// Gets the server certificate
        /// </summary>
        public X509Certificate2 GetServerCertificate()
        {
            return _serverCertificate;
        }
    }

    /// <summary>
    /// Information about a TLS connection
    /// </summary>
    public class TlsConnectionInfo
    {
        /// <summary>
        /// The unique ID of the connection
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// The client certificate, if provided
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; }

        /// <summary>
        /// The client's IP address
        /// </summary>
        public IPEndPoint ClientAddress { get; set; }

        /// <summary>
        /// When the connection was established
        /// </summary>
        public DateTimeOffset EstablishedTime { get; set; }

        /// <summary>
        /// Whether the client certificate is pinned
        /// </summary>
        public bool IsCertificatePinned { get; set; }
    }
}
