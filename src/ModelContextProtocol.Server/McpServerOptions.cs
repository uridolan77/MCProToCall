using System.Collections.Generic;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Server
{
    /// <summary>
    /// MCP Server configuration options
    /// </summary>
    public class McpServerOptions
    {
        /// <summary>
        /// Host to bind the server to (default: 127.0.0.1)
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";
        
        /// <summary>
        /// Port to listen on (default: 8080)
        /// </summary>
        public int Port { get; set; } = 8080;
        
        /// <summary>
        /// Available resources
        /// </summary>
        public List<McpResource> Resources { get; set; } = new List<McpResource>();
        
        /// <summary>
        /// Available tools
        /// </summary>
        public List<McpTool> Tools { get; set; } = new List<McpTool>();
        
        /// <summary>
        /// Available prompts
        /// </summary>
        public List<McpPrompt> Prompts { get; set; } = new List<McpPrompt>();
        
        /// <summary>
        /// Whether to enable TLS/HTTPS (recommended for production)
        /// </summary>
        public bool UseTls { get; set; } = false;
        
        /// <summary>
        /// Path to TLS certificate file
        /// </summary>
        public string CertificatePath { get; set; }
        
        /// <summary>
        /// Password for TLS certificate
        /// </summary>
        public string CertificatePassword { get; set; }
        
        /// <summary>
        /// Certificate thumbprint (if loading from certificate store)
        /// </summary>
        public string CertificateThumbprint { get; set; }
        
        /// <summary>
        /// Whether to require client certificate authentication
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;
        
        /// <summary>
        /// List of allowed client certificate thumbprints (if empty, all valid client certificates are accepted)
        /// </summary>
        public List<string> AllowedClientCertificateThumbprints { get; set; } = new List<string>();
        
        /// <summary>
        /// Whether to check certificate revocation
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = true;
        
        /// <summary>
        /// Path for storing certificate revocation lists
        /// </summary>
        public string RevocationCachePath { get; set; } = "./certs/revocation";
        
        /// <summary>
        /// How often to update certificate revocation lists (in hours)
        /// </summary>
        public int CrlUpdateIntervalHours { get; set; } = 24;
        
        /// <summary>
        /// Whether to allow potentially untrusted certificates (not recommended for production)
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; } = false;
        
        /// <summary>
        /// Whether to use certificate pinning for enhanced security
        /// </summary>
        public bool UseCertificatePinning { get; set; } = false;
        
        /// <summary>
        /// List of paths to pinned certificates that are automatically trusted
        /// </summary>
        public List<string> PinnedCertificates { get; set; } = new List<string>();
        
        /// <summary>
        /// Path for storing certificate pins
        /// </summary>
        public string CertificatePinStoragePath { get; set; } = "./certs/pins";
        
        /// <summary>
        /// Maximum number of TLS connections allowed per IP address
        /// </summary>
        public int MaxConnectionsPerIpAddress { get; set; } = 100;
        
        /// <summary>
        /// Duration of the connection rate limiting window in seconds
        /// </summary>
        public int ConnectionRateLimitingWindowSeconds { get; set; } = 60;
        
        /// <summary>
        /// TLS protocols to enable (defaults to TLS 1.2 and 1.3)
        /// </summary>
        public TlsProtocolOptions TlsProtocols { get; set; } = new TlsProtocolOptions();
        
        /// <summary>
        /// Whether to enable authentication
        /// </summary>
        public bool EnableAuthentication { get; set; } = false;
        
        /// <summary>
        /// JWT authentication settings
        /// </summary>
        public JwtAuthOptions JwtAuth { get; set; } = new JwtAuthOptions();
        
        /// <summary>
        /// Input validation settings
        /// </summary>
        public ValidationOptions Validation { get; set; } = new ValidationOptions();
        
        /// <summary>
        /// Rate limiting settings
        /// </summary>
        public RateLimitOptions RateLimit { get; set; } = new RateLimitOptions();
    }

    /// <summary>
    /// TLS protocol options
    /// </summary>
    public class TlsProtocolOptions
    {
        /// <summary>
        /// Whether to enable TLS 1.2 (recommended)
        /// </summary>
        public bool EnableTls12 { get; set; } = true;
        
        /// <summary>
        /// Whether to enable TLS 1.3 (recommended)
        /// </summary>
        public bool EnableTls13 { get; set; } = true;
        
        /// <summary>
        /// Whether to enable TLS 1.1 (not recommended)
        /// </summary>
        public bool EnableTls11 { get; set; } = false;
        
        /// <summary>
        /// Whether to enable TLS 1.0 (not recommended)
        /// </summary>
        public bool EnableTls10 { get; set; } = false;
        
        /// <summary>
        /// Whether to enable SSL 3.0 (strongly not recommended)
        /// </summary>
        public bool EnableSsl3 { get; set; } = false;
    }

    /// <summary>
    /// JWT authentication options
    /// </summary>
    public class JwtAuthOptions
    {
        /// <summary>
        /// Secret key for JWT token signing
        /// </summary>
        public string SecretKey { get; set; }
        
        /// <summary>
        /// Token issuer
        /// </summary>
        public string Issuer { get; set; }
        
        /// <summary>
        /// Token audience
        /// </summary>
        public string Audience { get; set; }
        
        /// <summary>
        /// Access token expiration in minutes
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        
        /// <summary>
        /// Refresh token expiration in days
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }

    /// <summary>
    /// Input validation options
    /// </summary>
    public class ValidationOptions
    {
        /// <summary>
        /// Maximum request size in bytes
        /// </summary>
        public int MaxRequestSize { get; set; } = 1024 * 1024; // 1 MB
        
        /// <summary>
        /// Whether to enforce strict schema validation
        /// </summary>
        public bool StrictSchemaValidation { get; set; } = true;
    }

    /// <summary>
    /// Rate limiting options
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Whether to enable rate limiting
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// Maximum requests per minute per client
        /// </summary>
        public int RequestsPerMinute { get; set; } = 60;
        
        /// <summary>
        /// Maximum requests per day per client
        /// </summary>
        public int RequestsPerDay { get; set; } = 1000;
    }
}