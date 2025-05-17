using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Utilities;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class CertificatePinningServiceTests
    {
        private readonly Mock<ILogger<CertificatePinningService>> _loggerMock;
        private readonly Mock<IOptions<TlsOptions>> _optionsMock;
        private readonly TlsOptions _tlsOptions;
        private readonly string _testCertPath;
        private X509Certificate2 _testCertificate;
        
        public CertificatePinningServiceTests()
        {
            _loggerMock = new Mock<ILogger<CertificatePinningService>>();
            _tlsOptions = new TlsOptions
            {
                UseCertificatePinning = true,
                CertificatePinStoragePath = Path.Combine(Path.GetTempPath(), "cert_pins_test"),
                RequireExactCertificateMatch = true
            };
            _optionsMock = new Mock<IOptions<TlsOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(_tlsOptions);
            
            // Create a test directory for pins
            if (!Directory.Exists(_tlsOptions.CertificatePinStoragePath))
            {
                Directory.CreateDirectory(_tlsOptions.CertificatePinStoragePath);
            }
            
            // Generate a test certificate
            var generator = new CertificateGenerator();
            _testCertificate = generator.GenerateSelfSignedCertificate(
                "CN=CertificatePinningTest", 
                DateTime.UtcNow, 
                DateTime.UtcNow.AddYears(1));
                
            // Save the certificate to a temporary file
            _testCertPath = Path.Combine(Path.GetTempPath(), "test_cert.pfx");
            File.WriteAllBytes(_testCertPath, _testCertificate.Export(X509ContentType.Pfx, "password"));
        }
        
        public void Dispose()
        {
            // Clean up
            _testCertificate?.Dispose();
            if (File.Exists(_testCertPath))
            {
                File.Delete(_testCertPath);
            }
            
            if (Directory.Exists(_tlsOptions.CertificatePinStoragePath))
            {
                Directory.Delete(_tlsOptions.CertificatePinStoragePath, true);
            }
        }
        
        [Fact]
        public void ValidateCertificatePin_ReturnsFalse_WhenCertificateIsNull()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.ValidateCertificatePin(null);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateCertificatePin_ReturnsTrue_WhenPinningDisabled()
        {
            // Arrange
            _tlsOptions.UseCertificatePinning = false;
            
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.ValidateCertificatePin(_testCertificate);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateCertificatePin_ReturnsFalse_WhenNoPinsMatch()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.ValidateCertificatePin(_testCertificate);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateCertificatePin_ReturnsTrue_WhenExactPinMatches()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
                
            // Add the certificate to pins
            service.AddCertificatePin(_testCertificate);
            
            // Act
            var result = service.ValidateCertificatePin(_testCertificate);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void AddCertificatePin_ReturnsFalse_WhenCertificateIsNull()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.AddCertificatePin(null);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void AddCertificatePin_ReturnsTrue_WhenCertificateValid()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.AddCertificatePin(_testCertificate);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void IsCertificatePinned_ReturnsFalse_WhenNoMatchingPin()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.IsCertificatePinned(_testCertificate.Thumbprint);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void IsCertificatePinned_ReturnsTrue_AfterAddingPin()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
                
            // Add the certificate
            service.AddCertificatePin(_testCertificate);
            
            // Act
            var result = service.IsCertificatePinned(_testCertificate.Thumbprint);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void RemoveCertificatePin_ReturnsFalse_WhenThumbprintEmpty()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.RemoveCertificatePin(string.Empty);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void RemoveCertificatePin_ReturnsFalse_WhenPinNotFound()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = service.RemoveCertificatePin("invalid-thumbprint");
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void RemoveCertificatePin_ReturnsTrue_WhenPinFound()
        {
            // Arrange
            var service = new CertificatePinningService(
                _loggerMock.Object,
                _optionsMock.Object);
                
            // Add the certificate
            service.AddCertificatePin(_testCertificate);
            
            // Act
            var result = service.RemoveCertificatePin(_testCertificate.Thumbprint);
            
            // Assert
            Assert.True(result);
            Assert.False(service.IsCertificatePinned(_testCertificate.Thumbprint));
        }
    }
}
