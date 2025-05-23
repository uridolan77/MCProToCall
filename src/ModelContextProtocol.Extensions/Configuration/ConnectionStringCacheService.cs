using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Singleton service to pre-resolve and cache connection strings at application startup
    /// </summary>
    public class ConnectionStringCacheService : IConnectionStringCacheService
    {
        private readonly ILogger<ConnectionStringCacheService> _logger;
        private readonly IAzureKeyVaultConnectionStringResolver _resolver;
        private readonly IConfiguration _configuration;
        private readonly ConnectionStringResolverOptions _options;

        // Static cache to store resolved connection strings
        private static readonly ConcurrentDictionary<string, string> _connectionStringCache = new();
        private static readonly ConcurrentDictionary<string, string> _connectionStringTemplateCache = new();
        private static bool _isInitialized = false;
        private static readonly SemaphoreSlim _initSemaphore = new(1, 1);

        public ConnectionStringCacheService(
            ILogger<ConnectionStringCacheService> logger,
            IAzureKeyVaultConnectionStringResolver resolver,
            IConfiguration configuration,
            IOptions<ConnectionStringResolverOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? new ConnectionStringResolverOptions();
        }

        /// <summary>
        /// Initialize the cache by pre-resolving all connection strings in the configuration
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogInformation("Connection string cache already initialized");
                return;
            }

            await _initSemaphore.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("Connection string cache already initialized (double-check)");
                    return;
                }

                _logger.LogInformation("Initializing connection string cache...");

                try
                {
                    var connectionStringsSection = _configuration.GetSection("ConnectionStrings");
                    if (connectionStringsSection == null)
                    {
                        _logger.LogWarning("No ConnectionStrings section found in configuration");
                        _isInitialized = true;
                        return;
                    }

                    var connectionStrings = connectionStringsSection.GetChildren();
                    foreach (var connectionString in connectionStrings)
                    {
                        var name = connectionString.Key;
                        var template = connectionString.Value;

                        if (string.IsNullOrWhiteSpace(template))
                        {
                            _logger.LogWarning("Connection string '{Name}' has empty value", name);
                            continue;
                        }

                        _logger.LogInformation("Processing connection string: {Name}", name);

                        // Store the template for reference
                        _connectionStringTemplateCache[name] = template;

                        // Resolve and cache the connection string
                        try
                        {
                            var resolvedConnectionString = await _resolver.ResolveConnectionStringAsync(template);
                            _connectionStringCache[name] = resolvedConnectionString;

                            _logger.LogInformation("Successfully cached connection string: {Name}", name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to resolve connection string '{Name}' during initialization", name);
                            // Continue with other connection strings
                        }
                    }

                    _isInitialized = true;
                    _logger.LogInformation("Connection string cache initialization completed. Cached {Count} connection strings",
                        _connectionStringCache.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during connection string cache initialization");
                    throw;
                }
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// Get a resolved connection string from the cache
        /// </summary>
        public string GetConnectionString(string connectionStringName)
        {
            if (string.IsNullOrWhiteSpace(connectionStringName))
            {
                throw new ArgumentException("Connection string name cannot be null or empty", nameof(connectionStringName));
            }

            if (!_isInitialized)
            {
                _logger.LogWarning("Connection string cache not initialized. Call InitializeAsync() first.");
                return null;
            }

            if (_connectionStringCache.TryGetValue(connectionStringName, out string cachedValue))
            {
                _logger.LogDebug("Retrieved connection string '{Name}' from cache", connectionStringName);
                return cachedValue;
            }

            _logger.LogWarning("Connection string '{Name}' not found in cache", connectionStringName);
            return null;
        }

        /// <summary>
        /// Resolve a connection string template by replacing Azure Key Vault placeholders with actual values
        /// </summary>
        public async Task<string> ResolveConnectionStringAsync(string connectionStringTemplate)
        {
            if (string.IsNullOrWhiteSpace(connectionStringTemplate))
            {
                return connectionStringTemplate;
            }

            try
            {
                _logger.LogDebug("Resolving connection string template");
                return await _resolver.ResolveConnectionStringAsync(connectionStringTemplate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving connection string template");
                throw;
            }
        }

        /// <summary>
        /// Add a resolved connection string to the cache
        /// </summary>
        public void AddToCache(string connectionStringName, string resolvedConnectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionStringName))
            {
                throw new ArgumentException("Connection string name cannot be null or empty", nameof(connectionStringName));
            }

            if (resolvedConnectionString == null)
            {
                throw new ArgumentNullException(nameof(resolvedConnectionString));
            }

            _connectionStringCache[connectionStringName] = resolvedConnectionString;
            _logger.LogDebug("Added connection string '{Name}' to cache", connectionStringName);
        }

        /// <summary>
        /// Dump the cache contents for debugging
        /// </summary>
        public void DumpCacheContents()
        {
            _logger.LogInformation("=== CONNECTION STRING CACHE CONTENTS ===");
            _logger.LogInformation("Connection string cache has {Count} entries", _connectionStringCache.Count);

            foreach (var entry in _connectionStringCache)
            {
                string sanitizedValue = SanitizeConnectionString(entry.Value);
                _logger.LogInformation("Cache entry: {Key} => {Value}", entry.Key, sanitizedValue);
            }

            _logger.LogInformation("Connection string template cache has {Count} entries", _connectionStringTemplateCache.Count);

            foreach (var entry in _connectionStringTemplateCache)
            {
                _logger.LogInformation("Template cache entry: {Key} => {Value}", entry.Key, entry.Value);
            }

            _logger.LogInformation("=== END CACHE CONTENTS ===");
        }

        /// <summary>
        /// Sanitize connection string for logging (remove sensitive information)
        /// </summary>
        private static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return connectionString;
            }

            // Replace password values with asterisks
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"(password|pwd|secret|key)\s*=\s*[^;]+",
                "$1=***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return sanitized;
        }
    }
}
