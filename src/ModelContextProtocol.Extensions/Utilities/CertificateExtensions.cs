using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Utilities
{
    /// <summary>
    /// Extension methods for X509Certificate2
    /// </summary>
    public static class CertificateExtensions
    {
        /// <summary>
        /// Checks if a certificate is expired or not yet valid
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if the certificate is valid, false otherwise</returns>
        public static bool IsValidPeriod(this X509Certificate2 certificate)
        {
            return DateTime.Now >= certificate.NotBefore && DateTime.Now <= certificate.NotAfter;
        }

        /// <summary>
        /// Checks if a certificate is expiring soon
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <param name="thresholdDays">The number of days to consider as "soon"</param>
        /// <returns>True if the certificate is expiring soon, false otherwise</returns>
        public static bool IsExpiringSoon(this X509Certificate2 certificate, int thresholdDays = 30)
        {
            return (certificate.NotAfter - DateTime.Now).TotalDays <= thresholdDays;
        }

        /// <summary>
        /// Checks if a certificate is self-signed
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if the certificate is self-signed, false otherwise</returns>
        public static bool IsSelfSigned(this X509Certificate2 certificate)
        {
            return certificate.Subject == certificate.Issuer;
        }

        /// <summary>
        /// Gets the SHA-256 thumbprint of a certificate
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <returns>The SHA-256 thumbprint as a hex string</returns>
        public static string GetSha256Thumbprint(this X509Certificate2 certificate)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(certificate.RawData);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets the public key pins for a certificate
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <returns>A dictionary of pin types and values</returns>
        public static Dictionary<string, string> GetPublicKeyPins(this X509Certificate2 certificate)
        {
            var pins = new Dictionary<string, string>();
            
            // Get the public key
            var publicKey = certificate.PublicKey.EncodedKeyValue.RawData;
            
            // Calculate SHA-256 pin
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(publicKey);
                pins["sha256"] = Convert.ToBase64String(hash);
            }
            
            return pins;
        }

        /// <summary>
        /// Gets the subject alternative names from a certificate
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <returns>A list of subject alternative names</returns>
        public static List<string> GetSubjectAlternativeNames(this X509Certificate2 certificate)
        {
            var sanList = new List<string>();
            
            foreach (var extension in certificate.Extensions)
            {
                if (extension.Oid.Value == "2.5.29.17") // Subject Alternative Name
                {
                    var asnData = new AsnEncodedData(extension.Oid, extension.RawData);
                    var sanString = asnData.Format(false);
                    
                    // Parse the SAN string
                    var sans = sanString.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var san in sans)
                    {
                        var trimmedSan = san.Trim();
                        if (!string.IsNullOrEmpty(trimmedSan))
                        {
                            sanList.Add(trimmedSan);
                        }
                    }
                    
                    break;
                }
            }
            
            return sanList;
        }

        /// <summary>
        /// Checks if a certificate has a specific key usage
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <param name="keyUsage">The key usage to check</param>
        /// <returns>True if the certificate has the specified key usage, false otherwise</returns>
        public static bool HasKeyUsage(this X509Certificate2 certificate, X509KeyUsageFlags keyUsage)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509KeyUsageExtension keyUsageExtension)
                {
                    return (keyUsageExtension.KeyUsages & keyUsage) == keyUsage;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Checks if a certificate has a specific enhanced key usage
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <param name="oid">The OID of the enhanced key usage to check</param>
        /// <returns>True if the certificate has the specified enhanced key usage, false otherwise</returns>
        public static bool HasEnhancedKeyUsage(this X509Certificate2 certificate, string oid)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension ekuExtension)
                {
                    return ekuExtension.EnhancedKeyUsages.Cast<Oid>().Any(ekuOid => ekuOid.Value == oid);
                }
            }
            
            return false;
        }

        /// <summary>
        /// Logs certificate details
        /// </summary>
        /// <param name="certificate">The certificate</param>
        /// <param name="logger">The logger</param>
        /// <param name="logLevel">The log level</param>
        public static void LogCertificateDetails(this X509Certificate2 certificate, ILogger logger, LogLevel logLevel = LogLevel.Debug)
        {
            logger.Log(logLevel, "Certificate Details:");
            logger.Log(logLevel, "  Subject: {Subject}", certificate.Subject);
            logger.Log(logLevel, "  Issuer: {Issuer}", certificate.Issuer);
            logger.Log(logLevel, "  Serial Number: {SerialNumber}", certificate.SerialNumber);
            logger.Log(logLevel, "  Thumbprint: {Thumbprint}", certificate.Thumbprint);
            logger.Log(logLevel, "  Not Before: {NotBefore}", certificate.NotBefore);
            logger.Log(logLevel, "  Not After: {NotAfter}", certificate.NotAfter);
            logger.Log(logLevel, "  Has Private Key: {HasPrivateKey}", certificate.HasPrivateKey);
            logger.Log(logLevel, "  Self-Signed: {IsSelfSigned}", certificate.IsSelfSigned());
            
            // Log key usage if available
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509KeyUsageExtension keyUsageExtension)
                {
                    logger.Log(logLevel, "  Key Usage: {KeyUsage}", keyUsageExtension.KeyUsages);
                }
                else if (extension is X509EnhancedKeyUsageExtension ekuExtension)
                {
                    var ekus = string.Join(", ", ekuExtension.EnhancedKeyUsages.Cast<Oid>().Select(oid => $"{oid.FriendlyName} ({oid.Value})"));
                    logger.Log(logLevel, "  Enhanced Key Usage: {EnhancedKeyUsage}", ekus);
                }
            }
            
            // Log subject alternative names
            var sans = certificate.GetSubjectAlternativeNames();
            if (sans.Count > 0)
            {
                logger.Log(logLevel, "  Subject Alternative Names: {SubjectAlternativeNames}", string.Join(", ", sans));
            }
        }
    }
}
