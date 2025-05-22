using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Certificate pinning service implementation
    /// </summary>
    public class CertificatePinningService : ICertificatePinningService
    {
        private readonly McpServerOptions _options;
        private readonly ILogger<CertificatePinningService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificatePinningService"/> class
        /// </summary>
        /// <param name="options">Server options</param>
        /// <param name="logger">Logger</param>
        public CertificatePinningService(
            IOptions<McpServerOptions> options,
            ILogger<CertificatePinningService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task AddPinnedCertificateAsync(X509Certificate2 certificate)
        {
            try
            {
                var pinPath = GetPinPath(certificate.Thumbprint);
                var pinDirectory = Path.GetDirectoryName(pinPath);

                if (!Directory.Exists(pinDirectory))
                {
                    Directory.CreateDirectory(pinDirectory);
                }

                // Export the certificate public key
                var publicKey = certificate.GetPublicKey();

                // Calculate the hash of the public key
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(publicKey);

                // Save the hash to the pin file
                await File.WriteAllBytesAsync(pinPath, hash);

                _logger.LogInformation("Pinned certificate with thumbprint {Thumbprint}", certificate.Thumbprint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning certificate");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task RemovePinnedCertificateAsync(string thumbprint)
        {
            try
            {
                var pinPath = GetPinPath(thumbprint);

                if (File.Exists(pinPath))
                {
                    File.Delete(pinPath);
                    _logger.LogInformation("Unpinned certificate with thumbprint {Thumbprint}", thumbprint);
                }
                else
                {
                    _logger.LogWarning("No pin found for certificate with thumbprint {Thumbprint}", thumbprint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning certificate");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsCertificatePinnedAsync(X509Certificate2 certificate)
        {
            try
            {
                var pinPath = GetPinPath(certificate.Thumbprint);

                if (!File.Exists(pinPath))
                {
                    _logger.LogDebug("No pin found for certificate with thumbprint {Thumbprint}",
                        certificate.Thumbprint);
                    return false;
                }

                // Read the pinned hash
                var pinnedHash = await File.ReadAllBytesAsync(pinPath);

                // Calculate the hash of the certificate public key
                var publicKey = certificate.GetPublicKey();
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(publicKey);

                // Compare the hashes
                if (hash.Length != pinnedHash.Length)
                {
                    return false;
                }

                for (int i = 0; i < hash.Length; i++)
                {
                    if (hash[i] != pinnedHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if certificate is pinned");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<X509Certificate2[]> GetPinnedCertificatesAsync()
        {
            try
            {
                var pinDirectory = _options.CertificatePinStoragePath ?? "certs/pins";

                if (!Directory.Exists(pinDirectory))
                {
                    return Array.Empty<X509Certificate2>();
                }

                var pinFiles = Directory.GetFiles(pinDirectory, "*.pin");
                var certificates = new List<X509Certificate2>();

                foreach (var pinFile in pinFiles)
                {
                    var thumbprint = Path.GetFileNameWithoutExtension(pinFile);

                    // Try to find the certificate in the certificate store
                    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);

                    var cert = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                    if (cert.Count > 0)
                    {
                        certificates.Add(cert[0]);
                    }
                }

                return certificates.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned certificates");
                return Array.Empty<X509Certificate2>();
            }
        }

        /// <inheritdoc/>
        public void AddCertificatePin(X509Certificate2 certificate, bool isPermanent)
        {
            try
            {
                var pinPath = GetPinPath(certificate.Thumbprint);
                var pinDirectory = Path.GetDirectoryName(pinPath);

                if (!Directory.Exists(pinDirectory))
                {
                    Directory.CreateDirectory(pinDirectory);
                }

                // Export the certificate public key
                var publicKey = certificate.GetPublicKey();

                // Calculate the hash of the public key
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(publicKey);

                // Save the hash to the pin file
                File.WriteAllBytes(pinPath, hash);

                _logger.LogInformation("Added certificate pin for thumbprint {Thumbprint}, isPermanent: {IsPermanent}",
                    certificate.Thumbprint, isPermanent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding certificate pin");
                throw;
            }
        }

        /// <summary>
        /// Validates a certificate against the pinned certificate
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if the certificate matches the pinned certificate, false otherwise</returns>
        public async Task<bool> ValidatePinAsync(X509Certificate2 certificate)
        {
            try
            {
                var pinPath = GetPinPath(certificate.Thumbprint);

                if (!File.Exists(pinPath))
                {
                    _logger.LogWarning("No pin found for certificate with thumbprint {Thumbprint}",
                        certificate.Thumbprint);

                    // If pinning is enabled but no pin exists, pin the certificate
                    if (_options.EnableCertificatePinning && _options.AutoPinFirstCertificate)
                    {
                        await AddPinnedCertificateAsync(certificate);
                        return true;
                    }

                    return false;
                }

                return await IsCertificatePinnedAsync(certificate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating certificate pin");
                return false;
            }
        }

        private string GetPinPath(string thumbprint)
        {
            var pinDirectory = _options.CertificatePinStoragePath ?? "certs/pins";
            return Path.Combine(pinDirectory, $"{thumbprint}.pin");
        }
    }
}
