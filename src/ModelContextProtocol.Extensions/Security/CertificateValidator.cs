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
        /// Validates a certificate chain
        /// </summary>
        public bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (certificate == null)
            {
                _logger.LogWarning("Certificate validation failed: Certificate is null");
                return false;
            }

            // If we're in development mode
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                _logger.LogWarning("Allowing untrusted certificate in development mode: {Subject}", certificate.Subject);
                return true;
            }

            // Check for SSL policy errors
            if (errors != SslPolicyErrors.None)
            {
                _logger.LogWarning("Certificate validation failed with SSL policy errors: {Errors}", errors);
                return false;
            }

            // Check certificate revocation if enabled
            if (_tlsOptions.RevocationOptions.CheckRevocation && !_revocationChecker.ValidateCertificateNotRevoked(certificate))
            {
                _logger.LogWarning("Certificate validation failed: Certificate is revoked");
                return false;
            }

            // Check certificate pinning if enabled
            if (_tlsOptions.CertificatePinning.Enabled && !_pinningService.ValidateCertificatePin(certificate))
            {
                _logger.LogWarning("Certificate validation failed: Certificate pin validation failed");
                return false;
            }

            // Additional custom validations can be added here
            // For example, check expiration date, issuer, subject, etc.
            if (certificate.NotAfter < DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate validation failed: Certificate expired on {ExpiryDate}", certificate.NotAfter);
                return false;
            }

            if (certificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate validation failed: Certificate not yet valid until {ValidDate}", certificate.NotBefore);
                return false;
            }

            _logger.LogDebug("Certificate validated successfully: {Subject}", certificate.Subject);
            return true;
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
