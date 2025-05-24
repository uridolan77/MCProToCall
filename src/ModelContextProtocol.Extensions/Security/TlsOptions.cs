using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Options for TLS configuration
    /// </summary>
    public class TlsOptions
    {
        /// <summary>
        /// Whether to use TLS
        /// </summary>
        public bool UseTls { get; set; } = false;

        /// <summary>
        /// The path to the certificate file
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// The password for the certificate file
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// The thumbprint of the certificate to use from the certificate store
        /// </summary>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Whether to check certificate revocation
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = true;

        /// <summary>
        /// Whether to require client certificates
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;

        /// <summary>
        /// The thumbprints of allowed client certificates
        /// </summary>
        public List<string> AllowedClientCertificateThumbprints { get; set; } = new List<string>();

        /// <summary>
        /// Whether to allow untrusted certificates (should only be used in development)
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; } = false;

        /// <summary>
        /// Whether to allow self-signed certificates
        /// </summary>
        public bool AllowSelfSignedCertificates { get; set; } = false;

        /// <summary>
        /// List of hostnames to trust even if the certificate doesn't match
        /// This should only be used in specific scenarios where hostname validation isn't possible
        /// </summary>
        public List<string> TrustedHostOverrides { get; set; } = new List<string>();

        /// <summary>
        /// Minimum TLS version to accept (e.g., "1.2", "1.3")
        /// </summary>
        public string MinimumTlsVersion { get; set; } = "1.2";

        /// <summary>
        /// Certificate pinning options
        /// </summary>
        public CertificatePinningOptions CertificatePinning { get; set; } = new CertificatePinningOptions();

        /// <summary>
        /// Certificate revocation options
        /// </summary>
        public CertificateRevocationOptions RevocationOptions { get; set; } = new CertificateRevocationOptions();

        /// <summary>
        /// Certificate transparency options
        /// </summary>
        public CertificateTransparencyOptions CertificateTransparencyOptions { get; set; } = new CertificateTransparencyOptions();

        /// <summary>
        /// Hardware Security Module options
        /// </summary>
        public HsmOptions HsmOptions { get; set; } = new HsmOptions();

        /// <summary>
        /// Protocol negotiation options
        /// </summary>
        public ProtocolNegotiationOptions ProtocolNegotiation { get; set; } = new ProtocolNegotiationOptions();

        /// <summary>
        /// Bulkhead isolation options
        /// </summary>
        public BulkheadOptions BulkheadOptions { get; set; } = new BulkheadOptions();

        /// <summary>
        /// Request hedging options
        /// </summary>
        public HedgingOptions HedgingOptions { get; set; } = new HedgingOptions();
    }

    /// <summary>
    /// Certificate pinning options
    /// </summary>
    public class CertificatePinningOptions
    {
        /// <summary>
        /// Whether certificate pinning is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// The thumbprints of pinned certificates
        /// </summary>
        public List<string> PinnedCertificates { get; set; } = new List<string>();

        /// <summary>
        /// Path to the directory containing pinned certificates
        /// </summary>
        public string PinnedCertificatesPath { get; set; } = "./certs/pinned";

        /// <summary>
        /// Whether to automatically pin the first certificate seen
        /// </summary>
        public bool AutoPinFirstCertificate { get; set; } = false;

        /// <summary>
        /// Whether to allow self-signed certificates if pinned
        /// </summary>
        public bool AllowSelfSignedIfPinned { get; set; } = false;
    }

    /// <summary>
    /// Certificate revocation options
    /// </summary>
    public class CertificateRevocationOptions
    {
        /// <summary>
        /// Whether to check certificate revocation
        /// </summary>
        public bool CheckRevocation { get; set; } = true;

        /// <summary>
        /// Whether to use OCSP for revocation checking
        /// </summary>
        public bool UseOcsp { get; set; } = true;

        /// <summary>
        /// Whether to use CRL for revocation checking
        /// </summary>
        public bool UseCrl { get; set; } = true;

        /// <summary>
        /// Whether to cache revocation results
        /// </summary>
        public bool CacheRevocationResults { get; set; } = true;

        /// <summary>
        /// How long to cache revocation results in minutes
        /// </summary>
        public int CacheTimeMinutes { get; set; } = 60;

        /// <summary>
        /// Whether to use OCSP stapling when available
        /// </summary>
        public bool UseOcspStapling { get; set; } = true;

        /// <summary>
        /// Path to the directory for caching revocation information
        /// </summary>
        public string RevocationCachePath { get; set; } = "./certs/revocation";

        /// <summary>
        /// Whether to fail closed (reject) or open (accept) when revocation checking fails
        /// </summary>
        public bool FailClosed { get; set; } = true;

        /// <summary>
        /// Timeout for revocation checking in seconds
        /// </summary>
        public int RevocationCheckTimeoutSeconds { get; set; } = 15;
    }

    /// <summary>
    /// Hardware Security Module options
    /// </summary>
    public class HsmOptions
    {
        /// <summary>
        /// Whether to use Hardware Security Module
        /// </summary>
        public bool UseHsm { get; set; } = false;

        /// <summary>
        /// HSM provider type (AzureKeyVault, PKCS11, etc.)
        /// </summary>
        public string ProviderType { get; set; } = "AzureKeyVault";

        /// <summary>
        /// HSM connection string or configuration
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Certificate identifier in HSM
        /// </summary>
        public string CertificateIdentifier { get; set; }

        /// <summary>
        /// Key identifier for signing operations
        /// </summary>
        public string SigningKeyIdentifier { get; set; }

        /// <summary>
        /// Key identifier for encryption operations
        /// </summary>
        public string EncryptionKeyIdentifier { get; set; }

        /// <summary>
        /// Timeout for HSM operations in seconds
        /// </summary>
        public int OperationTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Protocol negotiation options
    /// </summary>
    public class ProtocolNegotiationOptions
    {
        /// <summary>
        /// Whether to enable protocol negotiation
        /// </summary>
        public bool EnableNegotiation { get; set; } = true;

        /// <summary>
        /// Supported protocols in order of preference
        /// </summary>
        public List<string> SupportedProtocols { get; set; } = new List<string>
        {
            "json-rpc",
            "msgpack",
            "grpc"
        };

        /// <summary>
        /// Default protocol if negotiation fails
        /// </summary>
        public string DefaultProtocol { get; set; } = "json-rpc";

        /// <summary>
        /// Negotiation timeout in seconds
        /// </summary>
        public int NegotiationTimeoutSeconds { get; set; } = 5;
    }

    /// <summary>
    /// Bulkhead isolation options
    /// </summary>
    public class BulkheadOptions
    {
        /// <summary>
        /// Whether to enable bulkhead isolation
        /// </summary>
        public bool EnableBulkhead { get; set; } = true;

        /// <summary>
        /// Maximum concurrent executions
        /// </summary>
        public int MaxConcurrentExecutions { get; set; } = 100;

        /// <summary>
        /// Maximum queue size
        /// </summary>
        public int MaxQueueSize { get; set; } = 1000;

        /// <summary>
        /// Queue timeout in seconds
        /// </summary>
        public int QueueTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Request hedging options
    /// </summary>
    public class HedgingOptions
    {
        /// <summary>
        /// Whether to enable request hedging
        /// </summary>
        public bool EnableHedging { get; set; } = false;

        /// <summary>
        /// Delay before starting hedged request in milliseconds
        /// </summary>
        public int HedgingDelayMs { get; set; } = 100;

        /// <summary>
        /// Maximum number of hedged requests
        /// </summary>
        public int MaxHedgedRequests { get; set; } = 2;

        /// <summary>
        /// Operations that support hedging
        /// </summary>
        public List<string> HedgedOperations { get; set; } = new List<string>
        {
            "tools/call",
            "resources/read",
            "prompts/get"
        };
    }
}
