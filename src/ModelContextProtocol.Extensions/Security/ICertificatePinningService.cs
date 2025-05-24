using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Interface for certificate pinning services
    /// </summary>
    public interface ICertificatePinningService
    {
        /// <summary>
        /// Validates a certificate against pinned certificates or public keys
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <returns>True if the certificate matches a pinned certificate or key, false otherwise</returns>
        bool ValidateCertificatePin(X509Certificate2 certificate);

        /// <summary>
        /// Adds a certificate to the pin list
        /// </summary>
        /// <param name="certificate">The certificate to pin</param>
        /// <param name="isPermanent">Whether the pin should be permanent or temporary</param>
        /// <returns>True if the certificate was successfully pinned</returns>
        bool AddCertificatePin(X509Certificate2 certificate, bool isPermanent = false);

        /// <summary>
        /// Removes a certificate from the pin list
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to unpin</param>
        /// <returns>True if the certificate was successfully unpinned</returns>
        bool RemoveCertificatePin(string thumbprint);

        /// <summary>
        /// Checks if a certificate is pinned
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to check</param>
        /// <returns>True if the certificate is pinned</returns>
        bool IsCertificatePinned(string thumbprint);

        /// <summary>
        /// Validates a certificate pin asynchronously
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <returns>True if the certificate matches a pinned certificate or key, false otherwise</returns>
        Task<bool> ValidatePinAsync(X509Certificate2 certificate);

        /// <summary>
        /// Pins a certificate asynchronously
        /// </summary>
        /// <param name="certificate">The certificate to pin</param>
        /// <returns>True if the certificate was successfully pinned, false otherwise</returns>
        Task<bool> PinCertificateAsync(X509Certificate2 certificate);
    }
}
