using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Utility for generating test certificates for TLS testing
    /// </summary>
    public class CertificateGenerator
    {
        private readonly ILogger<CertificateGenerator> _logger;

        public CertificateGenerator(ILogger<CertificateGenerator> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CertificateGenerator>.Instance;
        }

        /// <summary>
        /// Generates a self-signed certificate for testing
        /// </summary>
        /// <param name="subjectName">Certificate subject name (e.g., "CN=TestServer")</param>
        /// <param name="validFrom">Certificate valid from date</param>
        /// <param name="validTo">Certificate valid to date</param>
        /// <param name="keySize">RSA key size (default 2048)</param>
        /// <returns>Generated certificate with private key</returns>
        public X509Certificate2 GenerateSelfSignedCertificate(
            string subjectName,
            DateTime validFrom,
            DateTime validTo,
            int keySize = 2048)
        {
            _logger.LogDebug("Generating self-signed certificate: {Subject}", subjectName);

            using var rsa = RSA.Create(keySize);
            var request = new CertificateRequest(
                subjectName,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add key usage extensions
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            // Add enhanced key usage for server authentication
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                    critical: true));

            // Add subject alternative name for localhost
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("127.0.0.1");
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Create the certificate
            var certificate = request.CreateSelfSigned(validFrom, validTo);

            _logger.LogDebug("Generated certificate with thumbprint: {Thumbprint}", certificate.Thumbprint);
            return certificate;
        }

        /// <summary>
        /// Generates a certificate chain for testing
        /// </summary>
        /// <param name="rootSubject">Root CA subject name</param>
        /// <param name="intermediateSubject">Intermediate CA subject name</param>
        /// <param name="serverSubject">Server certificate subject name</param>
        /// <param name="validFrom">Certificates valid from date</param>
        /// <param name="validTo">Certificates valid to date</param>
        /// <returns>Certificate chain (root, intermediate, server)</returns>
        public (X509Certificate2 root, X509Certificate2 intermediate, X509Certificate2 server) GenerateCertificateChain(
            string rootSubject = "CN=Test Root CA",
            string intermediateSubject = "CN=Test Intermediate CA",
            string serverSubject = "CN=Test Server",
            DateTime? validFrom = null,
            DateTime? validTo = null)
        {
            validFrom ??= DateTime.UtcNow.AddDays(-1);
            validTo ??= DateTime.UtcNow.AddDays(30);

            _logger.LogDebug("Generating certificate chain");

            // Generate root CA
            using var rootRsa = RSA.Create(2048);
            var rootRequest = new CertificateRequest(
                rootSubject,
                rootRsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            rootRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 2, true));
            rootRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));

            var rootCert = rootRequest.CreateSelfSigned(validFrom.Value, validTo.Value);

            // Generate intermediate CA
            using var intermediateRsa = RSA.Create(2048);
            var intermediateRequest = new CertificateRequest(
                intermediateSubject,
                intermediateRsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            intermediateRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 1, true));
            intermediateRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));

            var intermediateCert = intermediateRequest.Create(
                rootCert,
                validFrom.Value,
                validTo.Value,
                BitConverter.GetBytes(DateTime.UtcNow.Ticks));

            // Combine intermediate cert with its private key
            var intermediateWithKey = intermediateCert.CopyWithPrivateKey(intermediateRsa);

            // Generate server certificate
            using var serverRsa = RSA.Create(2048);
            var serverRequest = new CertificateRequest(
                serverSubject,
                serverRsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            serverRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    true));
            serverRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                    true));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            serverRequest.CertificateExtensions.Add(sanBuilder.Build());

            var serverCert = serverRequest.Create(
                intermediateWithKey,
                validFrom.Value,
                validTo.Value,
                BitConverter.GetBytes(DateTime.UtcNow.Ticks + 1));

            var serverWithKey = serverCert.CopyWithPrivateKey(serverRsa);

            _logger.LogDebug("Generated certificate chain - Root: {RootThumbprint}, Intermediate: {IntermediateThumbprint}, Server: {ServerThumbprint}",
                rootCert.Thumbprint, intermediateWithKey.Thumbprint, serverWithKey.Thumbprint);

            return (rootCert, intermediateWithKey, serverWithKey);
        }

        /// <summary>
        /// Generates a certificate with specific key usage for testing
        /// </summary>
        /// <param name="subjectName">Certificate subject name</param>
        /// <param name="keyUsage">Key usage flags</param>
        /// <param name="enhancedKeyUsage">Enhanced key usage OIDs</param>
        /// <param name="validFrom">Certificate valid from date</param>
        /// <param name="validTo">Certificate valid to date</param>
        /// <returns>Generated certificate</returns>
        public X509Certificate2 GenerateCertificateWithKeyUsage(
            string subjectName,
            X509KeyUsageFlags keyUsage,
            string[] enhancedKeyUsage = null,
            DateTime? validFrom = null,
            DateTime? validTo = null)
        {
            validFrom ??= DateTime.UtcNow.AddDays(-1);
            validTo ??= DateTime.UtcNow.AddDays(30);

            _logger.LogDebug("Generating certificate with key usage: {KeyUsage}", keyUsage);

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                subjectName,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(keyUsage, true));

            if (enhancedKeyUsage != null && enhancedKeyUsage.Length > 0)
            {
                var oidCollection = new OidCollection();
                foreach (var oid in enhancedKeyUsage)
                {
                    oidCollection.Add(new Oid(oid));
                }
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(oidCollection, true));
            }

            var certificate = request.CreateSelfSigned(validFrom.Value, validTo.Value);
            return certificate;
        }

        /// <summary>
        /// Generates an expired certificate for testing
        /// </summary>
        /// <param name="subjectName">Certificate subject name</param>
        /// <param name="expiredDaysAgo">Number of days ago the certificate expired</param>
        /// <returns>Expired certificate</returns>
        public X509Certificate2 GenerateExpiredCertificate(
            string subjectName = "CN=Expired Test Certificate",
            int expiredDaysAgo = 1)
        {
            var validFrom = DateTime.UtcNow.AddDays(-30);
            var validTo = DateTime.UtcNow.AddDays(-expiredDaysAgo);

            _logger.LogDebug("Generating expired certificate: {Subject}, expired {Days} days ago", 
                subjectName, expiredDaysAgo);

            return GenerateSelfSignedCertificate(subjectName, validFrom, validTo);
        }

        /// <summary>
        /// Saves a certificate to a file
        /// </summary>
        /// <param name="certificate">Certificate to save</param>
        /// <param name="filePath">File path to save to</param>
        /// <param name="password">Password for the PFX file (optional)</param>
        /// <param name="format">Certificate format</param>
        public async Task SaveCertificateAsync(
            X509Certificate2 certificate,
            string filePath,
            string password = null,
            X509ContentType format = X509ContentType.Pfx)
        {
            _logger.LogDebug("Saving certificate to: {FilePath}", filePath);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] certData;
            if (format == X509ContentType.Pfx && !string.IsNullOrEmpty(password))
            {
                certData = certificate.Export(format, password);
            }
            else
            {
                certData = certificate.Export(format);
            }

            await File.WriteAllBytesAsync(filePath, certData);
            _logger.LogDebug("Certificate saved successfully");
        }

        /// <summary>
        /// Creates a certificate revocation list (CRL) for testing
        /// </summary>
        /// <param name="issuerCertificate">Issuer certificate</param>
        /// <param name="revokedCertificates">List of revoked certificate serial numbers</param>
        /// <param name="nextUpdate">Next CRL update time</param>
        /// <returns>CRL as byte array</returns>
        public byte[] GenerateCertificateRevocationList(
            X509Certificate2 issuerCertificate,
            IEnumerable<string> revokedCertificates = null,
            DateTime? nextUpdate = null)
        {
            nextUpdate ??= DateTime.UtcNow.AddDays(30);
            revokedCertificates ??= new List<string>();

            _logger.LogDebug("Generating CRL for issuer: {Issuer}", issuerCertificate.Subject);

            // Note: This is a simplified CRL generation for testing purposes
            // In a real implementation, you would use proper ASN.1 encoding
            var crlData = new StringBuilder();
            crlData.AppendLine("-----BEGIN X509 CRL-----");
            crlData.AppendLine("Mock CRL for testing");
            crlData.AppendLine($"Issuer: {issuerCertificate.Subject}");
            crlData.AppendLine($"This Update: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            crlData.AppendLine($"Next Update: {nextUpdate:yyyy-MM-dd HH:mm:ss}");
            
            foreach (var serialNumber in revokedCertificates)
            {
                crlData.AppendLine($"Revoked: {serialNumber} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            }
            
            crlData.AppendLine("-----END X509 CRL-----");

            return Encoding.UTF8.GetBytes(crlData.ToString());
        }
    }
}
