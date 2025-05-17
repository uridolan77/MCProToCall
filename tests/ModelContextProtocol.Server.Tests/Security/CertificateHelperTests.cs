using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Security;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class CertificateHelperTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private string _tempCertDir;
        
        public CertificateHelperTests()
        {
            _mockLogger = new Mock<ILogger>();
            
            // Create a temp directory for test certificates
            _tempCertDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempCertDir);
        }
        
        [Fact]
        public void CreateSelfSignedCertificate_ValidParameters_CreatesCertificate()
        {
            // Arrange
            string certPath = Path.Combine(_tempCertDir, "test-cert.pfx");
            
            // Act
            var cert = CertificateHelper.CreateSelfSignedCertificate(
                "TestCertificate",
                1, // 1 day validity
                "password",
                certPath,
                _mockLogger.Object);
            
            // Assert
            Assert.NotNull(cert);
            Assert.True(File.Exists(certPath));
            Assert.Equal("CN=TestCertificate", cert.Subject);
            
            // Validate we can load the certificate back
            var loadedCert = new X509Certificate2(certPath, "password");
            Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);
        }
        
        [Fact]
        public void LoadCertificateFromFile_ValidCertificate_LoadsCertificate()
        {
            // Arrange
            string certPath = Path.Combine(_tempCertDir, "load-test.pfx");
            
            // First create a certificate
            var originalCert = CertificateHelper.CreateSelfSignedCertificate(
                "LoadTestCertificate",
                1,
                "password",
                certPath,
                _mockLogger.Object);
            
            // Act
            var loadedCert = CertificateHelper.LoadCertificateFromFile(
                certPath,
                "password",
                _mockLogger.Object);
            
            // Assert
            Assert.NotNull(loadedCert);
            Assert.Equal(originalCert.Thumbprint, loadedCert.Thumbprint);
        }
        
        [Fact]
        public void LoadCertificateFromFile_FileNotFound_ThrowsException()
        {
            // Arrange
            string nonExistentPath = Path.Combine(_tempCertDir, "non-existent.pfx");
            
            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() => 
                CertificateHelper.LoadCertificateFromFile(
                    nonExistentPath,
                    "password",
                    _mockLogger.Object));
                    
            Assert.Contains(nonExistentPath, exception.Message);
        }
        
        [Fact]
        public void LoadCertificateFromFile_InvalidPassword_ThrowsException()
        {
            // Arrange
            string certPath = Path.Combine(_tempCertDir, "password-test.pfx");
            
            // First create a certificate
            CertificateHelper.CreateSelfSignedCertificate(
                "PasswordTestCertificate",
                1,
                "correctpassword",
                certPath,
                _mockLogger.Object);
            
            // Act & Assert
            Assert.Throws<CryptographicException>(() => 
                CertificateHelper.LoadCertificateFromFile(
                    certPath,
                    "wrongpassword",
                    _mockLogger.Object));
        }
    }
}
