using System.Collections.Generic;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Configuration options for connection string resolution
    /// </summary>
    public class ConnectionStringResolverOptions
    {
        /// <summary>
        /// Whether to use environment variables as fallback when Key Vault is unavailable
        /// </summary>
        public bool UseEnvironmentVariablesFallback { get; set; } = true;

        /// <summary>
        /// Whether to use configuration fallback when Key Vault is unavailable
        /// </summary>
        public bool UseConfigurationFallback { get; set; } = true;

        /// <summary>
        /// Prefix for environment variables that contain fallback values
        /// </summary>
        public string EnvironmentVariablePrefix { get; set; } = "MSSQLDB_";

        /// <summary>
        /// Mapping of secret names to environment variable names
        /// </summary>
        public Dictionary<string, string> SecretToEnvironmentMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Whether to cache resolved connection strings
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache expiration time in minutes
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum number of retry attempts for Key Vault operations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay for retry operations in milliseconds
        /// </summary>
        public int RetryBaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Whether to validate connection strings after resolution
        /// </summary>
        public bool ValidateConnectionStrings { get; set; } = true;

        /// <summary>
        /// Timeout for connection string validation in seconds
        /// </summary>
        public int ValidationTimeoutSeconds { get; set; } = 30;
    }
}
