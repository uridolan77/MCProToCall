using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ModelContextProtocol.Client
{
    /// <summary>
    /// Configuration options for the MCP client
    /// </summary>
    public class McpClientOptions
    {
        /// <summary>
        /// Host to connect to (default: 127.0.0.1)
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";
        
        /// <summary>
        /// Port to connect to (default: 8080)
        /// </summary>
        public int Port { get; set; } = 8080;
        
        /// <summary>
        /// Request timeout (default: 30 seconds)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// JWT authorization token (optional)
        /// </summary>
        public string AuthToken { get; set; }
        
        /// <summary>
        /// Whether to use TLS/HTTPS (default: false)
        /// </summary>
        public bool UseTls { get; set; } = false;
        
        /// <summary>
        /// Whether to allow untrusted/self-signed server certificates (default: false)
        /// Not recommended for production!
        /// </summary>
        public bool AllowUntrustedServerCertificate { get; set; } = false;
        
        /// <summary>
        /// Path to client certificate file for mutual TLS authentication (optional)
        /// </summary>
        public string ClientCertificatePath { get; set; }
        
        /// <summary>
        /// Password for the client certificate file (optional)
        /// </summary>
        public string ClientCertificatePassword { get; set; }
        
        /// <summary>
        /// Client certificate for mutual TLS authentication (optional)
        /// This takes precedence over ClientCertificatePath if both are specified
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; }
        
        /// <summary>
        /// Server certificate validation callback (optional)
        /// If not specified and AllowUntrustedServerCertificate is false, the default validation will be used
        /// </summary>
        public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }
        
        /// <summary>
        /// Whether to automatically retry failed requests (default: true)
        /// </summary>
        public bool EnableRetry { get; set; } = true;
        
        /// <summary>
        /// Maximum number of retries for transient failures (default: 3)
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// Initial retry delay in milliseconds (default: 500ms)
        /// Will be increased exponentially for subsequent retries
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 500;
        
        /// <summary>
        /// Whether to automatically refresh the auth token when it expires (default: true)
        /// </summary>
        public bool AutoRefreshToken { get; set; } = true;
        
        /// <summary>
        /// Refresh token for authentication (used with AutoRefreshToken)
        /// </summary>
        public string RefreshToken { get; set; }
        
        /// <summary>
        /// Rate limit for requests per minute (default: 0 - no limit)
        /// </summary>
        public int RateLimitPerMinute { get; set; } = 0;
        
        /// <summary>
        /// Path to the server certificate to pin (optional)
        /// Used for certificate pinning to enhance security
        /// </summary>
        public string ServerCertificatePinPath { get; set; }
        
        /// <summary>
        /// Server certificate for certificate pinning (optional)
        /// This takes precedence over ServerCertificatePinPath if both are specified
        /// </summary>
        public X509Certificate2 ServerCertificatePin { get; set; }
        
        /// <summary>
        /// Whether to enable certificate pinning for enhanced security (default: false)
        /// </summary>
        public bool EnableCertificatePinning { get; set; } = false;
        
        /// <summary>
        /// Whether to check server certificate revocation status (default: true)
        /// </summary>
        public bool EnableRevocationCheck { get; set; } = true;
        
        /// <summary>
        /// Certificate check cache duration in minutes (default: 10 minutes)
        /// </summary>
        public int CertificateCheckCacheMinutes { get; set; } = 10;
        
        /// <summary>
        /// Path to store revocation cache (default: "./certs/revocation")
        /// </summary>
        public string RevocationCachePath { get; set; } = "./certs/revocation";
        
        /// <summary>
        /// Whether to validate all certificates in the certificate chain (default: true)
        /// </summary>
        public bool ValidateEntireCertificateChain { get; set; } = true;
        
        /// <summary>
        /// Whether to enforce strict certificate name validation (default: true)
        /// </summary>
        public bool EnforceStrictNameValidation { get; set; } = true;
        
        /// <summary>
        /// Enables detailed certificate validation logging (default: false)
        /// </summary>
        public bool EnableDetailedTlsLogging { get; set; } = false;
    }
}