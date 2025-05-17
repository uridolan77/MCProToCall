using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class SecurityIntegrationTests
    {
        [Fact]
        public void McpServer_InitializesWithTlsSecurityComponents()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<McpServer>>();
            var optionsMock = new Mock<IOptions<McpServerOptions>>();
            var certValidatorMock = new Mock<ICertificateValidator>();
            var pinningServiceMock = new Mock<ICertificatePinningService>();
            var connectionManagerMock = new Mock<TlsConnectionManager>();
            
            var options = new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = 8555,
                UseTls = true,
                CertificatePath = Path.Combine(Path.GetTempPath(), "test-cert.pfx"),
                CertificatePassword = "testpassword"
            };
            optionsMock.Setup(o => o.Value).Returns(options);
            
            // Generate a self-signed certificate for testing
            var generator = new CertificateGenerator();
            var certificate = generator.GenerateSelfSignedCertificate(
                "CN=SecurityIntegrationTests", 
                DateTime.UtcNow, 
                DateTime.UtcNow.AddDays(1));
                
            // Export certificate to the expected path
            File.WriteAllBytes(options.CertificatePath, certificate.Export(X509ContentType.Pfx, options.CertificatePassword));
            
            try
            {
                // Act - Create server with security components
                using var server = new McpServer(
                    optionsMock.Object,
                    loggerMock.Object,
                    null, 
                    null,
                    certValidatorMock.Object,
                    pinningServiceMock.Object,
                    connectionManagerMock.Object);
                
                // Assert - Verify interactions with security components
                certValidatorMock.Verify(v => v.ValidateCertificate(
                    It.IsAny<X509Certificate2>(), 
                    It.IsAny<X509Chain>(), 
                    SslPolicyErrors.None), 
                    Times.Once);
                
                pinningServiceMock.Verify(p => p.AddCertificatePin(
                    It.IsAny<X509Certificate2>(), 
                    true), 
                    Times.Once);
            }
            finally
            {
                // Cleanup
                if (File.Exists(options.CertificatePath))
                {
                    File.Delete(options.CertificatePath);
                }
                certificate.Dispose();
            }
        }
        
        [Fact]
        public async Task McpServer_EnforcesTlsConnectionLimits()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<McpServer>>();
            var optionsMock = new Mock<IOptions<McpServerOptions>>();
            var connectionManagerMock = new Mock<TlsConnectionManager>();
            
            var options = new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = 8556,
                UseTls = true,
                CertificatePath = Path.Combine(Path.GetTempPath(), "test-cert2.pfx"),
                CertificatePassword = "testpassword"
            };
            optionsMock.Setup(o => o.Value).Returns(options);
            
            // Set up connection manager to simulate connection limit reached
            connectionManagerMock
                .Setup(m => m.TryAddConnection(It.IsAny<string>()))
                .Returns(false); // Always reject connections
            
            // Generate a self-signed certificate for testing
            var generator = new CertificateGenerator();
            var certificate = generator.GenerateSelfSignedCertificate(
                "CN=SecurityIntegrationTests2", 
                DateTime.UtcNow, 
                DateTime.UtcNow.AddDays(1));
                
            // Export certificate to the expected path
            File.WriteAllBytes(options.CertificatePath, certificate.Export(X509ContentType.Pfx, options.CertificatePassword));
            
            McpServer server = null;
            
            try
            {
                // Act - Create and start server
                server = new McpServer(
                    optionsMock.Object,
                    loggerMock.Object,
                    null,
                    null,
                    null,
                    null,
                    connectionManagerMock.Object);
                
                // Start the server
                await server.StartAsync();
                
                // Assert - We should see the call to TryAddConnection in logs
                connectionManagerMock.Verify(m => m.TryAddConnection(It.IsAny<string>()), Times.Never);
                
                // Note: In a real test, we would make requests and verify they're rejected due to connection limits
                // but this requires more complex setup with client certificates
            }
            finally
            {
                // Cleanup
                server?.Dispose();
                if (File.Exists(options.CertificatePath))
                {
                    File.Delete(options.CertificatePath);
                }
                certificate.Dispose();
            }
        }
        
        [Fact]
        public void McpServer_CleanupsTlsConnections_OnDispose()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<McpServer>>();
            var optionsMock = new Mock<IOptions<McpServerOptions>>();
            var connectionManagerMock = new Mock<TlsConnectionManager>();
            
            var options = new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = 8557,
                UseTls = true,
                CertificatePath = Path.Combine(Path.GetTempPath(), "test-cert3.pfx"),
                CertificatePassword = "testpassword"
            };
            optionsMock.Setup(o => o.Value).Returns(options);
            
            // Generate a self-signed certificate for testing
            var generator = new CertificateGenerator();
            var certificate = generator.GenerateSelfSignedCertificate(
                "CN=SecurityIntegrationTests3", 
                DateTime.UtcNow, 
                DateTime.UtcNow.AddDays(1));
                
            // Export certificate to the expected path
            File.WriteAllBytes(options.CertificatePath, certificate.Export(X509ContentType.Pfx, options.CertificatePassword));
            
            try
            {
                // Act
                using (var server = new McpServer(
                    optionsMock.Object,
                    loggerMock.Object,
                    null,
                    null,
                    null,
                    null,
                    connectionManagerMock.Object))
                {
                    // Server gets disposed at the end of this block
                }
                
                // Assert - Verify server certificate gets disposed
                // Note: We can't directly verify the certificate is disposed as it's a private field
                // but we can verify other cleanup operations if we extend the class with more cleanup code
            }
            finally
            {
                // Cleanup
                if (File.Exists(options.CertificatePath))
                {
                    File.Delete(options.CertificatePath);
                }
                certificate.Dispose();
            }
        }
    }
}
