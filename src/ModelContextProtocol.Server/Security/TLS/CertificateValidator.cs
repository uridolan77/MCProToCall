using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Certificate validator implementation
    /// </summary>
    public class CertificateValidator : ICertificateValidator
    {
        private readonly McpServerOptions _options;
        private readonly ICertificateRevocationChecker _revocationChecker;
        private readonly ICertificatePinningService _pinningService;
        private readonly ILogger<CertificateValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateValidator"/> class
        /// </summary>
        /// <param name="options">Server options</param>
        /// <param name="revocationChecker">Revocation checker</param>
        /// <param name="pinningService">Certificate pinning service</param>
        /// <param name="logger">Logger</param>
        public CertificateValidator(
            IOptions<McpServerOptions> options,
            ICertificateRevocationChecker revocationChecker,
            ICertificatePinningService pinningService,
            ILogger<CertificateValidator> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _revocationChecker = revocationChecker ?? throw new ArgumentNullException(nameof(revocationChecker));
            _pinningService = pinningService ?? throw new ArgumentNullException(nameof(pinningService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateCertificateAsync(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                _logger.LogWarning("Certificate is null");
                return false;
            }

            // Check if the certificate is in the allowed list
            if (_options.AllowedClientCertificateThumbprints.Count > 0)
            {
                var thumbprint = certificate.Thumbprint;
                if (!_options.AllowedClientCertificateThumbprints.Contains(thumbprint))
                {
                    _logger.LogWarning("Certificate thumbprint {Thumbprint} is not in the allowed list", thumbprint);
                    return false;
                }
            }

            // Check if the certificate is expired
            if (DateTime.Now > certificate.NotAfter || DateTime.Now < certificate.NotBefore)
            {
                _logger.LogWarning("Certificate is expired or not yet valid");
                return false;
            }

            // Check if the certificate is revoked
            if (_options.CheckCertificateRevocation)
            {
                var isRevoked = await _revocationChecker.IsRevokedAsync(certificate);
                if (isRevoked)
                {
                    _logger.LogWarning("Certificate is revoked");
                    return false;
                }
            }

            // Check certificate pinning
            if (_options.EnableCertificatePinning)
            {
                var isPinned = await _pinningService.ValidatePinAsync(certificate);
                if (!isPinned)
                {
                    _logger.LogWarning("Certificate does not match pinned certificate");
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateCertificateChainAsync(X509Chain chain)
        {
            if (chain == null)
            {
                _logger.LogWarning("Certificate chain is null");
                return false;
            }

            // Check each certificate in the chain
            foreach (var element in chain.ChainElements)
            {
                var certificate = element.Certificate;

                // Check if the certificate is expired
                if (DateTime.Now > certificate.NotAfter || DateTime.Now < certificate.NotBefore)
                {
                    _logger.LogWarning("Certificate in chain is expired or not yet valid: {Subject}", certificate.Subject);
                    return false;
                }

                // Check if the certificate is revoked
                if (_options.CheckCertificateRevocation)
                {
                    var isRevoked = await _revocationChecker.IsRevokedAsync(certificate);
                    if (isRevoked)
                    {
                        _logger.LogWarning("Certificate in chain is revoked: {Subject}", certificate.Subject);
                        return false;
                    }
                }
            }

            // Check chain status
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.NoError)
                {
                    // Allow untrusted root if configured
                    if (status.Status == X509ChainStatusFlags.UntrustedRoot && _options.AllowUntrustedCertificates)
                    {
                        _logger.LogWarning("Allowing untrusted root certificate");
                        continue;
                    }

                    _logger.LogWarning("Certificate chain validation failed: {Status}", status.StatusInformation);
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public bool ValidateClientCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            try
            {
                // Convert to X509Certificate2
                var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

                // Log validation attempt
                _logger.LogDebug("Validating client certificate: {Subject}, {Thumbprint}",
                    cert2.Subject, cert2.Thumbprint);

                // Check for policy errors
                if (sslPolicyErrors != SslPolicyErrors.None)
                {
                    // Allow untrusted root if configured
                    if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors &&
                        _options.AllowUntrustedCertificates)
                    {
                        _logger.LogWarning("Allowing certificate with chain errors due to configuration");
                    }
                    else
                    {
                        _logger.LogWarning("Certificate validation failed with policy errors: {Errors}", sslPolicyErrors);
                        return false;
                    }
                }

                // Check if the certificate is in the allowed list
                if (_options.AllowedClientCertificateThumbprints.Count > 0)
                {
                    var thumbprint = cert2.Thumbprint;
                    if (!_options.AllowedClientCertificateThumbprints.Contains(thumbprint))
                    {
                        _logger.LogWarning("Certificate thumbprint {Thumbprint} is not in the allowed list", thumbprint);
                        return false;
                    }
                }

                // Check if the certificate is expired
                if (DateTime.Now > cert2.NotAfter || DateTime.Now < cert2.NotBefore)
                {
                    _logger.LogWarning("Certificate is expired or not yet valid");
                    return false;
                }

                // For other checks that require async, we'll have to do them elsewhere
                // This method is called during the TLS handshake and must be synchronous

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating client certificate");
                return false;
            }
        }
    }
}
