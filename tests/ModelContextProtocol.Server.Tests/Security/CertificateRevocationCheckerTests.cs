using System;
using System.IO;
using System.Net.Http;
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
    public class CertificateRevocationCheckerTests
    {
        private readonly Mock<ILogger<CertificateRevocationChecker>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IOptions<TlsOptions>> _optionsMock;
        private readonly TlsOptions _tlsOptions;
        private readonly string _testCertPath;
        private X509Certificate2 _testCertificate;
        private HttpClient _httpClient;
        
        public CertificateRevocationCheckerTests()
        {
            _loggerMock = new Mock<ILogger<CertificateRevocationChecker>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _tlsOptions = new TlsOptions
            {
                CheckCertificateRevocation = true,
                RevocationCheckMode = RevocationCheckMode.LocalCacheOnly,
                RevocationCachePath = Path.Combine(Path.GetTempPath(), "revocation_test")
            };
            _optionsMock = new Mock<IOptions<TlsOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(_tlsOptions);
            
            // Create test HTTP client
            _httpClient = new HttpClient();
            _httpClientFactoryMock
                .Setup(f => f.CreateClient("CrlDownloader"))
                .Returns(_httpClient);
            
            // Create a test directory for revocation cache
            if (!Directory.Exists(_tlsOptions.RevocationCachePath))
            {
                Directory.CreateDirectory(_tlsOptions.RevocationCachePath);
            }
            
            // Generate a test certificate
            var generator = new CertificateGenerator();
            _testCertificate = generator.GenerateSelfSignedCertificate(
                "CN=RevocationCheckerTest", 
                DateTime.UtcNow, 
                DateTime.UtcNow.AddYears(1));
                
            // Save the certificate to a temporary file
            _testCertPath = Path.Combine(Path.GetTempPath(), "test_cert_revocation.pfx");
            File.WriteAllBytes(_testCertPath, _testCertificate.Export(X509ContentType.Pfx, "password"));
        }
        
        public void Dispose()
        {
            // Clean up
            _testCertificate?.Dispose();
            _httpClient?.Dispose();
            
            if (File.Exists(_testCertPath))
            {
                File.Delete(_testCertPath);
            }
            
            if (Directory.Exists(_tlsOptions.RevocationCachePath))
            {
                Directory.Delete(_tlsOptions.RevocationCachePath, true);
            }
        }
        
        [Fact]
        public void ValidateCertificateNotRevoked_ReturnsFalse_WhenCertificateIsNull()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
            
            // Act
            var result = checker.ValidateCertificateNotRevoked(null);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateCertificateNotRevoked_ReturnsTrue_WhenCertificateNotInLocalCache()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
            
            // Act
            var result = checker.ValidateCertificateNotRevoked(_testCertificate);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateCertificateNotRevoked_ReturnsFalse_WhenCertificateInLocalCache()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
                
            // Add certificate to revocation list
            checker.AddToRevocationList(_testCertificate);
            
            // Act
            var result = checker.ValidateCertificateNotRevoked(_testCertificate);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void AddToRevocationList_ReturnsFalse_WhenCertificateNull()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
            
            // Act
            var result = checker.AddToRevocationList(null);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void AddToRevocationList_ReturnsTrue_ForValidCertificate()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
            
            // Act
            var result = checker.AddToRevocationList(_testCertificate);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void UpdateRevocationLists_ReturnsTrue()
        {
            // Arrange
            var checker = new CertificateRevocationChecker(
                _loggerMock.Object,
                _optionsMock.Object,
                _httpClientFactoryMock.Object);
            
            // Act
            var result = checker.UpdateRevocationLists();
            
            // Assert
            Assert.True(result);
        }
    }
}
