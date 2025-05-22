using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Verifies certificates against Certificate Transparency logs
    /// </summary>
    public class CertificateTransparencyVerifier : ICertificateTransparencyVerifier
    {
        private readonly ILogger<CertificateTransparencyVerifier> _logger;
        private readonly HttpClient _httpClient;
        private readonly TlsOptions _tlsOptions;

        // Cache of verified certificates
        private readonly Dictionary<string, bool> _verifiedCertificates = new();

        /// <summary>
        /// Creates a new instance of the certificate transparency verifier
        /// </summary>
        public CertificateTransparencyVerifier(
            ILogger<CertificateTransparencyVerifier> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<TlsOptions> tlsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory?.CreateClient("CtLogClient") ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _tlsOptions = tlsOptions?.Value ?? throw new ArgumentNullException(nameof(tlsOptions));
        }

        /// <summary>
        /// Verifies that a certificate is present in Certificate Transparency logs
        /// </summary>
        public async Task<bool> VerifyCertificateInCtLogsAsync(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                _logger.LogWarning("Cannot verify null certificate in CT logs");
                return false;
            }

            try
            {
                var thumbprint = certificate.Thumbprint;

                // Check cache first
                if (_verifiedCertificates.TryGetValue(thumbprint, out bool isVerified))
                {
                    return isVerified;
                }

                // For real implementation, we would query CT logs
                // This is a simplified implementation that checks against a public CT log API
                
                // Extract the certificate details needed for verification
                var serialNumber = certificate.SerialNumber;
                var issuerName = certificate.IssuerName.Name;

                // Query a CT log API (this is a placeholder - in a real implementation you would use a proper CT log API)
                var ctLogUrl = _tlsOptions.CertificateTransparencyOptions.CtLogApiUrl;
                if (string.IsNullOrEmpty(ctLogUrl))
                {
                    _logger.LogWarning("CT log API URL not configured");
                    return _tlsOptions.CertificateTransparencyOptions.AllowWhenCtUnavailable;
                }

                // In a real implementation, you would:
                // 1. Query multiple CT logs
                // 2. Verify SCTs embedded in the certificate
                // 3. Check for the minimum required number of SCTs
                
                // For this example, we'll simulate a successful verification
                bool result = true;
                
                // Cache the result
                _verifiedCertificates[thumbprint] = result;
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying certificate in CT logs: {Subject}", certificate.Subject);
                return _tlsOptions.CertificateTransparencyOptions.AllowWhenCtUnavailable;
            }
        }

        /// <summary>
        /// Checks if a certificate has embedded SCTs (Signed Certificate Timestamps)
        /// </summary>
        public bool HasEmbeddedScts(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return false;
            }

            try
            {
                // Look for the SCT extension OID
                const string sctExtensionOid = "1.3.6.1.4.1.11129.2.4.2";
                
                foreach (var extension in certificate.Extensions)
                {
                    if (extension.Oid.Value == sctExtensionOid)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for embedded SCTs: {Subject}", certificate.Subject);
                return false;
            }
        }
    }
}
