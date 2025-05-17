using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Default implementation of the certificate validator
    /// </summary>
    public class CertificateValidator : ICertificateValidator
    {
        private readonly ILogger<CertificateValidator> _logger;
        private readonly ICertificateRevocationChecker _revocationChecker;
        private readonly ICertificatePinningService _pinningService;
        private readonly TlsOptions _tlsOptions;

        /// <summary>
        /// Creates a new instance of the certificate validator
        /// </summary>
        public CertificateValidator(
            ILogger<CertificateValidator> logger,
            ICertificateRevocationChecker revocationChecker,
            ICertificatePinningService pinningService,
            IOptions<TlsOptions> tlsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _revocationChecker = revocationChecker ?? throw new ArgumentNullException(nameof(revocationChecker));
            _pinningService = pinningService ?? throw new ArgumentNullException(nameof(pinningService));
            _tlsOptions = tlsOptions?.Value ?? throw new ArgumentNullException(nameof(tlsOptions));
        }

        /// <summary>
        /// Validates a certificate chain with enhanced security checks
        /// </summary>
        public bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (certificate == null)
            {
                _logger.LogError("Certificate validation failed: Certificate is null");
                return false;
            }

            // Log certificate details for auditing and debugging
            _logger.LogDebug("Validating certificate: Subject={Subject}, Issuer={Issuer}, Thumbprint={Thumbprint}, NotBefore={NotBefore}, NotAfter={NotAfter}",
                certificate.Subject, certificate.Issuer, certificate.Thumbprint, certificate.NotBefore, certificate.NotAfter);

            // Check certificate pinning first if enabled (highest priority)
            if (_tlsOptions.CertificatePinning.Enabled)
            {
                if (_pinningService.ValidateCertificatePin(certificate))
                {
                    _logger.LogInformation("Certificate pinning validation passed, accepting certificate regardless of other checks");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Certificate pinning validation failed");
                    return false;
                }
            }

            // If we're in development mode and allowing untrusted certificates
            if (_tlsOptions.AllowUntrustedCertificates &&
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning("Allowing untrusted certificate in development mode: {Subject}. This should never be used in production!", certificate.Subject);
                return true;
            }

            // Check certificate validity period
            if (DateTime.UtcNow < certificate.NotBefore)
            {
                _logger.LogWarning("Certificate validation failed: Certificate not yet valid until {ValidDate}", certificate.NotBefore);
                return false;
            }

            if (DateTime.UtcNow > certificate.NotAfter)
            {
                _logger.LogWarning("Certificate validation failed: Certificate expired on {ExpiryDate}", certificate.NotAfter);
                return false;
            }

            // Check if certificate is about to expire
            TimeSpan timeUntilExpiry = certificate.NotAfter - DateTime.UtcNow;
            if (timeUntilExpiry.TotalDays < 30)
            {
                _logger.LogWarning("Certificate will expire in {DaysUntilExpiry} days on {ExpiryDate}",
                    (int)timeUntilExpiry.TotalDays, certificate.NotAfter);
                // Don't fail validation, but log a warning for monitoring
            }

            // Check for SSL policy errors
            if (errors != SslPolicyErrors.None)
            {
                // Log detailed information about the errors
                if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    _logger.LogWarning("Certificate validation failed: Remote certificate not available");
                }

                if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                {
                    _logger.LogWarning("Certificate validation failed: Remote certificate name mismatch");
                }

                if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && chain != null)
                {
                    foreach (var status in chain.ChainStatus)
                    {
                        _logger.LogWarning("Certificate chain error: {Status} - {StatusInformation}",
                            status.Status, status.StatusInformation);
                    }

                    // Check if we're allowing self-signed certificates
                    if (_tlsOptions.AllowSelfSignedCertificates && IsSelfSigned(certificate))
                    {
                        _logger.LogWarning("Allowing self-signed certificate due to configuration");
                    }
                    else
                    {
                        _logger.LogWarning("Certificate validation failed with chain errors");
                        return false;
                    }
                }
                else if (errors != SslPolicyErrors.None)
                {
                    _logger.LogWarning("Certificate validation failed with SSL policy errors: {Errors}", errors);
                    return false;
                }
            }

            // Check certificate revocation if enabled
            if (_tlsOptions.RevocationOptions.CheckRevocation)
            {
                if (!_revocationChecker.ValidateCertificateNotRevoked(certificate))
                {
                    _logger.LogWarning("Certificate validation failed: Certificate is revoked");
                    return false;
                }
            }

            // Check key usage and extended key usage
            if (!ValidateKeyUsage(certificate))
            {
                _logger.LogWarning("Certificate validation failed: Invalid key usage");
                return false;
            }

            _logger.LogDebug("Certificate validated successfully: {Subject}", certificate.Subject);
            return true;
        }

        /// <summary>
        /// Checks if a certificate is self-signed
        /// </summary>
        private bool IsSelfSigned(X509Certificate2 certificate)
        {
            // A certificate is self-signed if the subject and issuer are the same
            return certificate.Subject == certificate.Issuer;
        }

        /// <summary>
        /// Validates the key usage of a certificate
        /// </summary>
        private bool ValidateKeyUsage(X509Certificate2 certificate)
        {
            try
            {
                // For server certificates, check if they have the server authentication EKU
                foreach (var extension in certificate.Extensions)
                {
                    if (extension is X509EnhancedKeyUsageExtension ekuExtension)
                    {
                        foreach (var oid in ekuExtension.EnhancedKeyUsages)
                        {
                            // Server Authentication OID: 1.3.6.1.5.5.7.3.1
                            // Client Authentication OID: 1.3.6.1.5.5.7.3.2
                            if (oid.Value == "1.3.6.1.5.5.7.3.1" || oid.Value == "1.3.6.1.5.5.7.3.2")
                            {
                                return true;
                            }
                        }

                        _logger.LogWarning("Certificate does not have server or client authentication EKU");
                        return false;
                    }
                }

                // If no EKU extension is present, this is acceptable for some certificates
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating certificate key usage");
                return false;
            }
        }

        /// <summary>
        /// Validates a server certificate during client connection
        /// </summary>
        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            try
            {
                _logger.LogDebug("Validating server certificate: {Subject}", certificate.Subject);

                // Convert the certificate to X509Certificate2 for more functionality
                var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

                return ValidateCertificate(cert2, chain, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating server certificate");
                return false;
            }
        }

        /// <summary>
        /// Validates a client certificate during server connection
        /// </summary>
        public bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            try
            {
                _logger.LogDebug("Validating client certificate: {Subject}", certificate.Subject);

                // If client certificates are not required, allow null certificates
                if (certificate == null && !_tlsOptions.RequireClientCertificate)
                {
                    _logger.LogDebug("Client certificate not provided and not required");
                    return true;
                }

                // If client certificates are required but none provided, reject
                if (certificate == null && _tlsOptions.RequireClientCertificate)
                {
                    _logger.LogWarning("Client certificate required but not provided");
                    return false;
                }

                // Convert the certificate to X509Certificate2 for more functionality
                var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

                // Additional client certificate specific validations can be added here
                // For example, check if the certificate is in an allowed list

                return ValidateCertificate(cert2, chain, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating client certificate");
                return false;
            }
        }
    }
}
