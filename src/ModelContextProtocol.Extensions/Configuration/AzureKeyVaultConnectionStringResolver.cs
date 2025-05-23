using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Service for resolving connection strings with Azure Key Vault placeholders
    /// </summary>
    public class AzureKeyVaultConnectionStringResolver : IAzureKeyVaultConnectionStringResolver
    {
        private readonly ILogger<AzureKeyVaultConnectionStringResolver> _logger;
        private readonly IAzureKeyVaultService _keyVaultService;
        private readonly IConfiguration _configuration;
        private readonly ConnectionStringResolverOptions _options;
        private readonly IAsyncPolicy _retryPolicy;

        // Regex to find placeholders like {azurevault:vaultName:secretName}
        private static readonly Regex AzureVaultPlaceholderRegex = new(@"\{azurevault:([^:]+):([^}]+)\}", RegexOptions.IgnoreCase);

        public AzureKeyVaultConnectionStringResolver(
            ILogger<AzureKeyVaultConnectionStringResolver> logger,
            IAzureKeyVaultService keyVaultService,
            IConfiguration configuration,
            IOptions<ConnectionStringResolverOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? new ConnectionStringResolverOptions();

            // Create retry policy
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: _options.MaxRetryAttempts,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} for Key Vault operation after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Resolve a connection string by replacing Azure Key Vault placeholders with actual values
        /// </summary>
        public async Task<string> ResolveConnectionStringAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            try
            {
                _logger.LogDebug("Resolving connection string with Azure Key Vault placeholders");

                var resolvedConnectionString = connectionString;
                var matches = AzureVaultPlaceholderRegex.Matches(connectionString);

                foreach (Match match in matches)
                {
                    var placeholder = match.Value;
                    var vaultName = match.Groups[1].Value;
                    var secretName = match.Groups[2].Value;

                    _logger.LogDebug("Found placeholder: {Placeholder} (vault: {VaultName}, secret: {SecretName})", 
                        placeholder, vaultName, secretName);

                    try
                    {
                        // Retrieve from Azure Key Vault with retry logic
                        _logger.LogInformation("Retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);
                        string secretValue = await GetSecretWithRetryAsync(vaultName, secretName);

                        if (string.IsNullOrEmpty(secretValue))
                        {
                            _logger.LogWarning("Secret '{SecretName}' from vault '{VaultName}' is null or empty", secretName, vaultName);
                            continue; // Skip this placeholder if the secret value is null or empty
                        }

                        // Replace the placeholder with the actual value
                        resolvedConnectionString = resolvedConnectionString.Replace(placeholder, secretValue);

                        // Log success (without revealing the actual value)
                        string logValue = secretName.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "***" : secretValue;
                        _logger.LogInformation("Successfully resolved placeholder for '{SecretName}' from vault '{VaultName}': {Value}",
                            secretName, vaultName, logValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving secret '{SecretName}' from vault '{VaultName}'", secretName, vaultName);

                        // Try to get fallback value from environment variables
                        string fallbackValue = null;

                        if (_options.UseEnvironmentVariablesFallback)
                        {
                            string envVarName;

                            // Check if we have a mapping for this secret
                            if (_options.SecretToEnvironmentMapping.TryGetValue(secretName, out string mappedEnvVar))
                            {
                                envVarName = mappedEnvVar;
                            }
                            else
                            {
                                // Use the default naming convention
                                envVarName = $"{_options.EnvironmentVariablePrefix}{secretName.Replace("--", "_").ToUpperInvariant()}";
                            }

                            fallbackValue = Environment.GetEnvironmentVariable(envVarName);

                            if (!string.IsNullOrEmpty(fallbackValue))
                            {
                                _logger.LogWarning("Using fallback value from environment variable for '{SecretName}'", secretName);
                                resolvedConnectionString = resolvedConnectionString.Replace(placeholder, fallbackValue);
                                continue;
                            }
                        }

                        // Try configuration fallback
                        if (_options.UseConfigurationFallback)
                        {
                            var fallbackSection = _configuration.GetSection("Fallbacks");
                            fallbackValue = fallbackSection[secretName];

                            if (!string.IsNullOrEmpty(fallbackValue))
                            {
                                _logger.LogWarning("Using fallback value from configuration for '{SecretName}'", secretName);
                                resolvedConnectionString = resolvedConnectionString.Replace(placeholder, fallbackValue);
                                continue;
                            }
                        }

                        // If no fallback is available, throw the original exception
                        throw new InvalidOperationException($"Failed to resolve secret '{secretName}' from vault '{vaultName}' and no fallback value is available", ex);
                    }
                }

                _logger.LogDebug("Connection string resolution completed successfully");
                return resolvedConnectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving connection string");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a secret from Key Vault with retry logic
        /// </summary>
        private async Task<string> GetSecretWithRetryAsync(string vaultName, string secretName)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug("Attempting to retrieve secret '{SecretName}' from vault '{VaultName}'", 
                    secretName, vaultName);
                
                var secretValue = await _keyVaultService.GetSecretAsync(vaultName, secretName);
                
                if (string.IsNullOrEmpty(secretValue))
                {
                    throw new InvalidOperationException($"Secret '{secretName}' returned null or empty value from vault '{vaultName}'");
                }

                return secretValue;
            });
        }
    }
}
