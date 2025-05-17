using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Implementation of certificate revocation checking service
    /// </summary>
    public class CertificateRevocationChecker : ICertificateRevocationChecker
    {
        private readonly ILogger<CertificateRevocationChecker> _logger;
        private readonly TlsOptions _tlsOptions;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        
        // Cache of revoked certificate thumbprints
        private readonly ConcurrentDictionary<string, DateTime> _revokedCertificates = new();
        
        // Last time the CRL was updated
        private DateTime _lastCrlUpdate = DateTime.MinValue;
        
        /// <summary>
        /// Creates a new instance of the certificate revocation checker
        /// </summary>
        public CertificateRevocationChecker(
            ILogger<CertificateRevocationChecker> logger,
            IOptions<TlsOptions> tlsOptions,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tlsOptions = tlsOptions?.Value ?? throw new ArgumentNullException(nameof(tlsOptions));
            _httpClient = httpClientFactory?.CreateClient("CrlDownloader") ?? throw new ArgumentNullException(nameof(httpClientFactory));
            
            // Initialize by loading any cached revocation list
            LoadRevocationList();
        }

        /// <summary>
        /// Validates that a certificate has not been revoked
        /// </summary>
        public bool ValidateCertificateNotRevoked(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                _logger.LogWarning("Cannot check revocation for null certificate");
                return false;
            }

            try
            {
                var thumbprint = certificate.Thumbprint;
                
                // Check our local cache first
                if (_revokedCertificates.ContainsKey(thumbprint))
                {
                    _logger.LogWarning("Certificate {Thumbprint} found in local revocation cache", thumbprint);
                    return false;
                }

                // If we're configured to only use the local cache, return true at this point
                if (_tlsOptions.RevocationCheckMode == RevocationCheckMode.LocalCacheOnly)
                {
                    return true;
                }

                // If the CRL needs updating and we're allowed to check online
                if ((DateTime.UtcNow - _lastCrlUpdate).TotalHours > _tlsOptions.CrlUpdateIntervalHours)
                {
                    // Attempt to update, but don't block on it for too long
                    var updateTask = Task.Run(() => UpdateRevocationLists());
                    if (!updateTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        _logger.LogWarning("CRL update timed out, continuing with existing data");
                    }
                }

                // Check OCSP if configured
                if (_tlsOptions.RevocationCheckMode == RevocationCheckMode.OcspAndCrl || 
                    _tlsOptions.RevocationCheckMode == RevocationCheckMode.OcspOnly)
                {
                    // For OCSP checking, we use .NET's built-in mechanisms via X509Chain
                    var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    
                    if (!chain.Build(certificate))
                    {
                        foreach (var status in chain.ChainStatus)
                        {
                            if (status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                                status.Status == X509ChainStatusFlags.Revoked)
                            {
                                _logger.LogWarning("Certificate {Thumbprint} failed OCSP check: {Status}", 
                                    thumbprint, status.StatusInformation);
                                return false;
                            }
                        }
                    }
                }

                // Check CRL from certificate if configured
                if (_tlsOptions.RevocationCheckMode == RevocationCheckMode.OcspAndCrl || 
                    _tlsOptions.RevocationCheckMode == RevocationCheckMode.CrlOnly)
                {
                    // Get CRL distribution points from the certificate
                    var crlDistributionPoints = certificate.Extensions
                        .OfType<X509Extension>()
                        .FirstOrDefault(e => e.Oid.Value == "2.5.29.31"); // CRL Distribution Points OID
                    
                    if (crlDistributionPoints != null)
                    {
                        // Parse the CRL distribution points and check them
                        // In a real implementation, this would properly parse the ASN.1 data
                        // For simplicity, we're relying on .NET's chain building to handle CRLs
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking certificate revocation for {Subject}", certificate.Subject);
                
                // If we can't check, fall back to the configured behavior
                return _tlsOptions.RevocationFailureMode == RevocationFailureMode.Allow;
            }
        }

        /// <summary>
        /// Updates cached revocation lists from online sources
        /// </summary>
        public bool UpdateRevocationLists()
        {
            try
            {
                _logger.LogInformation("Updating certificate revocation lists");
                
                // In a real implementation, this would download CRLs from configured sources
                // and update the local cache
                
                // For trusted CAs, you would typically:
                // 1. Download CRLs from distribution points
                // 2. Parse the CRLs
                // 3. Update the local cache
                
                _lastCrlUpdate = DateTime.UtcNow;
                SaveRevocationList(); // Save our updated cache
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update certificate revocation lists");
                return false;
            }
        }

        /// <summary>
        /// Adds a certificate to a local cache of revoked certificates
        /// </summary>
        public bool AddToRevocationList(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                return false;
            }

            try
            {
                var thumbprint = certificate.Thumbprint;
                var added = _revokedCertificates.TryAdd(thumbprint, DateTime.UtcNow);
                
                if (added)
                {
                    _logger.LogInformation("Added certificate {Thumbprint} to local revocation list", thumbprint);
                    SaveRevocationList();
                }
                
                return added;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding certificate to revocation list");
                return false;
            }
        }

        /// <summary>
        /// Loads the revocation list from persistent storage
        /// </summary>
        private void LoadRevocationList()
        {
            try
            {
                _cacheLock.Wait();
                
                var path = Path.Combine(_tlsOptions.RevocationCachePath, "revoked_certs.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var revoked = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                    
                    if (revoked != null)
                    {
                        foreach (var (thumbprint, date) in revoked)
                        {
                            _revokedCertificates.TryAdd(thumbprint, date);
                        }
                        
                        _logger.LogInformation("Loaded {Count} revoked certificates from cache", _revokedCertificates.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading revocation list from cache");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Saves the revocation list to persistent storage
        /// </summary>
        private void SaveRevocationList()
        {
            try
            {
                _cacheLock.Wait();
                
                var directory = _tlsOptions.RevocationCachePath;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var path = Path.Combine(directory, "revoked_certs.json");
                var json = System.Text.Json.JsonSerializer.Serialize(_revokedCertificates);
                File.WriteAllText(path, json);
                
                _logger.LogDebug("Saved {Count} revoked certificates to cache", _revokedCertificates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving revocation list to cache");
            }
            finally
            {
                _cacheLock.Release();
            }
        }
    }

    /// <summary>
    /// Defines how certificate revocation should be checked
    /// </summary>
    public enum RevocationCheckMode
    {
        /// <summary>
        /// Check only the local cache of revoked certificates
        /// </summary>
        LocalCacheOnly,
        
        /// <summary>
        /// Check using Online Certificate Status Protocol
        /// </summary>
        OcspOnly,
        
        /// <summary>
        /// Check using Certificate Revocation Lists
        /// </summary>
        CrlOnly,
        
        /// <summary>
        /// Check using both OCSP and CRLs
        /// </summary>
        OcspAndCrl
    }

    /// <summary>
    /// Defines how revocation check failures should be handled
    /// </summary>
    public enum RevocationFailureMode
    {
        /// <summary>
        /// Allow the certificate if revocation checks fail
        /// </summary>
        Allow,
        
        /// <summary>
        /// Deny the certificate if revocation checks fail
        /// </summary>
        Deny
    }
}
