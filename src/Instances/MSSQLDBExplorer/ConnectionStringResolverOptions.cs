using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PPrePorter.Core.Services
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
        /// Whether to throw an exception when a secret cannot be resolved
        /// </summary>
        public bool ThrowOnResolutionFailure { get; set; } = true;

        /// <summary>
        /// Maximum number of retry attempts for Key Vault operations
        /// </summary>
        [Range(1, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay in milliseconds for retry backoff
        /// </summary>
        [Range(100, 10000)]
        public int BaseRetryDelayMs { get; set; } = 500;

        /// <summary>
        /// Backoff multiplier for exponential retry delay
        /// </summary>
        [Range(1.0, 5.0)]
        public double RetryBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Timeout for individual Key Vault operations in seconds
        /// </summary>
        [Range(5, 300)]
        public int OperationTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to validate connection strings after resolution
        /// </summary>
        public bool ValidateConnectionStrings { get; set; } = true;

        /// <summary>
        /// Mapping of secret names to environment variable names
        /// </summary>
        public Dictionary<string, string> SecretToEnvironmentMapping { get; set; } = new Dictionary<string, string>
        {
            { "DailyActionsDB--Username", "MSSQLDB_USERNAME" },
            { "DailyActionsDB--Password", "MSSQLDB_PASSWORD" },
            { "DailyActionsDB--Server", "MSSQLDB_SERVER" },
            { "DailyActionsDB--Database", "MSSQLDB_DATABASE" }
        };
    }
}
