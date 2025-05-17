using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PPrePorter.Core.Interfaces;

namespace PPrePorter.Core.Services
{
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

        // Method to clear caches (useful for refreshing secrets if needed)
        public void ClearCaches()
        {
            _logger.LogInformation("Connection string resolver caches cleared");
            // No need to clear caches here as they are managed by the ConnectionStringCacheService
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
    }
}