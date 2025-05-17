using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PPrePorter.Core.Interfaces;

namespace PPrePorter.Core.Services
{
    /// <summary>
    /// Service for resolving connection strings with Azure Key Vault placeholders
    /// </summary>
    public class AzureKeyVaultConnectionStringResolver : IAzureKeyVaultConnectionStringResolver
    {
        private readonly ILogger<AzureKeyVaultConnectionStringResolver> _logger;
        private readonly IAzureKeyVaultService _keyVaultService;

        // Regex to find placeholders like {azurevault:vaultName:secretName}
        private static readonly Regex AzureVaultPlaceholderRegex = new(@"\{azurevault:([^:]+):([^}]+)\}", RegexOptions.IgnoreCase);

        /// <summary>
        /// Constructor
        /// </summary>
        public AzureKeyVaultConnectionStringResolver(
            ILogger<AzureKeyVaultConnectionStringResolver> logger,
            IAzureKeyVaultService keyVaultService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
        }

        /// <summary>
        /// Resolve a connection string by replacing Azure Key Vault placeholders with actual values
        /// </summary>
        /// <param name="connectionString">The connection string with Azure Key Vault placeholders</param>
        /// <returns>The resolved connection string</returns>
        public async Task<string> ResolveConnectionStringAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            _logger.LogInformation("Resolving connection string with Azure Key Vault placeholders");

            string resolvedConnectionString = connectionString;
            MatchCollection matches = AzureVaultPlaceholderRegex.Matches(connectionString);

            if (matches.Count == 0)
            {
                _logger.LogInformation("No Azure Key Vault placeholders found in the connection string");
                return connectionString;
            }

            _logger.LogInformation("Found {MatchCount} Azure Key Vault placeholder(s) to resolve", matches.Count);

            foreach (Match match in matches)
            {
                string placeholder = match.Value; // e.g., {azurevault:vaultName:secretName}
                string vaultName = match.Groups[1].Value;
                string secretName = match.Groups[2].Value;

                _logger.LogInformation("Resolving placeholder: {Placeholder} (Vault: {VaultName}, Secret: {SecretName})", placeholder, vaultName, secretName);

                try
                {
                    // Retrieve from Azure Key Vault
                    _logger.LogInformation("Retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                    string secretValue = await _keyVaultService.GetSecretAsync(vaultName, secretName);

                    if (string.IsNullOrEmpty(secretValue))
                    {
                        _logger.LogWarning("Secret '{SecretName}' from vault '{VaultName}' is null or empty", secretName, vaultName);
                        continue; // Skip this placeholder if the secret value is null or empty
                    }

                    // Replace the placeholder with the actual value
                    resolvedConnectionString = resolvedConnectionString.Replace(placeholder, secretValue);
                    
                    // Log success (without revealing the actual value)
                    string logValue = secretName.Contains("Password") ? "***" : secretValue;
                    _logger.LogInformation("Successfully resolved placeholder for '{SecretName}' from vault '{VaultName}': {Value}", 
                        secretName, vaultName, logValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                    
                    // For specific secrets, provide fallback values
                    if (secretName == "DailyActionsDB--Username")
                    {
                        _logger.LogWarning("Using fallback value for '{SecretName}'", secretName);
                        resolvedConnectionString = resolvedConnectionString.Replace(placeholder, "ReportsUser");
                    }
                    else if (secretName == "DailyActionsDB--Password")
                    {
                        _logger.LogWarning("Using fallback value for '{SecretName}'", secretName);
                        resolvedConnectionString = resolvedConnectionString.Replace(placeholder, "Pp@123456");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Log the resolved connection string (without sensitive info)
            string sanitizedConnectionString = SanitizeConnectionString(resolvedConnectionString);
            _logger.LogInformation("Finished resolving connection string. Result: {ConnectionString}", sanitizedConnectionString);

            return resolvedConnectionString;
        }

        /// <summary>
        /// Helper method to sanitize connection strings by hiding sensitive information
        /// </summary>
        private static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return connectionString;

            if (connectionString.Contains("password=", StringComparison.OrdinalIgnoreCase))
            {
                // Simple string replacement
                int startIndex = connectionString.IndexOf("password=", StringComparison.OrdinalIgnoreCase);
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
