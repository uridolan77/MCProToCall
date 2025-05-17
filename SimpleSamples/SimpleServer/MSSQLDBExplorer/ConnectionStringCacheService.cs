using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PPrePorter.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PPrePorter.Core.Services
{
    /// <summary>
    /// Singleton service to pre-resolve and cache connection strings at application startup
    /// </summary>
    public class ConnectionStringCacheService : IConnectionStringCacheService
    {
        private readonly ILogger<ConnectionStringCacheService> _logger;
        private readonly IAzureKeyVaultService _keyVaultService;
        private readonly IConfiguration _configuration;

        // Static cache to store resolved secrets from Azure Key Vault
        // This ensures the cache persists across all instances of the service
        private static readonly ConcurrentDictionary<string, string> _secretCache = new();

        // Static resolved connection string cache - stores fully resolved connection strings
        // Key is the connection string name, value is the resolved connection string
        private static readonly ConcurrentDictionary<string, string> _connectionStringCache = new();

        // Static cache for connection string templates - maps connection string name to template
        private static readonly ConcurrentDictionary<string, string> _connectionStringTemplateCache = new();

        // Regex to find placeholders like {azurevault:vaultName:secretName}
        private static readonly Regex AzureVaultPlaceholderRegex = new(@"\{azurevault:([^:]+):([^}]+)\}", RegexOptions.IgnoreCase);

        // Flag to track if initialization has been completed
        private bool _isInitialized = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public ConnectionStringCacheService(
            ILogger<ConnectionStringCacheService> logger,
            IAzureKeyVaultService keyVaultService,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            _logger.LogInformation("Initializing connection string cache");

            try
            {
                // Get all connection strings from configuration
                var connectionStrings = _configuration.GetSection("ConnectionStrings");
                if (connectionStrings == null)
                {
                    _logger.LogWarning("No ConnectionStrings section found in configuration");
                    _isInitialized = true;
                    return;
                }

                // Pre-resolve all connection strings
                foreach (var connectionString in connectionStrings.GetChildren())
                {
                    string name = connectionString.Key;
                    string template = connectionString.Value;

                    if (string.IsNullOrEmpty(template))
                    {
                        _logger.LogWarning("Connection string '{Name}' is null or empty", name);
                        continue;
                    }

                    // Cache the template
                    _connectionStringTemplateCache.TryAdd(name, template);

                    _logger.LogInformation("Pre-resolving connection string '{Name}'", name);
                    string resolved = await ResolveConnectionStringAsync(template);

                    // Cache the resolved connection string by name
                    _connectionStringCache.TryAdd(name, resolved);

                    // Also cache with the template as key for template-based lookups
                    string templateCacheKey = $"template:{template}";
                    _connectionStringCache.TryAdd(templateCacheKey, resolved);

                    // Log the resolved connection string (without sensitive info)
                    string sanitizedConnectionString = SanitizeConnectionString(resolved);
                    _logger.LogInformation("Pre-resolved connection string '{Name}': {ConnectionString}", name, sanitizedConnectionString);

                    // Log cache status
                    _logger.LogInformation("Connection string '{Name}' cached with {Count} total cached connection strings",
                        name, _connectionStringCache.Count);
                }

                _isInitialized = true;
                _logger.LogInformation("Connection string cache initialized successfully with {Count} connection strings", _connectionStringCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing connection string cache");
                throw;
            }
        }

        /// <summary>
        /// Get a resolved connection string from the cache
        /// </summary>
        public string GetConnectionString(string connectionStringName)
        {
            if (string.IsNullOrEmpty(connectionStringName))
            {
                throw new ArgumentException("Connection string name cannot be null or empty", nameof(connectionStringName));
            }

            // First check if we have the resolved connection string cached by name
            if (_connectionStringCache.TryGetValue(connectionStringName, out string cachedResolved))
            {
                _logger.LogDebug("Using cached resolved connection string for '{Name}'", connectionStringName);
                return cachedResolved;
            }

            // If not in name-based cache, check if we have the template cached
            if (_connectionStringTemplateCache.TryGetValue(connectionStringName, out string cachedTemplate))
            {
                _logger.LogDebug("Using cached template for '{Name}'", connectionStringName);

                try
                {
                    // Resolve the template and cache the result
                    string resolved = ResolveConnectionStringAsync(cachedTemplate).GetAwaiter().GetResult();

                    // Cache the resolved connection string with both the name and template as keys
                    _connectionStringCache.TryAdd(connectionStringName, resolved);

                    // Also cache with the template as key for template-based lookups
                    string templateCacheKey = $"template:{cachedTemplate}";
                    _connectionStringCache.TryAdd(templateCacheKey, resolved);

                    return resolved;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resolving cached template for '{Name}'", connectionStringName);
                    // Continue to try getting from configuration
                }
            }

            // If not in template cache, get from configuration
            string template = _configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(template))
            {
                _logger.LogWarning("Connection string '{Name}' not found in configuration", connectionStringName);
                return null;
            }

            // Cache the template
            _connectionStringTemplateCache.TryAdd(connectionStringName, template);

            try
            {
                // Resolve it synchronously (this should rarely happen after initialization)
                _logger.LogWarning("Connection string '{Name}' not found in cache, resolving synchronously", connectionStringName);
                string resolvedConnectionString = ResolveConnectionStringAsync(template).GetAwaiter().GetResult();

                // Cache the resolved connection string with both the name and template as keys
                _connectionStringCache.TryAdd(connectionStringName, resolvedConnectionString);

                // Also cache with the template as key for template-based lookups
                string templateCacheKey = $"template:{template}";
                _connectionStringCache.TryAdd(templateCacheKey, resolvedConnectionString);

                return resolvedConnectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving connection string '{Name}' from configuration", connectionStringName);
                return null;
            }
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

            // Check if this connection string template has already been resolved and cached in the template-based cache
            // We'll use a separate dictionary for template-based caching to avoid conflicts with name-based caching
            string templateCacheKey = $"template:{connectionStringTemplate}";
            if (_connectionStringCache.TryGetValue(templateCacheKey, out string cachedConnectionString))
            {
                _logger.LogDebug("Using cached resolved connection string for template");
                return cachedConnectionString;
            }

            _logger.LogInformation("Resolving connection string template");

            string resolvedConnectionString = connectionStringTemplate;
            MatchCollection matches = AzureVaultPlaceholderRegex.Matches(connectionStringTemplate);

            if (matches.Count == 0)
            {
                _logger.LogInformation("No Azure Key Vault placeholders found in the connection string template");
                _connectionStringCache.TryAdd(connectionStringTemplate, connectionStringTemplate);
                return connectionStringTemplate;
            }

            _logger.LogInformation("Found {MatchCount} Azure Key Vault placeholder(s) to resolve", matches.Count);

            foreach (Match match in matches)
            {
                string placeholder = match.Value; // e.g., {azurevault:vaultName:secretName}
                string vaultName = match.Groups[1].Value;
                string secretName = match.Groups[2].Value;
                string cacheKey = $"{vaultName}:{secretName}";

                _logger.LogInformation("Resolving placeholder: {Placeholder} (Vault: {VaultName}, Secret: {SecretName})", placeholder, vaultName, secretName);
                string secretValue;

                // Try to get the secret value from the cache first
                if (_secretCache.TryGetValue(cacheKey, out string cachedSecretValue))
                {
                    _logger.LogDebug("Using cached secret for '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                    secretValue = cachedSecretValue;
                }
                else
                {
                    try
                    {
                        // If not in cache, retrieve from Azure Key Vault
                        _logger.LogInformation("Retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                        secretValue = await _keyVaultService.GetSecretAsync(vaultName, secretName);

                        if (string.IsNullOrEmpty(secretValue))
                        {
                            _logger.LogWarning("Secret '{SecretName}' from vault '{VaultName}' is null or empty", secretName, vaultName);
                            continue; // Skip this placeholder if the secret value is null or empty
                        }

                        // Add to cache
                        _secretCache.TryAdd(cacheKey, secretValue);
                        _logger.LogInformation("Added secret '{SecretName}' from vault '{VaultName}' to cache", secretName, vaultName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                        throw;
                    }
                }

                // Replace the placeholder with the actual value
                resolvedConnectionString = resolvedConnectionString.Replace(placeholder, secretValue);
            }

            // Cache the fully resolved connection string using the template cache key
            _connectionStringCache.TryAdd(templateCacheKey, resolvedConnectionString);

            // Log the resolved connection string (without sensitive info)
            string sanitizedConnectionString = SanitizeConnectionString(resolvedConnectionString);
            _logger.LogInformation("Finished resolving connection string template. Result: {ConnectionString}", sanitizedConnectionString);

            return resolvedConnectionString;
        }

        /// <summary>
        /// Helper method to sanitize connection strings by hiding sensitive information
        /// </summary>
        private static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return connectionString;

            if (connectionString.Contains("password="))
            {
                // Simple string replacement
                int startIndex = connectionString.IndexOf("password=");
                if (startIndex >= 0)
                {
                    int endIndex = connectionString.IndexOf(';', startIndex);
                    if (endIndex < 0)
                        endIndex = connectionString.Length;

                    // Build the sanitized string
                    var prefix = connectionString[..startIndex];
                    var suffix = endIndex < connectionString.Length ? connectionString[endIndex..] : "";
                    return prefix + "password=***" + suffix;
                }
            }

            return connectionString;
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

            _logger.LogInformation("Secret cache has {Count} entries", _secretCache.Count);

            foreach (var entry in _secretCache)
            {
                string sanitizedValue = entry.Key.Contains("Password") ? "***" : entry.Value;
                _logger.LogInformation("Secret cache entry: {Key} => {Value}", entry.Key, sanitizedValue);
            }

            _logger.LogInformation("=== END OF CACHE CONTENTS ===");
        }

        /// <summary>
        /// Add a resolved connection string to the cache
        /// </summary>
        public void AddToCache(string connectionStringName, string resolvedConnectionString)
        {
            if (string.IsNullOrEmpty(connectionStringName))
            {
                throw new ArgumentException("Connection string name cannot be null or empty", nameof(connectionStringName));
            }

            if (string.IsNullOrEmpty(resolvedConnectionString))
            {
                throw new ArgumentException("Resolved connection string cannot be null or empty", nameof(resolvedConnectionString));
            }

            _logger.LogInformation("Manually adding connection string '{Name}' to cache", connectionStringName);

            // Add to the connection string cache
            _connectionStringCache.AddOrUpdate(connectionStringName, resolvedConnectionString, (key, oldValue) => resolvedConnectionString);

            // If we have the template, also cache with the template as key
            if (_connectionStringTemplateCache.TryGetValue(connectionStringName, out string template))
            {
                string templateCacheKey = $"template:{template}";
                _connectionStringCache.AddOrUpdate(templateCacheKey, resolvedConnectionString, (key, oldValue) => resolvedConnectionString);
            }

            _logger.LogInformation("Connection string '{Name}' added to cache", connectionStringName);
        }
    }
}
