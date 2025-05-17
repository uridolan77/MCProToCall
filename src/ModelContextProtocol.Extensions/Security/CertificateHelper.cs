using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Helper class for managing certificates
    /// </summary>
    public static class CertificateHelper
    {
        /// <summary>
        /// Loads a certificate from a file
        /// </summary>
        /// <param name="certificatePath">Path to the certificate file</param>
        /// <param name="password">Certificate password</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>The loaded certificate</returns>
        public static X509Certificate2 LoadCertificateFromFile(string certificatePath, string password, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(certificatePath))
                throw new ArgumentNullException(nameof(certificatePath));

            if (!File.Exists(certificatePath))
            {
                var exception = new FileNotFoundException($"Certificate file not found at path: {certificatePath}");
                logger?.LogError(exception, "Certificate file not found");
                throw exception;
            }

            try
            {
                return new X509Certificate2(certificatePath, password, X509KeyStorageFlags.MachineKeySet);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load certificate from file: {Path}", certificatePath);
                throw;
            }
        }

        /// <summary>
        /// Loads a certificate from the certificate store
        /// </summary>
        /// <param name="thumbprint">Certificate thumbprint</param>
        /// <param name="storeLocation">Store location</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>The loaded certificate</returns>
        public static X509Certificate2 LoadCertificateFromStore(
            string thumbprint, 
            StoreLocation storeLocation = StoreLocation.LocalMachine, 
            ILogger logger = null)
        {
            if (string.IsNullOrEmpty(thumbprint))
                throw new ArgumentNullException(nameof(thumbprint));

            try
            {
                using var store = new X509Store(StoreName.My, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                
                var certificates = store.Certificates.Find(
                    X509FindType.FindByThumbprint, 
                    thumbprint, 
                    validOnly: true);

                if (certificates.Count == 0)
                {
                    var exception = new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found in store");
                    logger?.LogError(exception, "Certificate not found in store");
                    throw exception;
                }

                return certificates[0];
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load certificate with thumbprint {Thumbprint} from store", thumbprint);
                throw;
            }
        }
        
        /// <summary>
        /// Creates a self-signed certificate for development use
        /// </summary>
        /// <param name="subjectName">Certificate subject name</param>
        /// <param name="validityDays">Certificate validity period in days</param>
        /// <param name="exportPassword">Password for the exported PFX file</param>
        /// <param name="exportPath">Path to save the PFX file</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>The created certificate</returns>
        public static X509Certificate2 CreateSelfSignedCertificate(
            string subjectName,
            int validityDays = 365,
            string exportPassword = null,
            string exportPath = null,
            ILogger logger = null)
        {
            logger?.LogInformation("Creating self-signed certificate for {Subject}", subjectName);
            
            // Generate a new RSA key pair
            using var rsa = RSA.Create(2048);
            
            // Prepare certificate request
            var certificateRequest = new CertificateRequest(
                $"CN={subjectName}", 
                rsa, 
                HashAlgorithmName.SHA256, 
                RSASignaturePadding.Pkcs1);
            
            // Set certificate extensions
            certificateRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
                
            certificateRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, 
                    true));
                    
            certificateRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, 
                    true));
            
            // Set validity period
            var notBefore = DateTime.UtcNow;
            var notAfter = notBefore.AddDays(validityDays);
            
            // Create the certificate
            var certificate = certificateRequest.CreateSelfSigned(notBefore, notAfter);
            
            logger?.LogInformation("Created self-signed certificate valid from {NotBefore} to {NotAfter}", 
                notBefore, notAfter);
            
            // Export to PFX if requested
            if (!string.IsNullOrEmpty(exportPath))
            {
                try
                {
                    var pfxData = certificate.Export(X509ContentType.Pfx, exportPassword);
                    File.WriteAllBytes(exportPath, pfxData);
                    
                    logger?.LogInformation("Exported certificate to {ExportPath}", exportPath);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to export certificate to {ExportPath}", exportPath);
                }
            }
            
            return certificate;
        }
    }
}
