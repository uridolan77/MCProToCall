using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Utilities;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class TlsExtensionsTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private X509Certificate2 _serverCert;
        private X509Certificate2 _clientCert;
        private string _tempCertDir;

        public TlsExtensionsTests()
        {
            _mockLogger = new Mock<ILogger>();
            
            // Create a temp directory for test certificates
            _tempCertDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempCertDir);
            
            // Generate test certificates
            _serverCert = CertificateHelper.CreateSelfSignedCertificate(
                "TlsExtensionsTests-Server", 
                1, // 1 day validity
                "password",
                Path.Combine(_tempCertDir, "server.pfx"),
                _mockLogger.Object);
                
            _clientCert = CertificateHelper.CreateSelfSignedCertificate(
                "TlsExtensionsTests-Client", 
                1, // 1 day validity
                "password",
                Path.Combine(_tempCertDir, "client.pfx"),
                _mockLogger.Object);
        }
        
        [Fact]
        public void ValidateServerCertificate_ValidCertificate_ReturnsTrue()
        {
            // Arrange
            var chain = new X509Chain();
            
            // Act
            var result = TlsExtensions.ValidateServerCertificate(
                null, 
                _serverCert, 
                chain, 
                SslPolicyErrors.None,
                _mockLogger.Object);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateServerCertificate_InvalidCertificate_ReturnsFalse()
        {
            // Arrange
            var chain = new X509Chain();
            
            // Act
            var result = TlsExtensions.ValidateServerCertificate(
                null, 
                _serverCert, 
                chain, 
                SslPolicyErrors.RemoteCertificateChainErrors,
                _mockLogger.Object);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateClientCertificate_ValidCertificate_ReturnsTrue()
        {
            // Arrange
            var chain = new X509Chain();
            
            // Act
            var result = TlsExtensions.ValidateClientCertificate(
                null, 
                _clientCert, 
                chain, 
                SslPolicyErrors.None,
                null,
                _mockLogger.Object);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateClientCertificate_AllowedThumbprint_ReturnsTrue()
        {
            // Arrange
            var chain = new X509Chain();
            var allowedThumbprints = new[] { _clientCert.Thumbprint };
            
            // Act
            var result = TlsExtensions.ValidateClientCertificate(
                null, 
                _clientCert, 
                chain, 
                SslPolicyErrors.None,
                allowedThumbprints,
                _mockLogger.Object);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateClientCertificate_UnallowedThumbprint_ReturnsFalse()
        {
            // Arrange
            var chain = new X509Chain();
            var allowedThumbprints = new[] { "INVALID_THUMBPRINT" };
            
            // Act
            var result = TlsExtensions.ValidateClientCertificate(
                null, 
                _clientCert, 
                chain, 
                SslPolicyErrors.None,
                allowedThumbprints,
                _mockLogger.Object);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateClientCertificate_NullCertificate_ReturnsFalse()
        {
            // Arrange
            var chain = new X509Chain();
            
            // Act
            var result = TlsExtensions.ValidateClientCertificate(
                null, 
                null, 
                chain, 
                SslPolicyErrors.None,
                null,
                _mockLogger.Object);
            
            // Assert
            Assert.False(result);
        }
    }
}
