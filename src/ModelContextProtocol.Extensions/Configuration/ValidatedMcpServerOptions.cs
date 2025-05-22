using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Enhanced server options with validation
    /// </summary>
    public class ValidatedMcpServerOptions : McpServerOptions, IValidatableObject
    {
        /// <summary>
        /// Gets or sets the host to bind the server to
        /// </summary>
        [Required(ErrorMessage = "Host is required")]
        [ValidHost]
        public new string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// Gets or sets the port to listen on
        /// </summary>
        [Required]
        [ValidPort]
        public new int Port { get; set; } = 8080;

        /// <summary>
        /// Gets or sets the path to TLS certificate file
        /// </summary>
        [FileExists(Required = false)]
        public new string CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the path for storing certificate revocation lists
        /// </summary>
        [DirectoryExists(CreateIfMissing = true)]
        public new string RevocationCachePath { get; set; } = "./certs/revocation";

        /// <summary>
        /// Gets or sets the path for storing certificate pins
        /// </summary>
        [DirectoryExists(CreateIfMissing = true)]
        public string CertificatePinStoragePath { get; set; } = "./certs/pins";

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation results</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // TLS-specific validation
            if (UseTls)
            {
                if (string.IsNullOrWhiteSpace(CertificatePath) && 
                    string.IsNullOrWhiteSpace(CertificateThumbprint))
                {
                    results.Add(new ValidationResult(
                        "When TLS is enabled, either CertificatePath or CertificateThumbprint must be specified",
                        new[] { nameof(CertificatePath), nameof(CertificateThumbprint) }));
                }

                if (!string.IsNullOrWhiteSpace(CertificatePath))
                {
                    // Validate certificate can be loaded
                    try
                    {
                        using var cert = new X509Certificate2(CertificatePath, CertificatePassword);
                        
                        // Check if certificate is valid
                        if (cert.NotAfter < DateTime.Now)
                        {
                            results.Add(new ValidationResult(
                                $"Certificate has expired on {cert.NotAfter}",
                                new[] { nameof(CertificatePath) }));
                        }
                        else if (cert.NotAfter < DateTime.Now.AddDays(30))
                        {
                            results.Add(new ValidationResult(
                                $"Warning: Certificate will expire soon on {cert.NotAfter}",
                                new[] { nameof(CertificatePath) }));
                        }

                        if (!cert.HasPrivateKey)
                        {
                            results.Add(new ValidationResult(
                                "Server certificate must have a private key",
                                new[] { nameof(CertificatePath) }));
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ValidationResult(
                            $"Failed to load certificate: {ex.Message}",
                            new[] { nameof(CertificatePath) }));
                    }
                }

                // Validate client certificates if required
                if (RequireClientCertificate && AllowedClientCertificateThumbprints.Count == 0)
                {
                    results.Add(new ValidationResult(
                        "When client certificates are required, at least one allowed thumbprint should be specified",
                        new[] { nameof(AllowedClientCertificateThumbprints) }));
                }
            }

            // Authentication validation
            if (EnableAuthentication)
            {
                if (string.IsNullOrWhiteSpace(JwtAuth?.SecretKey))
                {
                    results.Add(new ValidationResult(
                        "JWT secret key is required when authentication is enabled",
                        new[] { "JwtAuth.SecretKey" }));
                }

                if (JwtAuth?.SecretKey?.Length < 32)
                {
                    results.Add(new ValidationResult(
                        "JWT secret key should be at least 32 characters for security",
                        new[] { "JwtAuth.SecretKey" }));
                }
            }

            // Rate limiting validation
            if (RateLimit.Enabled)
            {
                if (RateLimit.RequestsPerMinute <= 0)
                {
                    results.Add(new ValidationResult(
                        "RequestsPerMinute must be greater than 0 when rate limiting is enabled",
                        new[] { "RateLimit.RequestsPerMinute" }));
                }
            }

            return results;
        }
    }
}
