using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Resilience;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Middleware for request throttling and DDoS protection
    /// </summary>
    public class RequestThrottlingMiddleware
    {
        private readonly ILogger<RequestThrottlingMiddleware> _logger;
        private readonly RequestThrottlingOptions _options;
        private readonly AdaptiveRateLimiter _rateLimiter;
        private readonly ConcurrentDictionary<string, ClientThrottleInfo> _clientInfo;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        public RequestThrottlingMiddleware(
            ILogger<RequestThrottlingMiddleware> logger,
            IOptions<RequestThrottlingOptions> options,
            AdaptiveRateLimiter rateLimiter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _clientInfo = new ConcurrentDictionary<string, ClientThrottleInfo>();
            
            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, 
                _options.CleanupInterval, _options.CleanupInterval);
                
            _logger.LogInformation("Request throttling middleware initialized");
        }

        /// <summary>
        /// Process incoming request through throttling middleware
        /// </summary>
        public async Task<ThrottleResult> ProcessRequestAsync(
            string clientId, 
            string endpoint, 
            int requestSize,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(clientId))
                clientId = "anonymous";

            var clientInfo = _clientInfo.GetOrAdd(clientId, _ => new ClientThrottleInfo());
            var now = DateTime.UtcNow;

            // Check if client is currently blocked
            if (clientInfo.BlockedUntil > now)
            {
                _logger.LogWarning("Request blocked from client {ClientId} until {BlockedUntil}", 
                    clientId, clientInfo.BlockedUntil);
                
                return new ThrottleResult
                {
                    IsAllowed = false,
                    BlockedUntil = clientInfo.BlockedUntil,
                    Reason = ThrottleReason.ClientBlocked,
                    RetryAfter = clientInfo.BlockedUntil.Subtract(now)
                };
            }

            // Check suspicious activity patterns
            var suspiciousCheck = CheckSuspiciousActivity(clientInfo, endpoint, requestSize, now);
            if (!suspiciousCheck.IsAllowed)
            {
                return suspiciousCheck;
            }

            // Check rate limits
            var rateLimitResult = await _rateLimiter.IsAllowedAsync(clientId, cancellationToken);
            if (!rateLimitResult.IsAllowed)
            {
                // Increment consecutive rejections
                Interlocked.Increment(ref clientInfo.ConsecutiveRejections);
                
                // Block client if too many consecutive rejections
                if (clientInfo.ConsecutiveRejections >= _options.MaxConsecutiveRejections)
                {
                    clientInfo.BlockedUntil = now.Add(_options.BlockDuration);
                    _logger.LogWarning("Client {ClientId} blocked for {BlockDuration} due to {Rejections} consecutive rejections",
                        clientId, _options.BlockDuration, clientInfo.ConsecutiveRejections);
                }

                return new ThrottleResult
                {
                    IsAllowed = false,
                    CurrentLimit = rateLimitResult.CurrentLimit,
                    RemainingRequests = rateLimitResult.RemainingRequests,
                    RetryAfter = rateLimitResult.RetryAfter,
                    Reason = ThrottleReason.RateLimitExceeded
                };
            }

            // Request allowed - reset consecutive rejections and update metrics
            Interlocked.Exchange(ref clientInfo.ConsecutiveRejections, 0);
            UpdateClientMetrics(clientInfo, endpoint, requestSize, now);

            return new ThrottleResult
            {
                IsAllowed = true,
                CurrentLimit = rateLimitResult.CurrentLimit,
                RemainingRequests = rateLimitResult.RemainingRequests,
                RetryAfter = TimeSpan.Zero,
                Reason = ThrottleReason.Allowed
            };
        }

        /// <summary>
        /// Check for suspicious activity patterns
        /// </summary>
        private ThrottleResult CheckSuspiciousActivity(
            ClientThrottleInfo clientInfo, 
            string endpoint, 
            int requestSize, 
            DateTime now)
        {
            lock (clientInfo.Lock)
            {
                // Check for rapid-fire requests (burst detection)
                if (clientInfo.LastRequestTime.HasValue)
                {
                    var timeSinceLastRequest = now - clientInfo.LastRequestTime.Value;
                    if (timeSinceLastRequest < _options.MinRequestInterval)
                    {
                        clientInfo.BurstCount++;
                        
                        if (clientInfo.BurstCount >= _options.MaxBurstRequests)
                        {
                            clientInfo.BlockedUntil = now.Add(_options.BurstBlockDuration);
                            _logger.LogWarning("Client {ClientId} blocked for burst activity: {BurstCount} requests in rapid succession",
                                clientInfo.ClientId, clientInfo.BurstCount);
                            
                            return new ThrottleResult
                            {
                                IsAllowed = false,
                                BlockedUntil = clientInfo.BlockedUntil,
                                Reason = ThrottleReason.BurstDetected,
                                RetryAfter = _options.BurstBlockDuration
                            };
                        }
                    }
                    else if (timeSinceLastRequest > _options.BurstResetInterval)
                    {
                        // Reset burst count if enough time has passed
                        clientInfo.BurstCount = 0;
                    }
                }

                // Check for unusually large requests
                if (requestSize > _options.MaxRequestSize)
                {
                    _logger.LogWarning("Client {ClientId} sent oversized request: {RequestSize} bytes > {MaxSize} bytes",
                        clientInfo.ClientId, requestSize, _options.MaxRequestSize);
                    
                    return new ThrottleResult
                    {
                        IsAllowed = false,
                        Reason = ThrottleReason.RequestTooLarge,
                        RetryAfter = TimeSpan.FromSeconds(30)
                    };
                }

                // Check for endpoint abuse
                if (!string.IsNullOrEmpty(endpoint))
                {
                    if (!clientInfo.EndpointCounts.ContainsKey(endpoint))
                    {
                        clientInfo.EndpointCounts[endpoint] = 0;
                    }
                    
                    clientInfo.EndpointCounts[endpoint]++;
                    
                    if (clientInfo.EndpointCounts[endpoint] > _options.MaxEndpointRequestsPerWindow)
                    {
                        _logger.LogWarning("Client {ClientId} exceeded endpoint limit for {Endpoint}: {Count} requests",
                            clientInfo.ClientId, endpoint, clientInfo.EndpointCounts[endpoint]);
                        
                        return new ThrottleResult
                        {
                            IsAllowed = false,
                            Reason = ThrottleReason.EndpointAbuse,
                            RetryAfter = TimeSpan.FromMinutes(5)
                        };
                    }
                }
            }

            return new ThrottleResult { IsAllowed = true, Reason = ThrottleReason.Allowed };
        }

        /// <summary>
        /// Update client metrics after successful request processing
        /// </summary>
        private void UpdateClientMetrics(ClientThrottleInfo clientInfo, string endpoint, int requestSize, DateTime now)
        {
            lock (clientInfo.Lock)
            {
                clientInfo.LastRequestTime = now;
                clientInfo.TotalRequests++;
                clientInfo.TotalRequestSize += requestSize;
                
                if (string.IsNullOrEmpty(clientInfo.ClientId))
                {
                    clientInfo.ClientId = $"client_{now.Ticks}";
                }
            }
        }

        /// <summary>
        /// Record the completion of a request
        /// </summary>
        public void RecordRequestCompletion(string clientId, bool wasSuccessful, TimeSpan responseTime)
        {
            if (string.IsNullOrEmpty(clientId))
                clientId = "anonymous";

            _rateLimiter.RecordRequestCompletion(clientId, wasSuccessful, responseTime);

            if (_clientInfo.TryGetValue(clientId, out var clientInfo))
            {
                lock (clientInfo.Lock)
                {
                    if (wasSuccessful)
                    {
                        clientInfo.SuccessfulRequests++;
                    }
                    else
                    {
                        clientInfo.FailedRequests++;
                    }
                    
                    clientInfo.TotalResponseTime += responseTime;
                }
            }
        }

        /// <summary>
        /// Clean up expired client entries
        /// </summary>
        private void CleanupExpiredEntries(object state)
        {
            if (_disposed)
                return;

            try
            {
                var now = DateTime.UtcNow;
                var expiredClients = new List<string>();

                foreach (var kvp in _clientInfo)
                {
                    var clientInfo = kvp.Value;
                    
                    // Remove clients that haven't made requests recently and aren't blocked
                    if (clientInfo.LastRequestTime.HasValue &&
                        now - clientInfo.LastRequestTime.Value > _options.ClientExpirationTime &&
                        clientInfo.BlockedUntil <= now)
                    {
                        expiredClients.Add(kvp.Key);
                    }
                }

                foreach (var clientId in expiredClients)
                {
                    _clientInfo.TryRemove(clientId, out _);
                }

                if (expiredClients.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired client entries", expiredClients.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during client cleanup");
            }
        }

        /// <summary>
        /// Get throttling statistics
        /// </summary>
        public ThrottlingStatistics GetStatistics()
        {
            var stats = new ThrottlingStatistics
            {
                TotalClients = _clientInfo.Count,
                RateLimiterStats = _rateLimiter.GetStatistics()
            };

            var blockedClients = 0;
            var totalRequests = 0L;
            var totalSuccessfulRequests = 0L;
            var totalFailedRequests = 0L;
            var totalRequestSize = 0L;
            var totalResponseTime = TimeSpan.Zero;
            var now = DateTime.UtcNow;

            foreach (var kvp in _clientInfo)
            {
                var clientInfo = kvp.Value;
                
                if (clientInfo.BlockedUntil > now)
                {
                    blockedClients++;
                }

                lock (clientInfo.Lock)
                {
                    totalRequests += clientInfo.TotalRequests;
                    totalSuccessfulRequests += clientInfo.SuccessfulRequests;
                    totalFailedRequests += clientInfo.FailedRequests;
                    totalRequestSize += clientInfo.TotalRequestSize;
                    totalResponseTime += clientInfo.TotalResponseTime;
                }
            }

            stats.BlockedClients = blockedClients;
            stats.TotalRequests = totalRequests;
            stats.TotalSuccessfulRequests = totalSuccessfulRequests;
            stats.TotalFailedRequests = totalFailedRequests;
            stats.TotalRequestSize = totalRequestSize;
            stats.AverageResponseTime = totalRequests > 0 
                ? TimeSpan.FromMilliseconds(totalResponseTime.TotalMilliseconds / totalRequests)
                : TimeSpan.Zero;

            return stats;
        }

        /// <summary>
        /// Unblock a specific client
        /// </summary>
        public bool UnblockClient(string clientId)
        {
            if (_clientInfo.TryGetValue(clientId, out var clientInfo))
            {
                clientInfo.BlockedUntil = DateTime.UtcNow.AddSeconds(-1);
                clientInfo.ConsecutiveRejections = 0;
                clientInfo.BurstCount = 0;
                
                _logger.LogInformation("Client {ClientId} has been unblocked", clientId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dispose the throttling middleware
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _cleanupTimer?.Dispose();
            _rateLimiter?.Dispose();
            _clientInfo.Clear();
            _disposed = true;
            
            _logger.LogDebug("RequestThrottlingMiddleware disposed");
        }
    }

    /// <summary>
    /// Configuration options for request throttling
    /// </summary>
    public class RequestThrottlingOptions
    {
        public int MaxConsecutiveRejections { get; set; } = 5;
        public TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan MinRequestInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        public int MaxBurstRequests { get; set; } = 10;
        public TimeSpan BurstBlockDuration { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan BurstResetInterval { get; set; } = TimeSpan.FromSeconds(10);
        public int MaxRequestSize { get; set; } = 1024 * 1024; // 1MB
        public int MaxEndpointRequestsPerWindow { get; set; } = 1000;
        public TimeSpan ClientExpirationTime { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Result of request throttling check
    /// </summary>
    public class ThrottleResult
    {
        public bool IsAllowed { get; set; }
        public int CurrentLimit { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan RetryAfter { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public ThrottleReason Reason { get; set; }
    }

    /// <summary>
    /// Reasons for request throttling
    /// </summary>
    public enum ThrottleReason
    {
        Allowed,
        RateLimitExceeded,
        ClientBlocked,
        BurstDetected,
        RequestTooLarge,
        EndpointAbuse
    }

    /// <summary>
    /// Statistics for request throttling
    /// </summary>
    public class ThrottlingStatistics
    {
        public int TotalClients { get; set; }
        public int BlockedClients { get; set; }
        public long TotalRequests { get; set; }
        public long TotalSuccessfulRequests { get; set; }
        public long TotalFailedRequests { get; set; }
        public long TotalRequestSize { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public AdaptiveRateLimitStatistics RateLimiterStats { get; set; }
    }

    /// <summary>
    /// Information tracked for each client
    /// </summary>
    internal class ClientThrottleInfo
    {
        public readonly object Lock = new object();
        public string ClientId { get; set; }
        public DateTime? LastRequestTime { get; set; }
        public DateTime BlockedUntil { get; set; }
        public volatile int ConsecutiveRejections;
        public int BurstCount { get; set; }
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long TotalRequestSize { get; set; }
        public TimeSpan TotalResponseTime { get; set; }
        public Dictionary<string, int> EndpointCounts { get; set; } = new Dictionary<string, int>();
    }
}
