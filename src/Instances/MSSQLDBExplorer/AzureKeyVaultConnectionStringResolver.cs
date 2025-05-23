using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Utilities;
using PPrePorter.Core.Interfaces;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using System.ComponentModel.DataAnnotations;

namespace PPrePorter.Core.Services
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

        /// <summary>
        /// Constructor
        /// </summary>
        public AzureKeyVaultConnectionStringResolver(
            ILogger<AzureKeyVaultConnectionStringResolver> logger,
            IAzureKeyVaultService keyVaultService,
            IConfiguration configuration,
            IOptions<ConnectionStringResolverOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Initialize retry policy for Key Vault operations
            _retryPolicy = Policy
                .Handle<Exception>(ex => ShouldRetry(ex))
                .WaitAndRetryAsync(
                    retryCount: _options.MaxRetryAttempts,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(
                        _options.BaseRetryDelayMs * Math.Pow(_options.RetryBackoffMultiplier, retryAttempt - 1)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(outcome.Exception,
                            "Key Vault operation failed, retry attempt {RetryAttempt}/{MaxRetries} in {DelayMs}ms",
                            retryAttempt, _options.MaxRetryAttempts, timespan.TotalMilliseconds);
                    });

            ValidateOptions();
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
                    string logValue = secretName.Contains("Password") ? "***" : secretValue;
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

                    // Try to get fallback value from configuration
                    if (_options.UseConfigurationFallback)
                    {
                        fallbackValue = _configuration[$"Fallbacks:{secretName}"];

                        if (!string.IsNullOrEmpty(fallbackValue))
                        {
                            _logger.LogWarning("Using fallback value from configuration for '{SecretName}'", secretName);
                            resolvedConnectionString = resolvedConnectionString.Replace(placeholder, fallbackValue);
                            continue;
                        }
                    }

                    // If we couldn't find a fallback value and we're configured to throw
                    if (_options.ThrowOnResolutionFailure)
                    {
                        _logger.LogError("No fallback value found for '{SecretName}' and ThrowOnResolutionFailure is enabled", secretName);
                        throw new SecretResolutionException($"Failed to resolve secret '{secretName}' and no fallback was available", ex);
                    }

                    // If we get here, we couldn't find a fallback value but we're not configured to throw
                    _logger.LogWarning("No fallback value found for '{SecretName}', leaving placeholder unresolved", secretName);
                }
            }

            // Log the resolved connection string (without sensitive info)
            string sanitizedConnectionString = StringUtilities.SanitizeConnectionString(resolvedConnectionString);
            _logger.LogInformation("Finished resolving connection string. Result: {ConnectionString}", sanitizedConnectionString);

            return resolvedConnectionString;
        }

        /// <summary>
        /// Validates the configuration options
        /// </summary>
        private void ValidateOptions()
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(_options);

            if (!Validator.TryValidateObject(_options, context, results, true))
            {
                var errors = string.Join(", ", results.Select(r => r.ErrorMessage));
                throw new ArgumentException($"Invalid ConnectionStringResolverOptions: {errors}");
            }

            _logger.LogInformation("Connection string resolver options validated successfully");
        }

        /// <summary>
        /// Determines if an exception should trigger a retry
        /// </summary>
        private static bool ShouldRetry(Exception ex)
        {
            // Retry on network errors, timeouts, and transient Azure service errors
            return ex is System.Net.Http.HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   (ex is Azure.RequestFailedException azureEx && 
                    (azureEx.Status == (int)HttpStatusCode.TooManyRequests ||
                     azureEx.Status == (int)HttpStatusCode.InternalServerError ||
                     azureEx.Status == (int)HttpStatusCode.BadGateway ||
                     azureEx.Status == (int)HttpStatusCode.ServiceUnavailable ||
                     azureEx.Status == (int)HttpStatusCode.GatewayTimeout));
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
                    throw new SecretResolutionException($"Secret '{secretName}' returned null or empty value from vault '{vaultName}'");
                }

                return secretValue;
            });
        }
    }
}
