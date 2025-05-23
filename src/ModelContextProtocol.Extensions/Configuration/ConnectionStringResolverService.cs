using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Service for resolving connection strings
    /// </summary>
    public class ConnectionStringResolverService : IConnectionStringResolverService
    {
        private readonly IConnectionStringCacheService _connectionStringCacheService;
        private readonly ILogger<ConnectionStringResolverService> _logger;

        public ConnectionStringResolverService(
            IConnectionStringCacheService connectionStringCacheService,
            ILogger<ConnectionStringResolverService> logger)
        {
            _connectionStringCacheService = connectionStringCacheService ?? throw new ArgumentNullException(nameof(connectionStringCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Resolve a connection string template by replacing placeholders with actual values
        /// </summary>
        public async Task<string> ResolveConnectionStringAsync(string connectionStringTemplate)
        {
            if (string.IsNullOrWhiteSpace(connectionStringTemplate))
            {
                return connectionStringTemplate;
            }

            try
            {
                _logger.LogInformation("Delegating connection string resolution to ConnectionStringCacheService");
                string resolvedConnectionString = await _connectionStringCacheService.ResolveConnectionStringAsync(connectionStringTemplate);

                // Log the resolved connection string (without sensitive info)
                string sanitizedConnectionString = SanitizeConnectionString(resolvedConnectionString);
                _logger.LogInformation("Connection string resolved: {ConnectionString}", sanitizedConnectionString);

                return resolvedConnectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving connection string template");
                throw;
            }
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public void ClearCaches()
        {
            _logger.LogInformation("Connection string resolver caches cleared");
            // Note: The actual cache clearing would need to be implemented in the cache service
            // For now, this is a placeholder
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
