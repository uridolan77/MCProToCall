using System.Collections.Generic;

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
