using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.HSM;

namespace ModelContextProtocol.Extensions.Testing.Doubles
{
    /// <summary>
    /// Test time provider for deterministic time-based testing
    /// </summary>
    public class TestTimeProvider : ITimeProvider
    {
        private DateTime _currentTime;
        private readonly object _lock = new object();

        public TestTimeProvider(DateTime initialTime)
        {
            _currentTime = initialTime;
        }

        public DateTime UtcNow
        {
            get
            {
                lock (_lock)
                {
                    return _currentTime;
                }
            }
        }

        public DateTime Now => UtcNow.ToLocalTime();

        /// <summary>
        /// Advances time by the specified amount
        /// </summary>
        public void Advance(TimeSpan timeSpan)
        {
            lock (_lock)
            {
                _currentTime = _currentTime.Add(timeSpan);
            }
        }

        /// <summary>
        /// Sets the current time
        /// </summary>
        public void SetTime(DateTime time)
        {
            lock (_lock)
            {
                _currentTime = time;
            }
        }

        /// <summary>
        /// Resets to the initial time
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _currentTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Interface for time provider
    /// </summary>
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
        DateTime Now { get; }
    }

    /// <summary>
    /// Mock certificate validator for testing
    /// </summary>
    public class MockCertificateValidator : ICertificateValidator
    {
        private bool _alwaysValid;
        private readonly List<ValidationCall> _validationCalls = new();

        public MockCertificateValidator(bool alwaysValid = true)
        {
            _alwaysValid = alwaysValid;
        }

        public bool ValidateCertificate(X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors)
        {
            var call = new ValidationCall
            {
                Certificate = certificate,
                Chain = chain,
                Errors = errors,
                Timestamp = DateTime.UtcNow,
                Result = _alwaysValid
            };

            _validationCalls.Add(call);
            return _alwaysValid;
        }

        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
            return ValidateCertificate(cert2, chain, errors);
        }

