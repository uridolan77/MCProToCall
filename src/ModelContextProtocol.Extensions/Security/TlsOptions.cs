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
    /// Certificate transparency options
    /// </summary>
    public class CertificateTransparencyOptions
    {
        /// <summary>
        /// Whether to verify certificates in Certificate Transparency logs
        /// </summary>
        public bool VerifyCertificateTransparency { get; set; } = true;

        /// <summary>
        /// Whether to require SCTs (Signed Certificate Timestamps) in certificates
        /// </summary>
        public bool RequireEmbeddedScts { get; set; } = true;

        /// <summary>
        /// Minimum number of SCTs required for a certificate to be considered valid
        /// </summary>
        public int MinimumSctCount { get; set; } = 2;

        /// <summary>
        /// URL of the CT log API to use for verification
        /// </summary>
        public string CtLogApiUrl { get; set; } = "https://ct.googleapis.com/logs/";

        /// <summary>
        /// Whether to allow certificates when CT verification is unavailable
        /// </summary>
        public bool AllowWhenCtUnavailable { get; set; } = false;

        /// <summary>
        /// List of trusted CT logs
        /// </summary>
        public List<string> TrustedCtLogs { get; set; } = new List<string>
        {
            "ct.googleapis.com/logs/",
            "ct.cloudflare.com/logs/"
        };
    }
}
