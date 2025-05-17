using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Thread-safe implementation of IConnectionStringProvider that caches connection strings
    /// </summary>
    public class CachedConnectionStringProvider : IConnectionStringProvider
    {
        private readonly IMemoryCache _cache;
        private readonly ISecretManager _secretManager;
        private readonly ILogger<CachedConnectionStringProvider> _logger;
        private readonly ConnectionStringOptions _options;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the CachedConnectionStringProvider class
        /// </summary>
        /// <param name="cache">Memory cache instance</param>
        /// <param name="secretManager">Secret manager instance</param>
        /// <param name="options">Connection string options</param>
        /// <param name="logger">Logger instance</param>
        public CachedConnectionStringProvider(
            IMemoryCache cache,
            ISecretManager secretManager,
            IOptions<ConnectionStringOptions> options,
            ILogger<CachedConnectionStringProvider> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a connection string by name
        /// </summary>
        /// <param name="name">The name of the connection string</param>
        /// <returns>The connection string</returns>
        public async Task<string> GetConnectionStringAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            // Try to get from cache first
            string cacheKey = $"ConnectionString_{name}";
            if (_cache.TryGetValue(cacheKey, out string cachedConnectionString))
            {
                _logger.LogDebug("Retrieved connection string {Name} from cache", name);
                return cachedConnectionString;
            }

            // If not in cache, acquire semaphore to prevent multiple threads from fetching the same connection string
            await _semaphore.WaitAsync();
            try
            {
                // Double-check if another thread has already fetched the connection string
                if (_cache.TryGetValue(cacheKey, out cachedConnectionString))
                {
                    _logger.LogDebug("Retrieved connection string {Name} from cache after waiting", name);
                    return cachedConnectionString;
                }

                // Get the connection string from the secret manager
                string connectionString = await _secretManager.GetSecretAsync(name);
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ConnectionStringNotFoundException($"Connection string '{name}' not found");
                }

                // Cache the connection string with appropriate expiration
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheExpirationMinutes))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(_options.SlidingExpirationMinutes));

                _cache.Set(cacheKey, connectionString, cacheEntryOptions);
                
                _logger.LogInformation("Added connection string {Name} to cache", name);
                
                return connectionString;
            }
            catch (Exception ex) when (ex is not ConnectionStringNotFoundException)
            {
                _logger.LogError(ex, "Error retrieving connection string {Name}", name);
                throw new ConnectionStringException($"Failed to retrieve connection string '{name}'", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Gets a sanitized connection string (with sensitive information removed) for logging
        /// </summary>
        /// <param name="connectionString">The original connection string</param>
        /// <returns>A sanitized version of the connection string</returns>
        public string GetSanitizedConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return string.Empty;
            }

            // Sanitize password
            string sanitized = Regex.Replace(
                connectionString,
                @"(Password|PWD)=([^;]*)",
                "$1=*****",
                RegexOptions.IgnoreCase);

            // Sanitize user secrets
            sanitized = Regex.Replace(
                sanitized,
                @"(User ID|UID)=([^;]*)",
                "$1=*****",
                RegexOptions.IgnoreCase);

            // Sanitize access keys
            sanitized = Regex.Replace(
                sanitized,
                @"(AccountKey|AccessKey)=([^;]*)",
                "$1=*****",
                RegexOptions.IgnoreCase);

            return sanitized;
        }
    }

    /// <summary>
    /// Options for connection string management
    /// </summary>
    public class ConnectionStringOptions
    {
        /// <summary>
        /// Minutes until a cached connection string expires absolutely
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Minutes of inactivity until a cached connection string expires
        /// </summary>
        public int SlidingExpirationMinutes { get; set; } = 20;

        /// <summary>
        /// Mapping of connection string names to secret names
        /// </summary>
        public Dictionary<string, string> ConnectionStringMappings { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Exception thrown when a connection string is not found
    /// </summary>
    public class ConnectionStringNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the ConnectionStringNotFoundException class
        /// </summary>
        public ConnectionStringNotFoundException() : base() { }

        /// <summary>
        /// Initializes a new instance of the ConnectionStringNotFoundException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public ConnectionStringNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the ConnectionStringNotFoundException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public ConnectionStringNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when there is an error retrieving a connection string
    /// </summary>
    public class ConnectionStringException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the ConnectionStringException class
        /// </summary>
        public ConnectionStringException() : base() { }

        /// <summary>
        /// Initializes a new instance of the ConnectionStringException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public ConnectionStringException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the ConnectionStringException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public ConnectionStringException(string message, Exception innerException) : base(message, innerException) { }
    }
}
