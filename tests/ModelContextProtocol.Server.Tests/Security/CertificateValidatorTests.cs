using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class CertificateValidatorTests
    {
        private readonly Mock<ILogger<CertificateValidator>> _loggerMock;
        private readonly Mock<ICertificateRevocationChecker> _revocationCheckerMock;
        private readonly Mock<ICertificatePinningService> _pinningServiceMock;
        private readonly Mock<IOptions<TlsOptions>> _optionsMock;
        private readonly TlsOptions _tlsOptions;
        
        public CertificateValidatorTests()
        {
            _loggerMock = new Mock<ILogger<CertificateValidator>>();
            _revocationCheckerMock = new Mock<ICertificateRevocationChecker>();
            _pinningServiceMock = new Mock<ICertificatePinningService>();
            _tlsOptions = new TlsOptions
            {
                CheckCertificateRevocation = true,
                UseCertificatePinning = true,
                AllowUntrustedCertificates = false
            };
            _optionsMock = new Mock<IOptions<TlsOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(_tlsOptions);
        }
        
        [Fact]
        public void ValidateCertificate_ReturnsFalse_WhenCertificateIsNull()
        {
            // Arrange
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateCertificate(null, null, SslPolicyErrors.None);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateCertificate_ReturnsTrue_WhenAllowUntrustedCertificatesInDevelopment()
        {
            // Arrange
            var prevEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            
            _tlsOptions.AllowUntrustedCertificates = true;
            
            var certificate = new X509Certificate2();
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            try
            {
                // Act
                var result = validator.ValidateCertificate(certificate, null, SslPolicyErrors.None);
                
                // Assert
                Assert.True(result);
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", prevEnv);
            }
        }
        
        [Fact]
        public void ValidateCertificate_ReturnsFalse_WhenSslPolicyErrors()
        {
            // Arrange
            var certificate = new X509Certificate2();
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateCertificate(certificate, null, SslPolicyErrors.RemoteCertificateNameMismatch);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateCertificate_ChecksRevocation_WhenEnabled()
        {
            // Arrange
            var certificate = new X509Certificate2();
            _tlsOptions.CheckCertificateRevocation = true;
            
            _revocationCheckerMock
                .Setup(r => r.ValidateCertificateNotRevoked(certificate))
                .Returns(true);
                
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            validator.ValidateCertificate(certificate, null, SslPolicyErrors.None);
            
            // Assert
            _revocationCheckerMock.Verify(r => r.ValidateCertificateNotRevoked(certificate), Times.Once);
        }
        
        [Fact]
        public void ValidateCertificate_ReturnsFalse_WhenCertificateIsRevoked()
        {
            // Arrange
            var certificate = new X509Certificate2();
            _tlsOptions.CheckCertificateRevocation = true;
            
            _revocationCheckerMock
                .Setup(r => r.ValidateCertificateNotRevoked(certificate))
                .Returns(false);
                
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateCertificate(certificate, null, SslPolicyErrors.None);
            
            // Assert
            Assert.False(result);
            _revocationCheckerMock.Verify(r => r.ValidateCertificateNotRevoked(certificate), Times.Once);
        }
        
        [Fact]
        public void ValidateCertificate_ChecksPinning_WhenEnabled()
        {
            // Arrange
            var certificate = new X509Certificate2();
            _tlsOptions.UseCertificatePinning = true;
            _tlsOptions.CheckCertificateRevocation = false;
            
            _pinningServiceMock
                .Setup(p => p.ValidateCertificatePin(certificate))
                .Returns(true);
                
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            validator.ValidateCertificate(certificate, null, SslPolicyErrors.None);
            
            // Assert
            _pinningServiceMock.Verify(p => p.ValidateCertificatePin(certificate), Times.Once);
        }
        
        [Fact]
        public void ValidateCertificate_ReturnsFalse_WhenPinValidationFails()
        {
            // Arrange
            var certificate = new X509Certificate2();
            _tlsOptions.UseCertificatePinning = true;
            _tlsOptions.CheckCertificateRevocation = false;
            
            _pinningServiceMock
                .Setup(p => p.ValidateCertificatePin(certificate))
                .Returns(false);
                
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateCertificate(certificate, null, SslPolicyErrors.None);
            
            // Assert
            Assert.False(result);
            _pinningServiceMock.Verify(p => p.ValidateCertificatePin(certificate), Times.Once);
        }
        
        [Fact]
        public void ValidateClientCertificate_ReturnsTrue_WhenClientCertificateNotRequired()
        {
            // Arrange
            _tlsOptions.RequireClientCertificate = false;
            
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateClientCertificate(null, null, null, SslPolicyErrors.None);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateClientCertificate_ReturnsFalse_WhenClientCertificateRequiredButNotProvided()
        {
            // Arrange
            _tlsOptions.RequireClientCertificate = true;
            
            var validator = new CertificateValidator(
                _loggerMock.Object,
                _revocationCheckerMock.Object,
                _pinningServiceMock.Object,
                _optionsMock.Object);
            
            // Act
            var result = validator.ValidateClientCertificate(null, null, null, SslPolicyErrors.None);
            
            // Assert
            Assert.False(result);
        }
    }
}
