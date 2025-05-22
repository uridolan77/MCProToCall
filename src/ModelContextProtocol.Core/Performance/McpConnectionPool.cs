using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Connection pool for HTTP clients
    /// </summary>
    public class McpConnectionPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, HttpClient> _clients;
        private readonly HttpClientHandler _handler;
        private readonly TimeSpan _connectionLifetime;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpConnectionPool"/> class
        /// </summary>
        /// <param name="connectionLifetime">Connection lifetime</param>
        public McpConnectionPool(TimeSpan connectionLifetime)
        {
            _clients = new ConcurrentDictionary<string, HttpClient>();
            _connectionLifetime = connectionLifetime;
            
            _handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 50,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
        }

        /// <summary>
        /// Gets a client for the specified endpoint
        /// </summary>
        /// <param name="endpoint">Endpoint URL</param>
        /// <returns>HTTP client</returns>
        public HttpClient GetClient(string endpoint)
        {
            return _clients.GetOrAdd(endpoint, key =>
            {
                var client = new HttpClient(_handler, disposeHandler: false)
                {
                    BaseAddress = new Uri(key),
                    Timeout = TimeSpan.FromSeconds(30)
                };

                // Set connection lifetime
                client.DefaultRequestHeaders.ConnectionClose = false;
                client.DefaultRequestHeaders.Add("Keep-Alive", "timeout=600");

                return client;
            });
        }

        /// <summary>
        /// Disposes the connection pool
        /// </summary>
        public void Dispose()
        {
            foreach (var client in _clients.Values)
            {
                client?.Dispose();
            }
            
            _handler?.Dispose();
        }
    }
}
