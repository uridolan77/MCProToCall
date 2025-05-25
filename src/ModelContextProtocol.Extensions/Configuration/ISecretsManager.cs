using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Interface for managing secrets and sensitive configuration
    /// </summary>
    public interface ISecretsManager
    {
        /// <summary>
        /// Gets a secret value by key
        /// </summary>
        Task<string?> GetSecretAsync(string key, string? version = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a typed secret value
        /// </summary>
        Task<T?> GetTypedSecretAsync<T>(string key, string? version = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a secret value
        /// </summary>
        Task SetSecretAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a typed secret value
        /// </summary>
        Task SetTypedSecretAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rotates a secret (creates new version)
        /// </summary>
        Task<string> RotateSecretAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a secret
        /// </summary>
        Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all secret keys (without values)
        /// </summary>
        Task<string[]> ListSecretsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets secret metadata
        /// </summary>
        Task<SecretMetadata?> GetSecretMetadataAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a secret exists
        /// </summary>
        Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Metadata about a secret
    /// </summary>
    public class SecretMetadata
    {
        public string Key { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public bool IsActive { get; set; } = true;
        public string ContentType { get; set; } = "text/plain";
    }

    /// <summary>
    /// Options for secret operations
    /// </summary>
    public class SecretOptions
    {
        public TimeSpan? Expiry { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string ContentType { get; set; } = "text/plain";
        public bool EnableVersioning { get; set; } = true;
        public bool EnableAuditLogging { get; set; } = true;
    }

    /// <summary>
    /// Secret rotation policy
    /// </summary>
    public class SecretRotationPolicy
    {
        public TimeSpan RotationInterval { get; set; } = TimeSpan.FromDays(90);
        public bool AutoRotate { get; set; } = false;
        public string? RotationFunction { get; set; }
        public int MaxVersions { get; set; } = 10;
    }

    /// <summary>
    /// Secret access audit entry
    /// </summary>
    public class SecretAuditEntry
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}
