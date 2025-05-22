using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Interface for certificate revocation checking
    /// </summary>
    public interface ICertificateRevocationChecker
    {
        /// <summary>
        /// Checks if a certificate is revoked
        /// </summary>
        /// <param name="certificate">Certificate to check</param>
        /// <returns>True if the certificate is revoked, false otherwise</returns>
        Task<bool> IsRevokedAsync(X509Certificate2 certificate);
    }
}
