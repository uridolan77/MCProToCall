using System.Security.Cryptography.X509Certificates;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Interface for certificate revocation checking services
    /// </summary>
    public interface ICertificateRevocationChecker
    {
        /// <summary>
        /// Validates that a certificate has not been revoked
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if the certificate is not revoked, false otherwise</returns>
        bool ValidateCertificateNotRevoked(X509Certificate2 certificate);
        
        /// <summary>
        /// Updates cached revocation lists from online sources
        /// </summary>
        /// <returns>True if the update was successful, false otherwise</returns>
        bool UpdateRevocationLists();
        
        /// <summary>
        /// Adds a certificate to a local cache of revoked certificates
        /// </summary>
        /// <param name="certificate">The certificate to mark as revoked</param>
        /// <returns>True if the certificate was successfully added to the revocation list</returns>
        bool AddToRevocationList(X509Certificate2 certificate);
    }
}
