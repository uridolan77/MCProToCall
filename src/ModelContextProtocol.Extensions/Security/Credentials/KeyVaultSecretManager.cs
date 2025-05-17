using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Collections.Concurrent;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Implementation of ISecretManager that uses Azure Key Vault
    /// </summary>
    public class KeyVaultSecretManager : ISecretManager
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<KeyVaultSecretManager> _logger;
        private readonly KeyVaultOptions _options;
        private readonly ConcurrentDictionary<string, DateTime> _secretAccessTimes = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Initializes a new instance of the KeyVaultSecretManager class
        /// </summary>
        /// <param name="options">Key Vault configuration options</param>
        /// <param name="logger">Logger instance</param>
        public KeyVaultSecretManager(IOptions<KeyVaultOptions> options, ILogger<KeyVaultSecretManager> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(_options.VaultUri))
            {
                throw new ArgumentException("Key Vault URI must be specified in configuration", nameof(options));
            }

            // Create the secret client using the Key Vault URI from configuration
            var credential = CreateCredential();
            _secretClient = new SecretClient(new Uri(_options.VaultUri), credential);

            _logger.LogInformation("Initialized Key Vault Secret Manager with vault URI: {VaultUri}", _options.VaultUri);
        }

        /// <summary>
        /// Gets a secret by name
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException(nameof(secretName));
            }

            try
            {
                _logger.LogDebug("Retrieving secret: {SecretName}", secretName);

                // Record access time for rotation tracking
                _secretAccessTimes[secretName] = DateTime.UtcNow;

                // Get the secret from Key Vault
                var response = await _secretClient.GetSecretAsync(secretName);

                if (response?.Value == null)
                {
                    throw new SecretNotFoundException($"Secret '{secretName}' not found in Key Vault");
                }

                return response.Value.Value;
            }
            catch (Exception ex) when (ex is not SecretNotFoundException)
            {
                _logger.LogError(ex, "Error retrieving secret {SecretName} from Key Vault", secretName);

                // Use fallback if configured and available
                if (_options.UseFallbackSecrets && _options.FallbackSecrets.TryGetValue(secretName, out var fallbackValue))
                {
                    _logger.LogWarning("Using fallback value for secret {SecretName}", secretName);
                    return fallbackValue;
                }

                throw new SecretAccessException($"Failed to access secret '{secretName}'", ex);
            }
        }

        /// <summary>
        /// Gets a secret by name with rotation capabilities
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        public async Task<string> GetSecretWithRotationAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException(nameof(secretName));
            }

            try
            {
                // Check if rotation is needed
                if (await IsRotationNeededAsync(secretName))
                {
                    _logger.LogInformation("Secret {SecretName} needs rotation, rotating now", secretName);
                    return await RotateSecretAsync(secretName);
                }

                // Get the current secret
                return await GetSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSecretWithRotationAsync for {SecretName}", secretName);
                throw;
            }
        }

        /// <summary>
        /// Sets a secret value
        /// </summary>
        /// <param name="secretName">The name of the secret</param>
        /// <param name="secretValue">The value of the secret</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SetSecretAsync(string secretName, string secretValue)
        {
            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentNullException(nameof(secretName));
            }

            if (secretValue == null)
            {
                throw new ArgumentNullException(nameof(secretValue));
            }

            try
            {
                _logger.LogDebug("Setting secret: {SecretName}", secretName);

                // Create secret with metadata for tracking
                var secret = new KeyVaultSecret(secretName, secretValue);
                secret.Properties.ContentType = "text/plain";
                secret.Properties.ExpiresOn = DateTimeOffset.UtcNow.AddDays(_options.SecretExpiryDays);

                // Set the secret in Key Vault
                await _secretClient.SetSecretAsync(secret);

                _logger.LogInformation("Successfully set secret {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting secret {SecretName} in Key Vault", secretName);
                throw new SecretAccessException($"Failed to set secret '{secretName}'", ex);
            }
        }

        /// <summary>
        /// Checks if a secret needs rotation based on age or other criteria
        /// </summary>
        /// <param name="secretName">The name of the secret to check</param>
        /// <returns>True if the secret needs rotation, false otherwise</returns>
        public async Task<bool> IsRotationNeededAsync(string secretName)
        {
            try
            {
                // Get the secret to check its properties
                var response = await _secretClient.GetSecretAsync(secretName);

                if (response?.Value?.Properties == null)
                {
                    return false;
                }

                // Check if the secret is nearing expiration
                if (response.Value.Properties.ExpiresOn.HasValue)
                {
                    var daysUntilExpiry = (response.Value.Properties.ExpiresOn.Value - DateTimeOffset.UtcNow).TotalDays;
                    return daysUntilExpiry <= _options.RotationThresholdDays;
                }

                // Check if the secret is older than the rotation period
                if (response.Value.Properties.CreatedOn.HasValue)
                {
                    var age = (DateTimeOffset.UtcNow - response.Value.Properties.CreatedOn.Value).TotalDays;
                    return age >= _options.RotationPeriodDays;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rotation status for secret {SecretName}", secretName);
                return false;
            }
        }

        /// <summary>
        /// Rotates a secret by generating a new value
        /// </summary>
        /// <param name="secretName">The name of the secret to rotate</param>
        /// <returns>The new secret value</returns>
        public async Task<string> RotateSecretAsync(string secretName)
        {
            // This is a placeholder implementation
            // In a real implementation, this would integrate with the specific service
            // that uses this secret to update it (e.g., database password rotation)

            _logger.LogInformation("Rotating secret {SecretName}", secretName);

            // Generate a new secure random value
            var newSecretValue = GenerateSecureSecret();

            // Set the new secret value
            await SetSecretAsync(secretName, newSecretValue);

            return newSecretValue;
        }

        /// <summary>
        /// Creates the appropriate credential for Key Vault access
        /// </summary>
        private DefaultAzureCredential CreateCredential()
        {
            // DefaultAzureCredential tries multiple authentication methods in sequence
            // This works well in both development and production environments
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeManagedIdentityCredential = false
            });
        }

        /// <summary>
        /// Generates a secure random secret
        /// </summary>
        private string GenerateSecureSecret()
        {
            // Generate a cryptographically secure random string
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }
    }
}
