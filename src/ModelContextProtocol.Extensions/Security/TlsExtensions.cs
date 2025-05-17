using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Extension methods for configuring and using TLS in MCP communications
    /// </summary>
    public static class TlsExtensions
    {
        /// <summary>
        /// Configures TLS for a server-side SSL stream with secure defaults and optional client certificate requirements
        /// </summary>
        /// <param name="sslStream">The SSL stream to configure</param>
        /// <param name="certificate">The server certificate</param>
        /// <param name="clientCertificateRequired">Whether client certificates are required</param>
        /// <param name="logger">Optional logger for security events</param>
        /// <param name="remoteEndpoint">Optional remote endpoint information for rate limiting</param>
        /// <param name="connectionLimit">Optional connection limit per client</param>
        public static void ConfigureTls(
            this SslStream sslStream, 
            X509Certificate2 certificate, 
            bool clientCertificateRequired = false,
            ILogger logger = null,
            string remoteEndpoint = null,
            int connectionLimit = 0)
        {
            if (sslStream == null)
            {
                throw new ArgumentNullException(nameof(sslStream));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            // Configure secure protocols - using TLS 1.2 and 1.3 only
            var sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            
            try
            {
                // Apply connection rate limiting if configured
                if (!string.IsNullOrEmpty(remoteEndpoint) && connectionLimit > 0)
                {
                    if (!TlsConnectionManager.CheckConnectionLimit(remoteEndpoint, connectionLimit))
                    {
                        logger?.LogWarning("Connection limit exceeded for {RemoteEndpoint}", remoteEndpoint);
                        throw new SecurityException($"Connection limit exceeded for {remoteEndpoint}");
                    }
                }
                
                sslStream.AuthenticateAsServer(
                    certificate,
                    clientCertificateRequired: clientCertificateRequired,
                    enabledSslProtocols: sslProtocols,
                    checkCertificateRevocation: true);
                
                // If we reached here, the connection is successful, so register it
                if (!string.IsNullOrEmpty(remoteEndpoint) && connectionLimit > 0)
                {
                    TlsConnectionManager.RegisterConnection(remoteEndpoint);
                }
                
                logger?.LogInformation("TLS connection established with protocol: {Protocol}", sslStream.SslProtocol);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to establish secure TLS connection");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously configures TLS for a server-side SSL stream
        /// </summary>
        /// <param name="sslStream">The SSL stream to configure</param>
        /// <param name="certificate">The server certificate</param>
        /// <param name="clientCertificateRequired">Whether client certificates are required</param>
        /// <param name="logger">Optional logger for security events</param>
        public static async Task ConfigureTlsAsync(
            this SslStream sslStream, 
            X509Certificate2 certificate, 
            bool clientCertificateRequired = false,
            ILogger logger = null)
        {
            if (sslStream == null)
            {
                throw new ArgumentNullException(nameof(sslStream));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            // Configure secure protocols - using TLS 1.2 and 1.3 only
            var sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            
            try
            {
                await sslStream.AuthenticateAsServerAsync(
                    certificate,
                    clientCertificateRequired: clientCertificateRequired,
                    enabledSslProtocols: sslProtocols,
                    checkCertificateRevocation: true);
                
                logger?.LogInformation("TLS connection established with protocol: {Protocol}", sslStream.SslProtocol);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to establish secure TLS connection");
                throw;
            }
        }

        /// <summary>
        /// Configures TLS for a client-side SSL stream with secure defaults
        /// </summary>
        /// <param name="sslStream">The SSL stream to configure</param>
        /// <param name="targetHost">The target host name for server validation</param>
        /// <param name="clientCertificate">Optional client certificate for mutual TLS</param>
        /// <param name="logger">Optional logger for security events</param>
        public static void ConfigureTlsClient(
            this SslStream sslStream,
            string targetHost,
            X509Certificate2 clientCertificate = null,
            ILogger logger = null)
        {
            if (sslStream == null)
            {
                throw new ArgumentNullException(nameof(sslStream));
            }

            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new ArgumentException("Target host must be specified", nameof(targetHost));
            }

            // Configure secure protocols - using TLS 1.2 and 1.3 only
            var sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            
            try
            {
                sslStream.AuthenticateAsClient(
                    targetHost,
                    clientCertificates: clientCertificate != null ? new X509CertificateCollection { clientCertificate } : null,
                    enabledSslProtocols: sslProtocols,
                    checkCertificateRevocation: true);
                
                logger?.LogInformation("TLS client connection established with protocol: {Protocol}", sslStream.SslProtocol);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to establish secure TLS client connection");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously configures TLS for a client-side SSL stream with secure defaults
        /// </summary>
        /// <param name="sslStream">The SSL stream to configure</param>
        /// <param name="targetHost">The target host name for server validation</param>
        /// <param name="clientCertificate">Optional client certificate for mutual TLS</param>
        /// <param name="logger">Optional logger for security events</param>
        public static async Task ConfigureTlsClientAsync(
            this SslStream sslStream,
            string targetHost,
            X509Certificate2 clientCertificate = null,
            ILogger logger = null)
        {
            if (sslStream == null)
            {
                throw new ArgumentNullException(nameof(sslStream));
            }

            if (string.IsNullOrWhiteSpace(targetHost))
            {
                throw new ArgumentException("Target host must be specified", nameof(targetHost));
            }

            // Configure secure protocols - using TLS 1.2 and 1.3 only
            var sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            
            try
            {
                await sslStream.AuthenticateAsClientAsync(
                    targetHost,
                    clientCertificates: clientCertificate != null ? new X509CertificateCollection { clientCertificate } : null,
                    enabledSslProtocols: sslProtocols,
                    checkCertificateRevocation: true);
                
                logger?.LogInformation("TLS client connection established with protocol: {Protocol}", sslStream.SslProtocol);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to establish secure TLS client connection");
                throw;
            }
        }

        /// <summary>
        /// Validates a server certificate during client TLS negotiation
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="sslPolicyErrors">Any SSL policy errors</param>
        /// <param name="logger">Optional logger for security events</param>
        /// <returns>True if the certificate is valid, otherwise false</returns>
        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors,
            ILogger logger = null)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                logger?.LogDebug("Server certificate validated successfully");
                return true; // Certificate is valid
            }

            // Log detailed information about the certificate and validation failures
            var x509Certificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
            logger?.LogWarning("Certificate validation failed with errors: {Errors}", sslPolicyErrors);
            logger?.LogWarning("Certificate subject: {Subject}, issuer: {Issuer}", 
                x509Certificate.Subject, 
                x509Certificate.Issuer);

            // You can implement custom validation logic here if needed
            // For example, allowing self-signed certificates in development:
            // if (isDevelopment && sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) return true;

            return false; // Certificate is invalid
        }

        /// <summary>
        /// Validates a client certificate during server TLS negotiation
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="sslPolicyErrors">Any SSL policy errors</param>
        /// <param name="allowedClientThumbprints">Optional list of allowed client certificate thumbprints</param>
        /// <param name="logger">Optional logger for security events</param>
        /// <returns>True if the certificate is valid, otherwise false</returns>
        public static bool ValidateClientCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors,
            string[] allowedClientThumbprints = null,
            ILogger logger = null)
        {
            if (certificate == null)
            {
                logger?.LogWarning("Client certificate validation failed: No certificate provided");
                return false;
            }

            var x509Certificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
            var thumbprint = x509Certificate.Thumbprint;

            logger?.LogDebug("Validating client certificate: {Subject}, thumbprint: {Thumbprint}", 
                x509Certificate.Subject, 
                thumbprint);

            // Check basic certificate validity
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                logger?.LogWarning("Client certificate validation failed with errors: {Errors}", sslPolicyErrors);
                
                // You can implement custom validation logic here if needed
                // For example, ignoring certain errors in specific scenarios
            }

            // If we have a list of allowed thumbprints, verify the client certificate is in that list
            if (allowedClientThumbprints != null && allowedClientThumbprints.Length > 0)
            {
                bool isAllowed = Array.Exists(allowedClientThumbprints, t => 
                    string.Equals(t, thumbprint, StringComparison.OrdinalIgnoreCase));
                
                if (!isAllowed)
                {
                    logger?.LogWarning("Client certificate rejected: Thumbprint not in allowed list");
                    return false;
                }
            }

            // Verify the certificate is current
            if (DateTime.Now < x509Certificate.NotBefore || DateTime.Now > x509Certificate.NotAfter)
            {
                logger?.LogWarning("Client certificate rejected: Certificate not within valid time period");
                return false;
            }

            // Certificate has passed all validation
            logger?.LogInformation("Client certificate validated successfully");
            return true;
        }
    }
}