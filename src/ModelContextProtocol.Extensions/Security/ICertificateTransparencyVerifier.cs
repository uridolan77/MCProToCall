using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Interface for certificate transparency verification services
    /// </summary>
    public interface ICertificateTransparencyVerifier
    {
        /// <summary>
        /// Verifies that a certificate is present in Certificate Transparency logs
        /// </summary>
        /// <param name="certificate">The certificate to verify</param>
        /// <returns>True if the certificate is present in CT logs, false otherwise</returns>
        Task<bool> VerifyCertificateInCtLogsAsync(X509Certificate2 certificate);
        
        /// <summary>
        /// Checks if a certificate has embedded SCTs (Signed Certificate Timestamps)
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if the certificate has embedded SCTs, false otherwise</returns>
        bool HasEmbeddedScts(X509Certificate2 certificate);
    }
}
