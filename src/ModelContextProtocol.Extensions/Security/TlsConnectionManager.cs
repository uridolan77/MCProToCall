using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Manages TLS connection limits and tracking
    /// </summary>
    public static class TlsConnectionManager
    {
        private static readonly ConcurrentDictionary<string, EndpointInfo> _connectionCounts = 
            new ConcurrentDictionary<string, EndpointInfo>();
            
        private static readonly Timer _cleanupTimer = new Timer(
            CleanupStaleConnections, 
            null, 
            TimeSpan.FromMinutes(5), 
            TimeSpan.FromMinutes(5));
        
        /// <summary>
        /// Checks if a connection is allowed based on the connection limit
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier (typically IP address)</param>
        /// <param name="connectionLimit">Maximum connections allowed</param>
        /// <returns>True if connection is allowed, false if limit is exceeded</returns>
        public static bool CheckConnectionLimit(string endpointIdentifier, int connectionLimit)
        {
            if (string.IsNullOrEmpty(endpointIdentifier))
                throw new ArgumentNullException(nameof(endpointIdentifier));
                
            if (connectionLimit <= 0)
                return true; // No limit enforced
                
            var info = _connectionCounts.GetOrAdd(endpointIdentifier, _ => new EndpointInfo());
            
            // Check if connection count exceeds limit
            return info.ConnectionCount < connectionLimit;
        }
        
        /// <summary>
        /// Registers a new connection for the specified endpoint
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier (typically IP address)</param>
        public static void RegisterConnection(string endpointIdentifier)
        {
            if (string.IsNullOrEmpty(endpointIdentifier))
                throw new ArgumentNullException(nameof(endpointIdentifier));
                
            var info = _connectionCounts.GetOrAdd(endpointIdentifier, _ => new EndpointInfo());
            info.IncrementConnections();
        }
        
        /// <summary>
        /// Releases a connection for the specified endpoint
        /// </summary>
        /// <param name="endpointIdentifier">Endpoint identifier (typically IP address)</param>
        public static void ReleaseConnection(string endpointIdentifier)
        {
            if (string.IsNullOrEmpty(endpointIdentifier))
                return;
                
            if (_connectionCounts.TryGetValue(endpointIdentifier, out var info))
            {
                info.DecrementConnections();
            }
        }
        
        /// <summary>
        /// Cleanup stale connection information
        /// </summary>
        private static void CleanupStaleConnections(object state)
        {
            var staleCutoff = DateTime.UtcNow.AddMinutes(-30);
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _connectionCounts)
            {
                if (kvp.Value.LastActivity < staleCutoff && kvp.Value.ConnectionCount == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _connectionCounts.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Information about an endpoint's connections
        /// </summary>
        private class EndpointInfo
        {
            private int _connectionCount = 0;
            
            /// <summary>
            /// Number of active connections
            /// </summary>
            public int ConnectionCount => _connectionCount;
            
            /// <summary>
            /// Last connection activity timestamp
            /// </summary>
            public DateTime LastActivity { get; private set; } = DateTime.UtcNow;
            
            /// <summary>
            /// Increments the connection count
            /// </summary>
            public void IncrementConnections()
            {
                Interlocked.Increment(ref _connectionCount);
                LastActivity = DateTime.UtcNow;
            }
            
            /// <summary>
            /// Decrements the connection count
            /// </summary>
            public void DecrementConnections()
            {
                Interlocked.Decrement(ref _connectionCount);
                LastActivity = DateTime.UtcNow;
            }
        }
    }
}
