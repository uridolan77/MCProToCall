using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.HSM
{
    /// <summary>
    /// Interface for Hardware Security Module operations
    /// </summary>
    public interface IHardwareSecurityModule
    {
        /// <summary>
        /// Gets a certificate from the HSM
        /// </summary>
        /// <param name="identifier">Certificate identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The certificate</returns>
        Task<X509Certificate2> GetCertificateAsync(string identifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs data using a key in the HSM
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="data">Data to sign</param>
        /// <param name="algorithm">Signing algorithm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Signature bytes</returns>
        Task<byte[]> SignDataAsync(string keyIdentifier, byte[] data, string algorithm = "RS256", CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts data using a key in the HSM
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="data">Data to encrypt</param>
        /// <param name="algorithm">Encryption algorithm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encrypted data</returns>
        Task<byte[]> EncryptDataAsync(string keyIdentifier, byte[] data, string algorithm = "RSA-OAEP", CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts data using a key in the HSM
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="encryptedData">Encrypted data</param>
        /// <param name="algorithm">Decryption algorithm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decrypted data</returns>
        Task<byte[]> DecryptDataAsync(string keyIdentifier, byte[] encryptedData, string algorithm = "RSA-OAEP", CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a signature using a key in the HSM
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="data">Original data</param>
        /// <param name="signature">Signature to verify</param>
        /// <param name="algorithm">Signing algorithm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if signature is valid</returns>
        Task<bool> VerifySignatureAsync(string keyIdentifier, byte[] data, byte[] signature, string algorithm = "RS256", CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a new key pair in the HSM
        /// </summary>
        /// <param name="keyName">Name for the new key</param>
        /// <param name="keyType">Type of key to generate</param>
        /// <param name="keySize">Size of the key in bits</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Key identifier</returns>
        Task<string> GenerateKeyPairAsync(string keyName, HsmKeyType keyType = HsmKeyType.RSA, int keySize = 2048, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a key from the HSM
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if key was deleted</returns>
        Task<bool> DeleteKeyAsync(string keyIdentifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all keys in the HSM
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of key identifiers</returns>
        Task<string[]> ListKeysAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about a key
        /// </summary>
        /// <param name="keyIdentifier">Key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Key information</returns>
        Task<HsmKeyInfo> GetKeyInfoAsync(string keyIdentifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests connectivity to the HSM
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if HSM is accessible</returns>
        Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Types of keys supported by HSM
    /// </summary>
    public enum HsmKeyType
    {
        /// <summary>
        /// RSA key
        /// </summary>
        RSA,

        /// <summary>
        /// Elliptic Curve key
        /// </summary>
        EC,

        /// <summary>
        /// AES symmetric key
        /// </summary>
        AES
    }

    /// <summary>
    /// Information about an HSM key
    /// </summary>
    public class HsmKeyInfo
    {
        /// <summary>
        /// Key identifier
        /// </summary>
        public string KeyIdentifier { get; set; }

        /// <summary>
        /// Key name
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>
        /// Key type
        /// </summary>
        public HsmKeyType KeyType { get; set; }

        /// <summary>
        /// Key size in bits
        /// </summary>
        public int KeySize { get; set; }

        /// <summary>
        /// When the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Key usage flags
        /// </summary>
        public HsmKeyUsage Usage { get; set; }

        /// <summary>
        /// Whether the key is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Key expiration date (if any)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// HSM key usage flags
    /// </summary>
    [Flags]
    public enum HsmKeyUsage
    {
        /// <summary>
        /// No specific usage
        /// </summary>
        None = 0,

        /// <summary>
        /// Key can be used for signing
        /// </summary>
        Sign = 1,

        /// <summary>
        /// Key can be used for verification
        /// </summary>
        Verify = 2,

        /// <summary>
        /// Key can be used for encryption
        /// </summary>
        Encrypt = 4,

        /// <summary>
        /// Key can be used for decryption
        /// </summary>
        Decrypt = 8,

        /// <summary>
        /// Key can be used for key wrapping
        /// </summary>
        WrapKey = 16,

        /// <summary>
        /// Key can be used for key unwrapping
        /// </summary>
        UnwrapKey = 32,

        /// <summary>
        /// All operations
        /// </summary>
        All = Sign | Verify | Encrypt | Decrypt | WrapKey | UnwrapKey
    }

    /// <summary>
    /// HSM operation result
    /// </summary>
    public class HsmOperationResult<T>
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// The result data
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Error code if operation failed
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static HsmOperationResult<T> Success(T result, TimeSpan duration = default)
        {
            return new HsmOperationResult<T>
            {
                IsSuccessful = true,
                Result = result,
                Duration = duration
            };
        }

        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static HsmOperationResult<T> Failure(string errorMessage, string errorCode = null, TimeSpan duration = default)
        {
            return new HsmOperationResult<T>
            {
                IsSuccessful = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Duration = duration
            };
        }
    }
}
