using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Configuration options for Certificate Transparency validation
    /// </summary>
    public class CertificateTransparencyOptions
    {
        /// <summary>
        /// Whether to enable Certificate Transparency validation
        /// </summary>
        public bool EnableCertificateTransparency { get; set; } = true;

        /// <summary>
        /// Whether to verify certificate transparency (alias for EnableCertificateTransparency)
        /// </summary>
        public bool VerifyCertificateTransparency
        {
            get => EnableCertificateTransparency;
            set => EnableCertificateTransparency = value;
        }

        /// <summary>
        /// Whether to require embedded SCTs
        /// </summary>
        public bool RequireEmbeddedScts { get; set; } = true;

        /// <summary>
        /// Whether to allow certificates when CT validation fails
        /// </summary>
        public bool AllowWhenCtUnavailable { get; set; } = true;

        /// <summary>
        /// Timeout for CT log queries in seconds
        /// </summary>
        public int CtQueryTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// CT log API URL
        /// </summary>
        public string CtLogApiUrl { get; set; } = "https://ct.googleapis.com/logs/";

        /// <summary>
        /// Certificate Transparency log URLs to check
        /// </summary>
        public List<string> CtLogUrls { get; set; } = new List<string>
        {
            "https://ct.googleapis.com/logs/argon2024/",
            "https://ct.googleapis.com/logs/xenon2024/",
            "https://yeti2024.ct.digicert.com/log/",
            "https://oak.ct.letsencrypt.org/2024h1/"
        };

        /// <summary>
        /// Minimum number of CT logs that must contain the certificate
        /// </summary>
        public int MinimumCtLogCount { get; set; } = 2;

        /// <summary>
        /// Minimum number of SCTs required
        /// </summary>
        public int MinimumSctCount { get; set; } = 2;

        /// <summary>
        /// Trusted CT logs
        /// </summary>
        public List<string> TrustedCtLogs { get; set; } = new List<string>
        {
            "ct.googleapis.com/logs/",
            "ct.cloudflare.com/logs/"
        };
    }

    /// <summary>
    /// Validates certificates against Certificate Transparency logs
    /// </summary>
    public class CertificateTransparencyValidator
    {
        private readonly ILogger<CertificateTransparencyValidator> _logger;
        private readonly CertificateTransparencyOptions _options;
        private readonly HttpClient _httpClient;

        public CertificateTransparencyValidator(
            ILogger<CertificateTransparencyValidator> logger,
            IOptions<CertificateTransparencyOptions> options,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient.Timeout = TimeSpan.FromSeconds(_options.CtQueryTimeoutSeconds);
        }

        /// <summary>
        /// Validates that a certificate is present in Certificate Transparency logs
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the certificate is valid according to CT requirements</returns>
        public async Task<bool> ValidateCertificateTransparencyAsync(
            X509Certificate2 certificate,
            CancellationToken cancellationToken = default)
        {
            if (!_options.EnableCertificateTransparency)
            {
                _logger.LogDebug("Certificate Transparency validation is disabled");
                return true;
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            try
            {
                _logger.LogInformation("Validating certificate transparency for certificate: {Subject}",
                    certificate.Subject);

                // Check for SCT (Signed Certificate Timestamp) extensions in the certificate
                var sctExtension = GetSctExtension(certificate);
                if (sctExtension != null)
                {
                    _logger.LogDebug("Certificate contains SCT extension, analyzing embedded timestamps");
                    var sctCount = CountSignedCertificateTimestamps(sctExtension);

                    if (sctCount >= _options.MinimumCtLogCount)
                    {
                        _logger.LogInformation("Certificate contains {SctCount} SCTs, meeting minimum requirement of {MinCount}",
                            sctCount, _options.MinimumCtLogCount);
                        return true;
                    }
                }

                // Query CT logs directly if SCT extension validation isn't sufficient
                var foundInLogs = await QueryCertificateTransparencyLogsAsync(certificate, cancellationToken);

                if (foundInLogs >= _options.MinimumCtLogCount)
                {
                    _logger.LogInformation("Certificate found in {LogCount} CT logs, meeting minimum requirement of {MinCount}",
                        foundInLogs, _options.MinimumCtLogCount);
                    return true;
                }

                _logger.LogWarning("Certificate found in only {LogCount} CT logs, below minimum requirement of {MinCount}",
                    foundInLogs, _options.MinimumCtLogCount);

                return _options.AllowWhenCtUnavailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating certificate transparency");
                return _options.AllowWhenCtUnavailable;
            }
        }

        private X509Extension GetSctExtension(X509Certificate2 certificate)
        {
            // SCT extension OID: 1.3.6.1.4.1.11129.2.4.2
            const string sctOid = "1.3.6.1.4.1.11129.2.4.2";

            return certificate.Extensions.Cast<X509Extension>()
                .FirstOrDefault(ext => ext.Oid?.Value == sctOid);
        }

        private int CountSignedCertificateTimestamps(X509Extension sctExtension)
        {
            try
            {
                // This is a simplified implementation
                // In a production environment, you'd need to properly parse the SCT extension
                // according to RFC 6962
                var data = sctExtension.RawData;

                // Each SCT is approximately 70-80 bytes
                // This is a rough estimate
                return Math.Max(1, data.Length / 75);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing SCT extension");
                return 0;
            }
        }

        private async Task<int> QueryCertificateTransparencyLogsAsync(
            X509Certificate2 certificate,
            CancellationToken cancellationToken)
        {
            var foundCount = 0;
            var certificateHash = Convert.ToBase64String(certificate.GetCertHash());

            var tasks = _options.CtLogUrls.Select(async logUrl =>
            {
                try
                {
                    using var response = await _httpClient.GetAsync(
                        $"{logUrl.TrimEnd('/')}/ct/v1/get-entries?start=0&end=1000",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);

                        // This is a simplified check - in production you'd need to
                        // properly search the CT log entries
                        if (content.Contains(certificateHash))
                        {
                            _logger.LogDebug("Certificate found in CT log: {LogUrl}", logUrl);
                            return 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error querying CT log {LogUrl}", logUrl);
                }

                return 0;
            });

            var results = await Task.WhenAll(tasks);
            foundCount = results.Sum();

            return foundCount;
        }
    }
}