        public bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
            return ValidateCertificate(cert2, chain, errors);
        }

        /// <summary>
        /// Sets whether validation should always return valid
        /// </summary>
        public void SetAlwaysValid(bool alwaysValid)
        {
            _alwaysValid = alwaysValid;
        }

        /// <summary>
        /// Gets all validation calls made to this mock
        /// </summary>
        public IReadOnlyList<ValidationCall> GetValidationCalls()
        {
            return _validationCalls.AsReadOnly();
        }

        /// <summary>
        /// Resets the mock state
        /// </summary>
        public void Reset()
        {
            _validationCalls.Clear();
            _alwaysValid = true;
        }

        public class ValidationCall
        {
            public X509Certificate2 Certificate { get; set; }
            public X509Chain Chain { get; set; }
            public SslPolicyErrors Errors { get; set; }
            public DateTime Timestamp { get; set; }
            public bool Result { get; set; }
        }
    }

    /// <summary>
    /// Mock Hardware Security Module for testing
    /// </summary>
    public class MockHardwareSecurityModule : IHardwareSecurityModule
    {
        private readonly Dictionary<string, X509Certificate2> _certificates = new();
        private readonly Dictionary<string, HsmKeyInfo> _keys = new();
        private readonly List<HsmOperation> _operations = new();
        private bool _isConnected = true;

        public async Task<X509Certificate2> GetCertificateAsync(string identifier, CancellationToken cancellationToken = default)
        {
            RecordOperation("GetCertificate", identifier);

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(10, cancellationToken); // Simulate latency

            if (_certificates.TryGetValue(identifier, out var certificate))
            {
                return certificate;
            }

            throw new HsmOperationException($"Certificate '{identifier}' not found");
        }

        public async Task<byte[]> SignDataAsync(string keyIdentifier, byte[] data, string algorithm = "RS256", CancellationToken cancellationToken = default)
        {
            RecordOperation("SignData", keyIdentifier, new { Algorithm = algorithm, DataLength = data?.Length });

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(50, cancellationToken); // Simulate signing latency

            // Return mock signature
            return new byte[] { 0x01, 0x02, 0x03, 0x04 };
        }

        public async Task<byte[]> EncryptDataAsync(string keyIdentifier, byte[] data, string algorithm = "RSA-OAEP", CancellationToken cancellationToken = default)
        {
            RecordOperation("EncryptData", keyIdentifier, new { Algorithm = algorithm, DataLength = data?.Length });

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(30, cancellationToken);

            // Return mock encrypted data
            return new byte[] { 0x05, 0x06, 0x07, 0x08 };
        }

        public async Task<byte[]> DecryptDataAsync(string keyIdentifier, byte[] encryptedData, string algorithm = "RSA-OAEP", CancellationToken cancellationToken = default)
        {
            RecordOperation("DecryptData", keyIdentifier, new { Algorithm = algorithm, DataLength = encryptedData?.Length });

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(30, cancellationToken);

            // Return mock decrypted data
            return new byte[] { 0x09, 0x0A, 0x0B, 0x0C };
        }

        public async Task<bool> VerifySignatureAsync(string keyIdentifier, byte[] data, byte[] signature, string algorithm = "RS256", CancellationToken cancellationToken = default)
        {
            RecordOperation("VerifySignature", keyIdentifier, new { Algorithm = algorithm, DataLength = data?.Length, SignatureLength = signature?.Length });

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(40, cancellationToken);

            // Always return true for mock
            return true;
        }

        public async Task<string> GenerateKeyPairAsync(string keyName, HsmKeyType keyType = HsmKeyType.RSA, int keySize = 2048, CancellationToken cancellationToken = default)
        {
            RecordOperation("GenerateKeyPair", keyName, new { KeyType = keyType, KeySize = keySize });

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(100, cancellationToken); // Key generation takes longer

            var keyId = $"mock-key-{Guid.NewGuid():N}";
            _keys[keyId] = new HsmKeyInfo
            {
                KeyIdentifier = keyId,
                KeyName = keyName,
                KeyType = keyType,
                KeySize = keySize,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                Usage = HsmKeyUsage.All
            };

            return keyId;
        }

        public async Task<bool> DeleteKeyAsync(string keyIdentifier, CancellationToken cancellationToken = default)
        {
            RecordOperation("DeleteKey", keyIdentifier);

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(20, cancellationToken);

            return _keys.Remove(keyIdentifier);
        }

        public async Task<string[]> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            RecordOperation("ListKeys", null);

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(30, cancellationToken);

            return _keys.Keys.ToArray();
        }

        public async Task<HsmKeyInfo> GetKeyInfoAsync(string keyIdentifier, CancellationToken cancellationToken = default)
        {
            RecordOperation("GetKeyInfo", keyIdentifier);

            if (!_isConnected)
                throw new HsmOperationException("HSM not connected");

            await Task.Delay(15, cancellationToken);

            if (_keys.TryGetValue(keyIdentifier, out var keyInfo))
            {
                return keyInfo;
            }

            throw new HsmOperationException($"Key '{keyIdentifier}' not found");
        }

        public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
        {
            RecordOperation("TestConnectivity", null);
            await Task.Delay(5, cancellationToken);
            return _isConnected;
        }

        /// <summary>
        /// Adds a mock certificate
        /// </summary>
        public void AddCertificate(string identifier, X509Certificate2 certificate)
        {
            _certificates[identifier] = certificate;
        }

        /// <summary>
        /// Sets the connection status
        /// </summary>
        public void SetConnected(bool isConnected)
        {
            _isConnected = isConnected;
        }

        /// <summary>
        /// Gets all operations performed on this mock
        /// </summary>
        public IReadOnlyList<HsmOperation> GetOperations()
        {
            return _operations.AsReadOnly();
        }

        /// <summary>
        /// Resets the mock state
        /// </summary>
        public void Reset()
        {
            _certificates.Clear();
            _keys.Clear();
            _operations.Clear();
            _isConnected = true;
        }

        private void RecordOperation(string operation, string identifier, object parameters = null)
        {
            _operations.Add(new HsmOperation
            {
                Operation = operation,
                Identifier = identifier,
                Parameters = parameters,
                Timestamp = DateTime.UtcNow
            });
        }

        public class HsmOperation
        {
            public string Operation { get; set; }
            public string Identifier { get; set; }
            public object Parameters { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
