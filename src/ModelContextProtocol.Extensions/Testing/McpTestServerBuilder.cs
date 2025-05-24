using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.HSM;
using ModelContextProtocol.Extensions.Testing.Doubles;
using ModelContextProtocol.Extensions.Testing.Chaos;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Builder for creating test MCP servers with configurable test doubles and chaos policies
    /// </summary>
    public class McpTestServerBuilder
    {
        private readonly IServiceCollection _services;
        private readonly Dictionary<string, object> _configurationValues = new();
        private readonly List<Action<IServiceCollection>> _serviceConfigurations = new();
        private bool _enableChaos = false;
        private ChaosConfiguration _chaosConfig = new();

        public McpTestServerBuilder()
        {
            _services = new ServiceCollection();

            // Add basic test services
            _services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            _services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        }

        /// <summary>
        /// Configures services for the test server
        /// </summary>
        public McpTestServerBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            _serviceConfigurations.Add(configure);
            return this;
        }

        /// <summary>
        /// Adds configuration values
        /// </summary>
        public McpTestServerBuilder WithConfiguration(string key, object value)
        {
            _configurationValues[key] = value;
            return this;
        }

        /// <summary>
        /// Adds configuration section
        /// </summary>
        public McpTestServerBuilder WithConfigurationSection(string sectionName, object sectionData)
        {
            _configurationValues[sectionName] = sectionData;
            return this;
        }

        /// <summary>
        /// Uses test time provider for deterministic time-based testing
        /// </summary>
        public McpTestServerBuilder UseTestTimeProvider(DateTime? fixedTime = null)
        {
            _services.AddSingleton<ITimeProvider>(new TestTimeProvider(fixedTime ?? DateTime.UtcNow));
            return this;
        }

        /// <summary>
        /// Uses mock certificate validator
        /// </summary>
        public McpTestServerBuilder UseMockCertificateValidator(bool alwaysValid = true)
        {
            _services.AddSingleton<ICertificateValidator>(new MockCertificateValidator(alwaysValid));
            return this;
        }

        /// <summary>
        /// Uses mock HSM
        /// </summary>
        public McpTestServerBuilder UseMockHsm()
        {
            _services.AddSingleton<IHardwareSecurityModule, MockHardwareSecurityModule>();
            return this;
        }

        /// <summary>
        /// Enables chaos testing with default configuration
        /// </summary>
        public McpTestServerBuilder EnableChaos()
        {
            _enableChaos = true;
            return this;
        }

        /// <summary>
        /// Enables chaos testing with custom configuration
        /// </summary>
        public McpTestServerBuilder EnableChaos(Action<ChaosConfigurationBuilder> configure)
        {
            _enableChaos = true;
            var builder = new ChaosConfigurationBuilder();
            configure?.Invoke(builder);
            _chaosConfig = builder.Build();
            return this;
        }

        /// <summary>
        /// Uses in-memory configuration
        /// </summary>
        public McpTestServerBuilder UseInMemoryConfiguration()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(_configurationValues.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString()));

            var configuration = configBuilder.Build();
            _services.AddSingleton<IConfiguration>(configuration);
            return this;
        }

        /// <summary>
        /// Adds test-specific TLS configuration
        /// </summary>
        public McpTestServerBuilder WithTestTlsConfiguration()
        {
            var tlsOptions = new TlsOptions
            {
                UseTls = false, // Disable TLS for testing by default
                AllowUntrustedCertificates = true,
                AllowSelfSignedCertificates = true,
                CheckCertificateRevocation = false,
                CertificatePinning = new CertificatePinningOptions { Enabled = false },
                RevocationOptions = new CertificateRevocationOptions { CheckRevocation = false },
                CertificateTransparencyOptions = new CertificateTransparencyOptions { VerifyCertificateTransparency = false }
            };

            _services.Configure<TlsOptions>(options =>
            {
                options.UseTls = tlsOptions.UseTls;
                options.AllowUntrustedCertificates = tlsOptions.AllowUntrustedCertificates;
                options.AllowSelfSignedCertificates = tlsOptions.AllowSelfSignedCertificates;
                options.CheckCertificateRevocation = tlsOptions.CheckCertificateRevocation;
                options.CertificatePinning = tlsOptions.CertificatePinning;
                options.RevocationOptions = tlsOptions.RevocationOptions;
                options.CertificateTransparencyOptions = tlsOptions.CertificateTransparencyOptions;
            });

            return this;
        }

        /// <summary>
        /// Builds the test server
        /// </summary>
        public McpTestServer Build()
        {
            // Apply service configurations
            foreach (var configure in _serviceConfigurations)
            {
                configure(_services);
            }

            // Add chaos policy if enabled
            if (_enableChaos)
            {
                _services.AddSingleton(_chaosConfig);
                _services.AddSingleton<IChaosPolicy, ConfigurableChaosPolicy>();
            }

            // Ensure configuration is available
            if (!_services.Any(s => s.ServiceType == typeof(IConfiguration)))
            {
                UseInMemoryConfiguration();
            }

            // Build service provider
            var serviceProvider = _services.BuildServiceProvider();

            return new McpTestServer(serviceProvider, _chaosConfig);
        }
    }

    /// <summary>
    /// Test MCP server for integration testing
    /// </summary>
    public class McpTestServer : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ChaosConfiguration _chaosConfig;
        private bool _disposed;

        internal McpTestServer(IServiceProvider serviceProvider, ChaosConfiguration chaosConfig)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _chaosConfig = chaosConfig ?? new ChaosConfiguration();
        }

        /// <summary>
        /// Gets a service from the test server
        /// </summary>
        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service from the test server
        /// </summary>
        public T GetRequiredService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets the service provider
        /// </summary>
        public IServiceProvider Services => _serviceProvider;

        /// <summary>
        /// Gets the chaos configuration
        /// </summary>
        public ChaosConfiguration ChaosConfiguration => _chaosConfig;

        /// <summary>
        /// Executes an operation with chaos injection
        /// </summary>
        public async Task<T> ExecuteWithChaosAsync<T>(Func<Task<T>> operation)
        {
            var chaosPolicy = _serviceProvider.GetService<IChaosPolicy>();
            if (chaosPolicy != null)
            {
                return await chaosPolicy.ExecuteAsync(operation);
            }

            return await operation();
        }

        /// <summary>
        /// Executes an operation with chaos injection
        /// </summary>
        public async Task ExecuteWithChaosAsync(Func<Task> operation)
        {
            var chaosPolicy = _serviceProvider.GetService<IChaosPolicy>();
            if (chaosPolicy != null)
            {
                await chaosPolicy.ExecuteAsync(async () =>
                {
                    await operation();
                    return Task.CompletedTask;
                });
            }
            else
            {
                await operation();
            }
        }

        /// <summary>
        /// Resets all test doubles to their initial state
        /// </summary>
        public void Reset()
        {
            // Reset time provider
            var timeProvider = _serviceProvider.GetService<TestTimeProvider>();
            timeProvider?.Reset();

            // Reset mock services
            var mockValidator = _serviceProvider.GetService<MockCertificateValidator>();
            mockValidator?.Reset();

            var mockHsm = _serviceProvider.GetService<MockHardwareSecurityModule>();
            mockHsm?.Reset();

            // Reset chaos policy
            var chaosPolicy = _serviceProvider.GetService<ConfigurableChaosPolicy>();
            chaosPolicy?.Reset();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Test host environment
    /// </summary>
    internal class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "McpTestServer";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>
    /// Extension methods for LINQ operations
    /// </summary>
    internal static class EnumerableExtensions
    {
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source)
        {
            return source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
