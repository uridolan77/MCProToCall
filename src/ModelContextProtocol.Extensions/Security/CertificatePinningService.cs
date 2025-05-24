using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Implementation of certificate pinning service
    /// </summary>
    public class CertificatePinningService : ICertificatePinningService
    {
        private readonly ILogger<CertificatePinningService> _logger;
        private readonly TlsOptions _tlsOptions;
        private readonly SemaphoreSlim _pinLock = new(1, 1);

        // Cache of pinned certificate thumbprints
        private readonly ConcurrentDictionary<string, PinnedCertificateInfo> _pinnedCertificates = new();

        /// <summary>
        /// Creates a new instance of the certificate pinning service
        /// </summary>
        public CertificatePinningService(
            ILogger<CertificatePinningService> logger,
            IOptions<TlsOptions> tlsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tlsOptions = tlsOptions?.Value ?? throw new ArgumentNullException(nameof(tlsOptions));

            // Initialize by loading any configured pins
            LoadPinnedCertificatesInternal();
        }

        /// <summary>
        /// Validates a certificate against pinned certificates or public keys
        /// </summary>
        public bool ValidateCertificatePin(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                _logger.LogWarning("Cannot validate pin for null certificate");
                return false;
            }

            try
            {
                // If certificate pinning is disabled, always return true
                if (!_tlsOptions.CertificatePinning.Enabled)
                {
                    return true;
                }

                var thumbprint = certificate.Thumbprint;

                // First try direct thumbprint match (most secure)
                if (_pinnedCertificates.TryGetValue(thumbprint, out var pinInfo))
                {
                    _logger.LogDebug("Certificate {Thumbprint} matched pinned certificate", thumbprint);
                    return true;
                }

                // If we didn't find a match, fail
                _logger.LogWarning("Certificate {Thumbprint} did not match any pinned certificates",
                    thumbprint);
                return false;

                // If we get here, try to match by public key or issuer chain
                // This allows for certificate rotation while maintaining the same keys

                // Extract the public key
                var publicKey = certificate.PublicKey.EncodedKeyValue.RawData;

                // Check if any pins match this public key
                var matchByPublicKey = _pinnedCertificates.Values
                    .Where(p => p.PublicKeyHash != null)
                    .Any(p => CompareByteArrays(ComputeHash(publicKey), p.PublicKeyHash));

                if (matchByPublicKey)
                {
                    _logger.LogDebug("Certificate {Thumbprint} matched by public key", thumbprint);
                    return true;
                }

                // If no pinned certificates match and pinning is enforced, fail
                _logger.LogWarning("Certificate {Thumbprint} did not match any pinned certificates or public keys",
                    thumbprint);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating certificate pin for {Subject}", certificate.Subject);

                // In case of errors, fail closed
                return false;
            }
        }

        /// <summary>
        /// Adds a certificate to the pin list
        /// </summary>
        public bool AddCertificatePin(X509Certificate2 certificate, bool isPermanent = false)
        {
            if (certificate == null)
            {
                return false;
            }

            try
            {
                var thumbprint = certificate.Thumbprint;
                var publicKeyHash = ComputeHash(certificate.PublicKey.EncodedKeyValue.RawData);

                var pinnedInfo = new PinnedCertificateInfo
                {
                    Thumbprint = thumbprint,
                    SubjectName = certificate.Subject,
                    PublicKeyHash = publicKeyHash,
                    IssuerName = certificate.Issuer,
                    PinnedDate = DateTime.UtcNow,
                    IsPermanent = isPermanent
                };

                var added = _pinnedCertificates.TryAdd(thumbprint, pinnedInfo);

                if (added)
                {
                    _logger.LogInformation("Added certificate {Thumbprint} to pinned certificates list", thumbprint);

                    if (isPermanent)
                    {
                        SavePinnedCertificatesInternal();
                    }
                }

                return added;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding certificate to pinned list");
                return false;
            }
        }

        /// <summary>
        /// Removes a certificate from the pin list
        /// </summary>
        public bool RemoveCertificatePin(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                return false;
            }

            try
            {
                var removed = _pinnedCertificates.TryRemove(thumbprint, out _);

                if (removed)
                {
                    _logger.LogInformation("Removed certificate {Thumbprint} from pinned certificates list", thumbprint);
                    SavePinnedCertificatesInternal();
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing certificate from pinned list");
                return false;
            }
        }

        /// <summary>
        /// Checks if a certificate is pinned
        /// </summary>
        public bool IsCertificatePinned(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                return false;
            }

            return _pinnedCertificates.ContainsKey(thumbprint);
        }

        /// <summary>
        /// Validates a certificate pin asynchronously
        /// </summary>
        public async Task<bool> ValidatePinAsync(X509Certificate2 certificate)
        {
            return await Task.FromResult(ValidateCertificatePin(certificate));
        }

        /// <summary>
        /// Pins a certificate asynchronously
        /// </summary>
        public async Task<bool> PinCertificateAsync(X509Certificate2 certificate)
        {
            return await Task.FromResult(AddCertificatePin(certificate, true));
        }

        /// <summary>
        /// Loads pinned certificates from storage
        /// </summary>
        public bool LoadPinnedCertificates()
        {
            try
            {
                LoadPinnedCertificatesInternal();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Saves pinned certificates to storage
        /// </summary>
        public bool SavePinnedCertificates()
        {
            try
            {
                SavePinnedCertificatesInternal();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pins a certificate (interface implementation)
        /// </summary>
        public bool PinCertificate(X509Certificate2 certificate)
        {
            return AddCertificatePin(certificate, true);
        }

        /// <summary>
        /// Unpins a certificate (interface implementation)
        /// </summary>
        public bool UnpinCertificate(X509Certificate2 certificate)
        {
            if (certificate == null) return false;
            return RemoveCertificatePin(certificate.Thumbprint);
        }

        /// <summary>
        /// Computes a SHA-256 hash of the input data
        /// </summary>
        private byte[] ComputeHash(byte[] data)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return sha256.ComputeHash(data);
        }

        /// <summary>
        /// Compares two byte arrays for equality
        /// </summary>
        private bool CompareByteArrays(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            return a.SequenceEqual(b);
        }

        /// <summary>
        /// Loads pinned certificates from persistent storage
        /// </summary>
        private void LoadPinnedCertificatesInternal()
        {
            try
            {
                _pinLock.Wait();

                // First add any certificates configured in the options
                if (_tlsOptions.CertificatePinning.PinnedCertificates != null)
                {
                    foreach (var certPath in _tlsOptions.CertificatePinning.PinnedCertificates)
                    {
                        try
                        {
                            var certificate = CertificateHelper.LoadCertificateFromFile(certPath, string.Empty, _logger);
                            if (certificate != null)
                            {
                                AddCertificatePin(certificate, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to load pinned certificate from {Path}", certPath);
                        }
                    }
                }

                // Now load any saved pins from storage
                var path = Path.Combine(_tlsOptions.CertificatePinning.PinnedCertificatesPath, "pinned_certs.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var pinned = System.Text.Json.JsonSerializer.Deserialize<List<PinnedCertificateInfo>>(json);

                    if (pinned != null)
                    {
                        foreach (var pin in pinned)
                        {
                            if (!string.IsNullOrEmpty(pin.Thumbprint))
                            {
                                _pinnedCertificates.TryAdd(pin.Thumbprint, pin);
                            }
                        }

                        _logger.LogInformation("Loaded {Count} pinned certificates from storage", _pinnedCertificates.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pinned certificates");
            }
            finally
            {
                _pinLock.Release();
            }
        }

        /// <summary>
        /// Saves pinned certificates to persistent storage
        /// </summary>
        private void SavePinnedCertificatesInternal()
        {
            try
            {
                _pinLock.Wait();

                // Only save permanent pins
                var permanentPins = _pinnedCertificates.Values
                    .Where(p => p.IsPermanent)
                    .ToList();

                var directory = _tlsOptions.CertificatePinning.PinnedCertificatesPath;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var path = Path.Combine(directory, "pinned_certs.json");
                var json = System.Text.Json.JsonSerializer.Serialize(permanentPins);
                File.WriteAllText(path, json);

                _logger.LogDebug("Saved {Count} pinned certificates to storage", permanentPins.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pinned certificates");
            }
            finally
            {
                _pinLock.Release();
            }
        }
    }

    /// <summary>
    /// Information about a pinned certificate
    /// </summary>
    public class PinnedCertificateInfo
    {
        /// <summary>
        /// The certificate thumbprint
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// The subject name of the certificate
        /// </summary>
        public string SubjectName { get; set; }

        /// <summary>
        /// The issuer name of the certificate
        /// </summary>
        public string IssuerName { get; set; }

        /// <summary>
        /// The hash of the public key
        /// </summary>
        public byte[] PublicKeyHash { get; set; }

        /// <summary>
        /// When the certificate was pinned
        /// </summary>
        public DateTime PinnedDate { get; set; }

        /// <summary>
        /// Whether the pin is permanent
        /// </summary>
        public bool IsPermanent { get; set; }
    }
}
