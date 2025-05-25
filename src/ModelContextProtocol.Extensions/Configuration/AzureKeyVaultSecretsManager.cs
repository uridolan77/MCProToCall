using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Azure Key Vault implementation of secrets manager
    /// </summary>
    public class AzureKeyVaultSecretsManager : ISecretsManager
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<AzureKeyVaultSecretsManager> _logger;
        private readonly AzureKeyVaultSecretsOptions _options;

        public AzureKeyVaultSecretsManager(
            SecretClient secretClient,
            ILogger<AzureKeyVaultSecretsManager> logger,
            IOptions<AzureKeyVaultSecretsOptions> options)
        {
            _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string?> GetSecretAsync(string key, string? version = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            try
            {
                var secretName = NormalizeSecretName(key);
                KeyVaultSecret secret;

                if (!string.IsNullOrEmpty(version))
                {
                    secret = await _secretClient.GetSecretAsync(secretName, version, cancellationToken);
                }
                else
                {
                    secret = await _secretClient.GetSecretAsync(secretName, null, cancellationToken);
                }

                _logger.LogDebug("Retrieved secret: {SecretName}", secretName);
                return secret.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug("Secret not found: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secret: {Key}", key);
                throw;
            }
        }

        public async Task<T?> GetTypedSecretAsync<T>(string key, string? version = null, CancellationToken cancellationToken = default)
        {
            var secretValue = await GetSecretAsync(key, version, cancellationToken);
            if (secretValue == null)
                return default;

            try
            {
                if (typeof(T) == typeof(string))
                    return (T)(object)secretValue;

                return JsonSerializer.Deserialize<T>(secretValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing secret to type {Type}: {Key}", typeof(T).Name, key);
                throw;
            }
        }

        public async Task SetSecretAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var secretName = NormalizeSecretName(key);
                var secret = new KeyVaultSecret(secretName, value);

                if (expiry.HasValue)
                {
                    secret.Properties.ExpiresOn = DateTimeOffset.UtcNow.Add(expiry.Value);
                }

                // Add metadata tags
                secret.Properties.Tags[_options.CreatedByTag] = _options.ServiceName;
                secret.Properties.Tags[_options.CreatedAtTag] = DateTimeOffset.UtcNow.ToString("O");

                await _secretClient.SetSecretAsync(secret, cancellationToken);
                _logger.LogInformation("Set secret: {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting secret: {Key}", key);
                throw;
            }
        }

        public async Task SetTypedSecretAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            string serializedValue;
            if (typeof(T) == typeof(string))
            {
                serializedValue = value.ToString()!;
            }
            else
            {
                serializedValue = JsonSerializer.Serialize(value);
            }

            await SetSecretAsync(key, serializedValue, expiry, cancellationToken);
        }

        public async Task<string> RotateSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            try
            {
                var secretName = NormalizeSecretName(key);

                // Get current secret
                var currentSecret = await _secretClient.GetSecretAsync(secretName, null, cancellationToken);

                // Generate new value (this is a simple example - in practice, you'd use proper secret generation)
                var newValue = GenerateNewSecretValue(currentSecret.Value.Value);

                // Create new version
                var newSecret = new KeyVaultSecret(secretName, newValue);
                newSecret.Properties.Tags[_options.RotatedAtTag] = DateTimeOffset.UtcNow.ToString("O");
                newSecret.Properties.Tags[_options.RotatedFromTag] = currentSecret.Value.Properties.Version;

                var response = await _secretClient.SetSecretAsync(newSecret, cancellationToken);

                _logger.LogInformation("Rotated secret: {SecretName}, new version: {Version}",
                    secretName, response.Value.Properties.Version);

                return response.Value.Properties.Version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating secret: {Key}", key);
                throw;
            }
        }

        public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            try
            {
                var secretName = NormalizeSecretName(key);
                await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken);
                _logger.LogInformation("Deleted secret: {SecretName}", secretName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug("Secret not found for deletion: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting secret: {Key}", key);
                throw;
            }
        }

        public async Task<string[]> ListSecretsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var secrets = new List<string>();

                await foreach (var secretProperties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
                {
                    if (secretProperties.Enabled == true)
                    {
                        secrets.Add(secretProperties.Name);
                    }
                }

                _logger.LogDebug("Listed {Count} secrets", secrets.Count);
                return secrets.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing secrets");
                throw;
            }
        }

        public async Task<SecretMetadata?> GetSecretMetadataAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            try
            {
                var secretName = NormalizeSecretName(key);
                var secret = await _secretClient.GetSecretAsync(secretName, null, cancellationToken);

                return new SecretMetadata
                {
                    Key = key,
                    Version = secret.Value.Properties.Version,
                    CreatedAt = secret.Value.Properties.CreatedOn?.DateTime ?? DateTime.MinValue,
                    UpdatedAt = secret.Value.Properties.UpdatedOn?.DateTime,
                    ExpiresAt = secret.Value.Properties.ExpiresOn?.DateTime,
                    Tags = secret.Value.Properties.Tags.Keys.ToArray(),
                    IsActive = secret.Value.Properties.Enabled ?? false,
                    ContentType = secret.Value.Properties.ContentType ?? "text/plain"
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting secret metadata: {Key}", key);
                throw;
            }
        }

        public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var secretName = NormalizeSecretName(key);
                await _secretClient.GetSecretAsync(secretName, null, cancellationToken);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking secret existence: {Key}", key);
                throw;
            }
        }

        private string NormalizeSecretName(string key)
        {
            // Azure Key Vault secret names must match ^[0-9a-zA-Z-]+$
            return key.Replace("_", "-").Replace(".", "-").Replace(":", "-");
        }

        private string GenerateNewSecretValue(string currentValue)
        {
            // This is a simple example - in practice, you'd implement proper secret generation
            // based on the type of secret (password, API key, connection string, etc.)
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    /// Configuration options for Azure Key Vault secrets manager
    /// </summary>
    public class AzureKeyVaultSecretsOptions
    {
        public string ServiceName { get; set; } = "mcp-extensions";
        public string CreatedByTag { get; set; } = "created-by";
        public string CreatedAtTag { get; set; } = "created-at";
        public string RotatedAtTag { get; set; } = "rotated-at";
        public string RotatedFromTag { get; set; } = "rotated-from";
        public TimeSpan DefaultCacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableCaching { get; set; } = true;
        public bool EnableAuditLogging { get; set; } = true;
    }
}
