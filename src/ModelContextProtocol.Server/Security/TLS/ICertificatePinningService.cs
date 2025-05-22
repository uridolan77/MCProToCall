using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.TLS
{
    /// <summary>
    /// Interface for certificate pinning service
    /// </summary>
    public interface ICertificatePinningService
    {
        /// <summary>
        /// Adds a certificate to the pinned certificates
        /// </summary>
        /// <param name="certificate">The certificate to pin</param>
        Task AddPinnedCertificateAsync(X509Certificate2 certificate);

        /// <summary>
        /// Removes a certificate from the pinned certificates
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to remove</param>
        Task RemovePinnedCertificateAsync(string thumbprint);

        /// <summary>
        /// Checks if a certificate is pinned
        /// </summary>
        /// <param name="certificate">The certificate to check</param>
        /// <returns>True if the certificate is pinned, false otherwise</returns>
        Task<bool> IsCertificatePinnedAsync(X509Certificate2 certificate);

        /// <summary>
        /// Gets all pinned certificates
        /// </summary>
        /// <returns>An array of pinned certificates</returns>
        Task<X509Certificate2[]> GetPinnedCertificatesAsync();

        /// <summary>
        /// Adds a certificate pin
        /// </summary>
        /// <param name="certificate">The certificate to pin</param>
        /// <param name="isPermanent">Whether the pin is permanent</param>
        void AddCertificatePin(X509Certificate2 certificate, bool isPermanent);

        /// <summary>
        /// Validates a certificate against the pinned certificate
        /// </summary>
        /// <param name="certificate">Certificate to validate</param>
        /// <returns>True if the certificate matches the pinned certificate, false otherwise</returns>
        Task<bool> ValidatePinAsync(X509Certificate2 certificate);
    }
}
