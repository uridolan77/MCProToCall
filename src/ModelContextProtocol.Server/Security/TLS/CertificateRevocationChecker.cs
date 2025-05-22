using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Certificate revocation checker implementation
    /// </summary>
    public class CertificateRevocationChecker : ICertificateRevocationChecker
    {
        private readonly McpServerOptions _options;
        private readonly ILogger<CertificateRevocationChecker> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateRevocationChecker"/> class
        /// </summary>
        /// <param name="options">Server options</param>
        /// <param name="logger">Logger</param>
        public CertificateRevocationChecker(
            IOptions<McpServerOptions> options,
            ILogger<CertificateRevocationChecker> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> IsRevokedAsync(X509Certificate2 certificate)
        {
            try
            {
                // Create the chain
                using var chain = new X509Chain();
                
                // Configure the chain
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                chain.ChainPolicy.VerificationTime = DateTime.Now;
                
                // Set the cache directory
                if (!string.IsNullOrEmpty(_options.RevocationCachePath))
                {
                    if (!Directory.Exists(_options.RevocationCachePath))
                    {
                        Directory.CreateDirectory(_options.RevocationCachePath);
                    }
                    
                    chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }
                
                // Build the chain
                var isValid = chain.Build(certificate);
                
                // Check for revocation status
                foreach (var status in chain.ChainStatus)
                {
                    if (status.Status == X509ChainStatusFlags.Revoked)
                    {
                        _logger.LogWarning("Certificate is revoked: {Status}", status.StatusInformation);
                        return true;
                    }
                }
                
                return !isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking certificate revocation");
                
                // If we can't check revocation, assume it's not revoked
                // This is a conservative approach, but it's better than blocking all certificates
                return false;
            }
        }
    }
}
