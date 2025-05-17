using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Client.Security
{
    /// <summary>
    /// Extensions for TLS functionality
    /// </summary>
    public static class TlsExtensions
    {
        /// <summary>
        /// Validates a server certificate
        /// </summary>
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors,
            ILogger logger)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            logger?.LogWarning("Certificate validation errors: {Errors}", sslPolicyErrors);

            // Log detailed chain status information
            if (chain != null)
            {
                foreach (var status in chain.ChainStatus)
                {
                    logger?.LogWarning("Certificate chain status: {Status} - {StatusInformation}",
                        status.Status, status.StatusInformation);
                }
            }

            // In a real implementation, we would check for specific errors and handle them
            // For now, we'll just return false for any errors
            return false;
        }

        /// <summary>
        /// Validates a certificate against a pinned thumbprint
        /// </summary>
        public static bool ValidateCertificateThumbprint(
            X509Certificate2 certificate,
            string expectedThumbprint,
            ILogger logger)
        {
            if (certificate == null)
            {
                logger?.LogWarning("Certificate is null");
                return false;
            }

            if (string.IsNullOrEmpty(expectedThumbprint))
            {
                logger?.LogWarning("Expected thumbprint is null or empty");
                return false;
            }

            string actualThumbprint = certificate.Thumbprint;
            bool isValid = string.Equals(actualThumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                logger?.LogWarning("Certificate thumbprint mismatch. Expected: {Expected}, Actual: {Actual}",
                    expectedThumbprint, actualThumbprint);
            }

            return isValid;
        }
    }
}
