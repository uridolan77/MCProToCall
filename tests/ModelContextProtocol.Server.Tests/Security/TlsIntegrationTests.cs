using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Utilities;
using ModelContextProtocol.Server;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class TlsIntegrationTests : IDisposable
    {
        private readonly IServiceProvider _serverServices;
        private readonly IServiceProvider _clientServices;
        private readonly IMcpServer _server;
        private readonly IMcpClient _client;
        private readonly X509Certificate2 _serverCert;
        private readonly X509Certificate2 _clientCert;
        private readonly string _tempCertDir;

        public TlsIntegrationTests()
        {
            // Create test certificates
            _tempCertDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempCertDir);
            
            string serverCertPath = Path.Combine(_tempCertDir, "server.pfx");
            string clientCertPath = Path.Combine(_tempCertDir, "client.pfx");
            
            // Create logger factory for tests
            var loggerFactory = LoggerFactory.Create(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger<TlsIntegrationTests>();
            
            // Generate test certificates
            _serverCert = CertificateHelper.CreateSelfSignedCertificate(
                "TlsIntegrationTests-Server", 
                1, // 1 day validity
                "password",
                serverCertPath,
                logger);
                
            _clientCert = CertificateHelper.CreateSelfSignedCertificate(
                "TlsIntegrationTests-Client", 
                1, // 1 day validity
                "password",
                clientCertPath,
                logger);
              // Configure server
            var serverServices = new ServiceCollection();
            
            serverServices.AddLogging(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            // Configure TLS options
            var tlsOptions = new TlsOptions
            {
                CertificatePath = serverCertPath,
                CertificatePassword = "password",
                RequireClientCertificate = true,
                AllowedClientCertificateThumbprints = new List<string> { _clientCert.Thumbprint },
                CheckCertificateRevocation = true,
                RevocationCheckMode = RevocationCheckMode.LocalCacheOnly,
                RevocationCachePath = Path.Combine(_tempCertDir, "revocation"),
                UseCertificatePinning = true,
                PinnedCertificates = new List<string> { clientCertPath },
                CertificatePinStoragePath = Path.Combine(_tempCertDir, "pins"),
                MaxConnectionsPerIpAddress = 10,
                AllowUntrustedCertificates = true // For tests only
            };
            
            // Configure server with TLS
            serverServices.AddSingleton<McpServerOptions>(new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = 8443, // Different port for tests
                UseTls = true
            });
            
            // Create directories needed for tests
            Directory.CreateDirectory(tlsOptions.RevocationCachePath);
            Directory.CreateDirectory(tlsOptions.CertificatePinStoragePath);
            
            // Register TLS services
            serverServices.AddSingleton<IOptions<TlsOptions>>(Options.Create(tlsOptions));
            serverServices.AddSingleton<ICertificateValidator, CertificateValidator>();
            serverServices.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            serverServices.AddSingleton<ICertificatePinningService, CertificatePinningService>();
            serverServices.AddSingleton<TlsConnectionManager>();
            
            // Required for revocation checker
            serverServices.AddHttpClient("CrlDownloader");
            
            serverServices.AddMcpServer();
            
            _serverServices = serverServices.BuildServiceProvider();
            _server = _serverServices.GetRequiredService<IMcpServer>();
            
            // Register a test method
            ((McpServer)_server).RegisterPublicMethod("system.test", async (param) => 
            {
                return "Test successful";
            });
            
            // Start the server
            _server.StartAsync().Wait();
            
            // Configure client
            var clientServices = new ServiceCollection();
            
            clientServices.AddLogging(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
              // Configure client with TLS
            var clientOptions = new McpClientOptions
            {
                Host = "127.0.0.1",
                Port = 8443, // Same port as server
                UseTls = true,
                ClientCertificatePath = clientCertPath,
                ClientCertificatePassword = "password",
                AllowUntrustedServerCertificate = true, // For test we allow self-signed
                ServerCertificatePinPath = serverCertPath,
                EnableCertificatePinning = true,
                EnableRevocationCheck = true,
                EnableDetailedTlsLogging = true
            };
            
            clientServices.AddSingleton(clientOptions);
            
            // Add certificate validation services
            clientServices.AddSingleton<ICertificateValidator, CertificateValidator>();
            clientServices.AddSingleton<ICertificatePinningService, CertificatePinningService>();
            clientServices.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            
            clientServices.AddMcpClient(clientOptions);
            
            _clientServices = clientServices.BuildServiceProvider();
            _client = _clientServices.GetRequiredService<IMcpClient>();
        }
          [Fact]
        public async Task SecureConnection_WithMutualTls_SuccessfullyConnects()
        {
            // Arrange
            // Server and client are already configured with mutual TLS
            
            // Act
            string result = await _client.CallMethodAsync<string>("system.test");
            
            // Assert
            Assert.Equal("Test successful", result);
        }
        
        [Fact]
        public async Task SecureConnection_WithCertificatePinning_SuccessfullyConnects()
        {
            // Arrange
            // Get the certificate pinning service
            var pinningService = _clientServices.GetRequiredService<ICertificatePinningService>();
            
            // Import server certificate for pinning
            var serverCert = new X509Certificate2(_serverCert);
            pinningService.AddCertificatePin(serverCert, true);
            
            // Act
            string result = await _client.CallMethodAsync<string>("system.test");
            
            // Assert
            Assert.Equal("Test successful", result);
        }
        
        [Fact]
        public async Task SecureConnection_WithRevocationChecker_SuccessfullyConnects()
        {
            // Arrange
            // Get the revocation checker service
            var revocationChecker = _serverServices.GetRequiredService<ICertificateRevocationChecker>();
            
            // Ensure the client certificate is not revoked
            bool isNotRevoked = revocationChecker.ValidateCertificateNotRevoked(_clientCert);
            Assert.True(isNotRevoked, "Client certificate should not be revoked");
            
            // Act
            string result = await _client.CallMethodAsync<string>("system.test");
            
            // Assert
            Assert.Equal("Test successful", result);
        }
        
        [Fact]
        public async Task SecureConnection_WithConnectionManager_LimitsConnections()
        {
            // Arrange
            var connectionManager = _serverServices.GetRequiredService<TlsConnectionManager>();
            string ip = "127.0.0.1";
            
            // Add some test connections
            for (int i = 0; i < 5; i++)
            {
                connectionManager.TryAddConnection(ip);
            }
            
            // Act & Assert
            // Should still be able to connect since we haven't reached the limit (10)
            string result = await _client.CallMethodAsync<string>("system.test");
            Assert.Equal("Test successful", result);
            
            // Check the current connection count
            int count = connectionManager.GetActiveConnectionCount(ip);
            Assert.True(count >= 5, "Should have at least 5 connections");
        }
        
        public void Dispose()
        {
            // Stop the server
            ((McpServer)_server).Stop();
            
            // Clean up certificates
            try
            {
                Directory.Delete(_tempCertDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
