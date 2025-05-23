using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Service for interacting with Azure Key Vault
    /// </summary>
    public class AzureKeyVaultService : IAzureKeyVaultService
    {
        private readonly ILogger<AzureKeyVaultService> _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Constructor
        /// </summary>
        public AzureKeyVaultService(
            ILogger<AzureKeyVaultService> logger,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Get a secret from Azure Key Vault
        /// </summary>
        /// <param name="vaultName">The name of the vault</param>
        /// <param name="secretName">The name of the secret</param>
        /// <returns>The secret value</returns>
        public async Task<string> GetSecretAsync(string vaultName, string secretName)
        {
            try
            {
                _logger.LogInformation("Retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);

                // Create a URI to the key vault
                var keyVaultUri = new Uri($"https://{vaultName}.vault.azure.net/");

                // Create a client using DefaultAzureCredential
                var client = new SecretClient(keyVaultUri, new DefaultAzureCredential());

                // Get the secret
                var secret = await client.GetSecretAsync(secretName);

                if (secret?.Value?.Value == null)
                {
                    _logger.LogWarning("Secret '{SecretName}' not found in vault '{VaultName}'", secretName, vaultName);
                    return null;
                }

                _logger.LogInformation("Successfully retrieved secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                
                // Try to get a fallback value from configuration
                var fallbackSection = _configuration.GetSection("Fallbacks");
                if (fallbackSection != null)
                {
                    var fallbackKey = $"{secretName}";
                    var fallbackValue = fallbackSection[fallbackKey];
                    
                    if (!string.IsNullOrEmpty(fallbackValue))
                    {
                        _logger.LogWarning("Using fallback value for '{SecretName}' from configuration", secretName);
                        return fallbackValue;
                    }
                }

                // Re-throw the original exception if no fallback is available
                throw;
            }
        }
    }
}
