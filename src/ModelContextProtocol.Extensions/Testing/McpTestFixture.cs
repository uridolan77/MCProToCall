using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Server;
using Xunit;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Test fixture for integration testing MCP servers and clients
    /// </summary>
    public class McpTestFixture : IAsyncLifetime
    {
        private readonly Dictionary<string, object> _configuration;
        private readonly List<IMcpServer> _servers;
        private readonly List<X509Certificate2> _testCertificates;
        private readonly string _tempDirectory;
        private IServiceProvider _serviceProvider;
        private IHost _host;

        public McpTestFixture()
        {
            _configuration = new Dictionary<string, object>();
            _servers = new List<IMcpServer>();
            _testCertificates = new List<X509Certificate2>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"McpTest_{Guid.NewGuid()}");

            Directory.CreateDirectory(_tempDirectory);
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "certificates"));
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "logs"));
        }

        /// <summary>
        /// Available ports for testing
        /// </summary>
        public static class TestPorts
        {
            public const int BasePort = 19000;
            public const int SecurePort = 19443;
            public const int WebSocketPort = 19001;
            public const int MetricsPort = 19002;
        }

        /// <summary>
        /// Test configuration values
        /// </summary>
        public IReadOnlyDictionary<string, object> Configuration => _configuration;

        /// <summary>
        /// Service provider for dependency injection
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// Temporary directory for test files
        /// </summary>
        public string TempDirectory => _tempDirectory;

        /// <summary>
        /// Creates a test MCP server with specified configuration
        /// </summary>
        /// <param name="configureOptions">Server configuration action</param>
        /// <param name="configureServices">Service configuration action</param>
        /// <returns>Configured MCP server</returns>
        public async Task<IMcpServer> CreateServerAsync(
            Action<McpServerOptions> configureOptions = null,
            Action<IServiceCollection> configureServices = null)
        {
            var services = new ServiceCollection();

            // Add default services
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            services.AddSingleton<IConfiguration>(BuildConfiguration());

            // Configure server options
            var options = new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = GetNextAvailablePort(),
                UseTls = false
            };

            configureOptions?.Invoke(options);
            services.Configure<McpServerOptions>(opts =>
            {
                opts.Host = options.Host;
                opts.Port = options.Port;
                opts.UseTls = options.UseTls;
                opts.CertificatePath = options.CertificatePath;
                opts.CertificatePassword = options.CertificatePassword;
                opts.EnableAuthentication = options.EnableAuthentication;
                opts.CheckCertificateRevocation = options.CheckCertificateRevocation;
                opts.MaxConcurrentConnections = options.MaxConcurrentConnections;
                opts.RequestTimeout = options.RequestTimeout;
            });

            // Add MCP server services
            services.AddSingleton<IMcpServer, McpServer>();

            // Allow additional service configuration
            configureServices?.Invoke(services);

            var serviceProvider = services.BuildServiceProvider();
            var server = serviceProvider.GetRequiredService<IMcpServer>();

            _servers.Add(server);
            return server;
        }

        /// <summary>
        /// Creates a test MCP server with TLS enabled
        /// </summary>
        /// <param name="configureOptions">Additional server configuration</param>
        /// <param name="configureServices">Service configuration action</param>
        /// <returns>Configured secure MCP server</returns>
        public async Task<IMcpServer> CreateSecureServerAsync(
            Action<McpServerOptions> configureOptions = null,
            Action<IServiceCollection> configureServices = null)
        {
            var certificate = await CreateTestCertificateAsync();
            var certPath = Path.Combine(_tempDirectory, "certificates", $"server_{Guid.NewGuid()}.pfx");
            var certPassword = "TestPassword123!";

            await File.WriteAllBytesAsync(certPath, certificate.Export(X509ContentType.Pfx, certPassword));

            return await CreateServerAsync(options =>
            {
                options.UseTls = true;
                options.CertificatePath = certPath;
                options.CertificatePassword = certPassword;
                options.Port = GetNextAvailablePort(TestPorts.SecurePort);
                configureOptions?.Invoke(options);
            }, services =>
            {
                // Add security services
                services.AddSingleton<ICertificateValidator, CertificateValidator>();
                services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
                services.AddSingleton<ICertificatePinningService, CertificatePinningService>();
                configureServices?.Invoke(services);
            });
        }

        /// <summary>
        /// Creates an HTTP client configured for testing
        /// </summary>
        /// <param name="useTls">Whether to use HTTPS</param>
        /// <param name="ignoreSSLErrors">Whether to ignore SSL certificate errors</param>
        /// <returns>Configured HTTP client</returns>
        public HttpClient CreateTestHttpClient(bool useTls = false, bool ignoreSSLErrors = true)
        {
            var handler = new HttpClientHandler();

            if (useTls && ignoreSSLErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            return client;
        }

        /// <summary>
        /// Creates a test certificate for TLS testing
        /// </summary>
        /// <param name="subject">Certificate subject</param>
        /// <param name="validFrom">Certificate valid from date</param>
        /// <param name="validTo">Certificate valid to date</param>
        /// <returns>Test certificate</returns>
        public async Task<X509Certificate2> CreateTestCertificateAsync(
            string subject = "CN=TestServer",
            DateTime? validFrom = null,
            DateTime? validTo = null)
        {
            validFrom ??= DateTime.UtcNow.AddDays(-1);
            validTo ??= DateTime.UtcNow.AddDays(30);

            var generator = new CertificateGenerator();
            var certificate = generator.GenerateSelfSignedCertificate(
                subject,
                validFrom.Value,
                validTo.Value);

            _testCertificates.Add(certificate);
            return certificate;
        }

        /// <summary>
        /// Configures the test environment with specific settings
        /// </summary>
        /// <param name="key">Configuration key</param>
        /// <param name="value">Configuration value</param>
        public void SetConfiguration(string key, object value)
        {
            _configuration[key] = value;
        }

        /// <summary>
        /// Waits for a server to be ready to accept connections
        /// </summary>
        /// <param name="server">Server to wait for</param>
        /// <param name="timeout">Maximum wait time</param>
        /// <returns>True if server is ready, false if timeout</returns>
        public async Task<bool> WaitForServerReadyAsync(IMcpServer server, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(30);
            var cancellationToken = new CancellationTokenSource(timeout.Value).Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (server.IsListening)
                    {
                        return true;
                    }

                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a mock configuration for testing
        /// </summary>
        /// <returns>Test configuration</returns>
        private IConfiguration BuildConfiguration()
        {
            var configBuilder = new ConfigurationBuilder();

            // Add in-memory configuration
            var configPairs = _configuration.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value?.ToString()));
            configBuilder.AddInMemoryCollection(configPairs);

            // Add test-specific configuration
            var testConfig = new Dictionary<string, string>
            {
                ["Logging:LogLevel:Default"] = "Debug",
                ["Logging:LogLevel:Microsoft"] = "Warning",
                ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Information",
                ["McpServer:Host"] = "127.0.0.1",
                ["McpServer:Port"] = TestPorts.BasePort.ToString(),
                ["McpServer:UseTls"] = "false",
                ["McpServer:MaxConcurrentConnections"] = "100",
                ["McpServer:RequestTimeout"] = "00:00:30",
                ["McpServer:EnableAuthentication"] = "false"
            };

            configBuilder.AddInMemoryCollection(testConfig);

            return configBuilder.Build();
        }

        /// <summary>
        /// Gets the next available port for testing
        /// </summary>
        /// <param name="startPort">Starting port number</param>
        /// <returns>Available port number</returns>
        private int GetNextAvailablePort(int startPort = TestPorts.BasePort)
        {
            // Simple port allocation for testing
            // In a real implementation, you might want to check if ports are actually available
            return startPort + _servers.Count;
        }

        /// <summary>
        /// Initialize the test fixture
        /// </summary>
        public async Task InitializeAsync()
        {
            // Build service provider for shared services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            services.AddSingleton<IConfiguration>(BuildConfiguration());

            _serviceProvider = services.BuildServiceProvider();

            await Task.CompletedTask;
        }

        /// <summary>
        /// Clean up test resources
        /// </summary>
        public async Task DisposeAsync()
        {
            // Stop all servers
            foreach (var server in _servers)
            {
                try
                {
                    server.Stop();
                    if (server is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception)
                {
                    // Ignore errors during cleanup
                }
            }

            // Dispose certificates
            foreach (var certificate in _testCertificates)
            {
                try
                {
                    certificate.Dispose();
                }
                catch (Exception)
                {
                    // Ignore errors during cleanup
                }
            }

            // Clean up temporary directory
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch (Exception)
            {
                // Ignore errors during cleanup
            }

            // Dispose service provider
            if (_serviceProvider is IDisposable serviceDisposable)
            {
                serviceDisposable.Dispose();
            }

            _servers.Clear();
            _testCertificates.Clear();
            _configuration.Clear();

            await Task.CompletedTask;
        }
    }
}
