using System.Collections.Generic;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Configuration options for Azure Key Vault integration
    /// </summary>
    public class KeyVaultOptions
    {
        /// <summary>
        /// The URI of the Azure Key Vault
        /// </summary>
        public string VaultUri { get; set; }

        /// <summary>
        /// Whether to use fallback secrets when Key Vault is unavailable
        /// </summary>
        public bool UseFallbackSecrets { get; set; } = false;

        /// <summary>
        /// Fallback secrets to use when Key Vault is unavailable
        /// These should only be used in development or emergency scenarios
        /// </summary>
        public Dictionary<string, string> FallbackSecrets { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Number of days before a secret expires that it should be rotated
        /// </summary>
        public int RotationThresholdDays { get; set; } = 30;

        /// <summary>
        /// Number of days after which a secret should be rotated
        /// </summary>
        public int RotationPeriodDays { get; set; } = 90;

        /// <summary>
        /// Number of days until a secret expires after creation or rotation
        /// </summary>
        public int SecretExpiryDays { get; set; } = 180;
    }
}
