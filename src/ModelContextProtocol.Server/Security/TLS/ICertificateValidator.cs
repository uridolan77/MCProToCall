using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Interface for validating X.509 certificates
    /// </summary>
    public interface ICertificateValidator
    {
        /// <summary>
        /// Validates a certificate
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <returns>True if the certificate is valid, false otherwise</returns>
        Task<bool> ValidateCertificateAsync(X509Certificate2 certificate);

        /// <summary>
        /// Validates a certificate chain
        /// </summary>
        /// <param name="chain">The certificate chain to validate</param>
        /// <returns>True if the certificate chain is valid, false otherwise</returns>
        Task<bool> ValidateCertificateChainAsync(X509Chain chain);
        
        /// <summary>
        /// Validates a client certificate during TLS handshake
        /// </summary>
        bool ValidateClientCertificate(
            object sender, 
            X509Certificate certificate, 
            X509Chain chain, 
            SslPolicyErrors sslPolicyErrors);
    }
}
