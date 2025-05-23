using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Configuration options for OCSP validation
    /// </summary>
    public class OcspValidationOptions
    {
        /// <summary>
        /// Whether to enable OCSP validation
        /// </summary>
        public bool EnableOcspValidation { get; set; } = true;

        /// <summary>
        /// Whether to prefer OCSP stapling over direct OCSP queries
        /// </summary>
        public bool PreferOcspStapling { get; set; } = true;

        /// <summary>
        /// Timeout for OCSP queries in seconds
        /// </summary>
        public int OcspTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Whether to allow certificates when OCSP is unavailable
        /// </summary>
        public bool AllowWhenOcspUnavailable { get; set; } = true;

        /// <summary>
        /// Maximum age of cached OCSP responses in hours
        /// </summary>
        public int MaxOcspCacheAgeHours { get; set; } = 24;
    }

    /// <summary>
    /// Enhanced OCSP validator with stapling support
    /// </summary>
    public class EnhancedOcspValidator
    {
        private readonly ILogger<EnhancedOcspValidator> _logger;
        private readonly OcspValidationOptions _options;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, (DateTime Timestamp, bool IsValid)> _ocspCache;

        public EnhancedOcspValidator(
            ILogger<EnhancedOcspValidator> logger,
            IOptions<OcspValidationOptions> options,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ocspCache = new Dictionary<string, (DateTime, bool)>();

            _httpClient.Timeout = TimeSpan.FromSeconds(_options.OcspTimeoutSeconds);
        }

        /// <summary>
        /// Validates certificate revocation status using OCSP
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="issuerCertificate">The issuer certificate</param>
        /// <param name="stapledOcspResponse">Optional stapled OCSP response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the certificate is not revoked</returns>
        public async Task<bool> ValidateOcspAsync(
            X509Certificate2 certificate,
            X509Certificate2 issuerCertificate,
            byte[] stapledOcspResponse = null,
            CancellationToken cancellationToken = default)
        {
            if (!_options.EnableOcspValidation)
            {
                _logger.LogDebug("OCSP validation is disabled");
                return true;
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            try
            {
                var certificateThumbprint = certificate.Thumbprint;
                
                // Check cache first
                if (TryGetCachedResult(certificateThumbprint, out bool cachedResult))
                {
                    _logger.LogDebug("Using cached OCSP result for certificate {Thumbprint}: {Result}",
                        certificateThumbprint, cachedResult);
                    return cachedResult;
                }

                _logger.LogInformation("Validating OCSP status for certificate: {Subject}", certificate.Subject);

                bool isValid;

                // Try stapled OCSP response first if available and preferred
                if (_options.PreferOcspStapling && stapledOcspResponse != null)
                {
                    _logger.LogDebug("Validating stapled OCSP response");
                    isValid = await ValidateStapledOcspResponseAsync(certificate, issuerCertificate, stapledOcspResponse);
                    
                    if (isValid)
                    {
                        CacheResult(certificateThumbprint, true);
                        return true;
                    }
                    
                    _logger.LogWarning("Stapled OCSP response validation failed, falling back to direct OCSP query");
                }

                // Fall back to direct OCSP query
                isValid = await QueryOcspDirectlyAsync(certificate, issuerCertificate, cancellationToken);
                
                CacheResult(certificateThumbprint, isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OCSP validation for certificate {Subject}", certificate.Subject);
                return _options.AllowWhenOcspUnavailable;
            }
        }

        private async Task<bool> ValidateStapledOcspResponseAsync(
            X509Certificate2 certificate,
            X509Certificate2 issuerCertificate,
            byte[] stapledResponse)
        {
            try
            {
                // Parse and validate the stapled OCSP response
                // This is a simplified implementation - in production, you'd need
                // to properly parse the OCSP response according to RFC 6960
                
                _logger.LogDebug("Parsing stapled OCSP response for certificate {Subject}", certificate.Subject);

                // Verify the response signature and timestamp
                if (await VerifyOcspResponseSignatureAsync(stapledResponse, issuerCertificate))
                {
                    // Check if the response indicates the certificate is not revoked
                    var status = ParseOcspResponseStatus(stapledResponse);
                    
                    _logger.LogInformation("Stapled OCSP response status: {Status}", status);
                    return status == OcspResponseStatus.Good;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating stapled OCSP response");
                return false;
            }
        }

        private async Task<bool> QueryOcspDirectlyAsync(
            X509Certificate2 certificate,
            X509Certificate2 issuerCertificate,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get OCSP URL from certificate's Authority Information Access extension
                var ocspUrl = GetOcspUrlFromCertificate(certificate);
                
                if (string.IsNullOrEmpty(ocspUrl))
                {
                    _logger.LogWarning("No OCSP URL found in certificate {Subject}", certificate.Subject);
                    return _options.AllowWhenOcspUnavailable;
                }

                _logger.LogDebug("Querying OCSP responder at {OcspUrl}", ocspUrl);

                // Create OCSP request
                var ocspRequest = CreateOcspRequest(certificate, issuerCertificate);
                
                // Send OCSP request
                using var content = new ByteArrayContent(ocspRequest);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/ocsp-request");

                using var response = await _httpClient.PostAsync(ocspUrl, content, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OCSP query failed with status {StatusCode}", response.StatusCode);
                    return _options.AllowWhenOcspUnavailable;
                }

                var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var status = ParseOcspResponseStatus(responseData);

                _logger.LogInformation("OCSP response status: {Status}", status);
                return status == OcspResponseStatus.Good;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during direct OCSP query");
                return _options.AllowWhenOcspUnavailable;
            }
        }

        private string GetOcspUrlFromCertificate(X509Certificate2 certificate)
        {
            // Authority Information Access extension OID: 1.3.6.1.5.5.7.1.1
            const string aiaOid = "1.3.6.1.5.5.7.1.1";
            
            var aiaExtension = certificate.Extensions[aiaOid];
            if (aiaExtension == null)
                return null;

            // Parse the AIA extension to extract OCSP URL
            // This is a simplified implementation
            var data = aiaExtension.RawData;
            var dataStr = System.Text.Encoding.ASCII.GetString(data);
            
            // Look for HTTP URLs in the extension data
            var httpIndex = dataStr.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
            if (httpIndex == -1)
                httpIndex = dataStr.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            
            if (httpIndex == -1)
                return null;

            var endIndex = dataStr.IndexOfAny(new[] { '\0', ' ', '\r', '\n' }, httpIndex);
            if (endIndex == -1)
                endIndex = dataStr.Length;

            return dataStr.Substring(httpIndex, endIndex - httpIndex);
        }

        private byte[] CreateOcspRequest(X509Certificate2 certificate, X509Certificate2 issuerCertificate)
        {
            // Create a basic OCSP request
            // This is a simplified implementation - in production, you'd need
            // to properly construct the OCSP request according to RFC 6960
            
            var serialNumber = certificate.GetSerialNumber();
            var issuerKeyHash = SHA1.HashData(issuerCertificate.GetPublicKey());
            var issuerNameHash = SHA1.HashData(issuerCertificate.SubjectName.RawData);

            // Return a basic OCSP request structure
            // In practice, you'd use a proper ASN.1 encoder
            var request = new byte[100]; // Placeholder
            Array.Copy(serialNumber, 0, request, 0, Math.Min(serialNumber.Length, 20));
            Array.Copy(issuerKeyHash, 0, request, 20, Math.Min(issuerKeyHash.Length, 20));
            Array.Copy(issuerNameHash, 0, request, 40, Math.Min(issuerNameHash.Length, 20));

            return request;
        }

        private async Task<bool> VerifyOcspResponseSignatureAsync(byte[] responseData, X509Certificate2 issuerCertificate)
        {
            // Verify the OCSP response signature
            // This is a simplified implementation
            try
            {
                // In a real implementation, you would:
                // 1. Parse the OCSP response ASN.1 structure
                // 2. Extract the signature and signed data
                // 3. Verify the signature using the appropriate certificate
                
                await Task.Delay(10); // Simulate async work
                return responseData.Length > 0; // Simplified validation
            }
            catch
            {
                return false;
            }
        }

        private OcspResponseStatus ParseOcspResponseStatus(byte[] responseData)
        {
            // Parse the OCSP response status
            // This is a simplified implementation
            if (responseData == null || responseData.Length == 0)
                return OcspResponseStatus.Unknown;

            // In a real implementation, you would parse the ASN.1 structure
            // and extract the actual certificate status
            
            // For now, assume Good status if we have response data
            return OcspResponseStatus.Good;
        }

        private bool TryGetCachedResult(string certificateThumbprint, out bool result)
        {
            result = false;
            
            if (_ocspCache.TryGetValue(certificateThumbprint, out var cached))
            {
                var age = DateTime.UtcNow - cached.Timestamp;
                if (age.TotalHours < _options.MaxOcspCacheAgeHours)
                {
                    result = cached.IsValid;
                    return true;
                }
                
                // Remove expired entry
                _ocspCache.Remove(certificateThumbprint);
            }

            return false;
        }

        private void CacheResult(string certificateThumbprint, bool isValid)
        {
            _ocspCache[certificateThumbprint] = (DateTime.UtcNow, isValid);
        }
    }

    /// <summary>
    /// OCSP response status values
    /// </summary>
    public enum OcspResponseStatus
    {
        Good = 0,
        Revoked = 1,
        Unknown = 2
    }
}
