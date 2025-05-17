using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Interface for certificate validation services
    /// </summary>
    public interface ICertificateValidator
    {
        /// <summary>
        /// Validates a certificate chain
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="errors">Any SSL policy errors</param>
        /// <returns>True if the certificate is valid, false otherwise</returns>
        bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors);
        
        /// <summary>
        /// Validates a server certificate during client connection
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="certificate">The certificate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="errors">Any SSL policy errors</param>
        /// <returns>True if the certificate is valid, false otherwise</returns>
        bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors);
        
        /// <summary>
        /// Validates a client certificate during server connection
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="certificate">The certificate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="errors">Any SSL policy errors</param>
        /// <returns>True if the certificate is valid, false otherwise</returns>
        bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors);
    }
}
