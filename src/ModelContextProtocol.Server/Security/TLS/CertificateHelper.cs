using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Helper class for working with X.509 certificates
    /// </summary>
    public static class CertificateHelper
    {
        /// <summary>
        /// Loads a certificate from a file
        /// </summary>
        /// <param name="path">The path to the certificate file</param>
        /// <param name="password">The password for the certificate file</param>
        /// <returns>The loaded certificate</returns>
        public static X509Certificate2 LoadCertificateFromFile(string path, string password = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Certificate file not found", path);
            }

            X509KeyStorageFlags flags = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable;

            return string.IsNullOrEmpty(password)
                ? new X509Certificate2(path, string.Empty, flags)
                : new X509Certificate2(path, password, flags);
        }

        /// <summary>
        /// Loads a certificate from a file with logging
        /// </summary>
        /// <param name="path">The path to the certificate file</param>
        /// <param name="password">The password for the certificate file</param>
        /// <param name="logger">Logger for logging messages</param>
        /// <returns>The loaded certificate</returns>
        public static X509Certificate2 LoadCertificateFromFile(string path, string password, ILogger logger)
        {
            try
            {
                return LoadCertificateFromFile(path, password);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load certificate from file: {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Loads a certificate from the certificate store
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate</param>
        /// <param name="storeLocation">The store location</param>
        /// <param name="logger">Logger for logging messages</param>
        /// <returns>The loaded certificate</returns>
        public static X509Certificate2 LoadCertificateFromStore(
            string thumbprint, 
            StoreLocation storeLocation,
            ILogger logger)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new ArgumentNullException(nameof(thumbprint));
            }

            try
            {
                using (var store = new X509Store(StoreName.My, storeLocation))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certificates = store.Certificates.Find(
                        X509FindType.FindByThumbprint, 
                        thumbprint, 
                        false);
                    
                    if (certificates.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Certificate with thumbprint {thumbprint} not found");
                    }
                    
                    return certificates[0];
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load certificate from store: {Thumbprint}", thumbprint);
                throw;
            }
        }
    }
}
