using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Extensions.Performance;
using ModelContextProtocol.Extensions.Resilience;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Testing;
using ModelContextProtocol.Extensions.WebSocket;
using ModelContextProtocol.Extensions.Documentation;
using ModelContextProtocol.Extensions.Lifecycle;
using System;
using System.Collections.Generic;
using ModelContextProtocol.Core.Performance;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Comprehensive service registration for all enhanced MCP components
    /// </summary>
    public static class McpEnhancedServiceRegistration
    {
        /// <summary>
        /// Adds all enhanced MCP services with comprehensive configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddEnhancedMcp(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Core MCP services
            services.AddMcpServer(configuration);

            // Enhanced security services
            services.AddEnhancedSecurity(configuration);

            // Performance optimizations
            services.AddPerformanceEnhancements(configuration);

            // Observability improvements
            services.AddObservabilityEnhancements(configuration);

            // Resilience enhancements
            services.AddResilienceEnhancements(configuration);

            // WebSocket enhancements
            services.AddWebSocketEnhancements(configuration);

            // Configuration management
            services.AddConfigurationManagement(configuration);

            // Testing support (conditional based on environment)
            services.AddTestingSupport(configuration);

            // Documentation generation
            services.AddDocumentationGeneration(configuration);

            return services;
        }

        /// <summary>
        /// Adds enhanced security services including CT validation and OCSP
        /// </summary>
        private static IServiceCollection AddEnhancedSecurity(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Certificate Transparency validation
            services.AddSingleton<CertificateTransparencyValidator>();

            // Enhanced OCSP validation
            services.AddSingleton<EnhancedOcspValidator>();

            // Configure HTTP client for CT log validation
            services.AddHttpClient("CtLogClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "MCP-CertificateTransparency/1.0");
            });

            // Configure HTTP client for OCSP validation
            services.AddHttpClient("OcspClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("User-Agent", "MCP-OCSP/1.0");
            });

            return services;
        }

        /// <summary>
        /// Adds performance enhancement services
        /// </summary>
        private static IServiceCollection AddPerformanceEnhancements(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Compression middleware
            services.AddSingleton<McpCompressionMiddleware>();

            // Configure connection pool options
            services.Configure<ConnectionPoolOptions>(options =>
            {
                var section = configuration.GetSection("ConnectionPool");
                options.MaxConnectionsPerServer = section.GetValue<int>("MaxConnectionsPerServer", 50);
                options.ConnectionLifetimeSeconds = section.GetValue<int>("ConnectionLifetimeSeconds", 600);
                options.ConnectionTimeoutSeconds = section.GetValue<int>("ConnectionTimeoutSeconds", 30);
                options.PoolCleanupIntervalSeconds = section.GetValue<int>("PoolCleanupIntervalSeconds", 300);
                options.MaxIdleConnections = section.GetValue<int>("MaxIdleConnections", 20);
                options.EnableHttp2 = section.GetValue<bool>("EnableHttp2", true);
                options.EnableMultiplexing = section.GetValue<bool>("EnableMultiplexing", true);
            });

            // Enhanced connection pool (replace existing if present)
            services.AddSingleton<McpConnectionPool>();

            // Configure compression options
            services.Configure<CompressionOptions>(options =>
            {
                var section = configuration.GetSection("Compression");
                options.EnableBrotli = section.GetValue<bool>("EnableBrotli", true);
                options.EnableGzip = section.GetValue<bool>("EnableGzip", true);
                options.MinimumCompressionSize = section.GetValue<int>("MinimumCompressionSize", 1024);
                options.CompressionLevel = section.GetValue<int>("CompressionLevel", 6);
            });

            return services;
        }

        /// <summary>
        /// Adds observability enhancement services
        /// </summary>
        private static IServiceCollection AddObservabilityEnhancements(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Distributed tracing
            services.AddSingleton<McpDistributedTracing>();

            // Configure tracing options
            services.Configure<TracingOptions>(options =>
            {
                var section = configuration.GetSection("Tracing");
                options.ServiceName = section.GetValue<string>("ServiceName", "MCP-Server");
                options.ServiceVersion = section.GetValue<string>("ServiceVersion", "1.0.0");
                options.EnableMcpTracing = section.GetValue<bool>("EnableMcpTracing", true);
                options.EnableDatabaseTracing = section.GetValue<bool>("EnableDatabaseTracing", true);
                options.EnableHttpTracing = section.GetValue<bool>("EnableHttpTracing", true);
                options.SamplingRatio = section.GetValue<double>("SamplingRatio", 1.0);
            });

            return services;
        }

        /// <summary>
        /// Adds resilience enhancement services
        /// </summary>
        private static IServiceCollection AddResilienceEnhancements(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Key Vault circuit breaker
            services.AddSingleton<KeyVaultCircuitBreaker>();

            // Adaptive rate limiter
            services.AddSingleton<AdaptiveRateLimiter>();

            // Request throttling middleware
            services.AddSingleton<RequestThrottlingMiddleware>();

            // Configure circuit breaker options
            services.Configure<CircuitBreakerOptions>(options =>
            {
                var section = configuration.GetSection("CircuitBreaker:KeyVault");
                options.FailureThreshold = section.GetValue<int>("FailureThreshold", 5);
                options.RecoveryTimeout = section.GetValue<TimeSpan>("RecoveryTimeout", TimeSpan.FromMinutes(1));
                options.HalfOpenRetryTimeout = section.GetValue<TimeSpan>("HalfOpenRetryTimeout", TimeSpan.FromSeconds(30));
                options.MaxConcurrentCalls = section.GetValue<int>("MaxConcurrentCalls", 10);
            });

            // Configure adaptive rate limiting options
            services.Configure<AdaptiveRateLimitOptions>(options =>
            {
                var section = configuration.GetSection("AdaptiveRateLimit");
                options.InitialLimit = section.GetValue<int>("InitialLimit", 100);
                options.MinLimit = section.GetValue<int>("MinLimit", 10);
                options.MaxLimit = section.GetValue<int>("MaxLimit", 1000);
                options.TimeWindow = section.GetValue<TimeSpan>("TimeWindow", TimeSpan.FromMinutes(1));
                options.AdjustmentInterval = section.GetValue<TimeSpan>("AdjustmentInterval", TimeSpan.FromSeconds(30));
                options.ErrorRateThreshold = section.GetValue<double>("ErrorRateThreshold", 0.05);
                options.ResponseTimeThresholdMs = section.GetValue<double>("ResponseTimeThresholdMs", 1000);
                options.IncreaseFactor = section.GetValue<double>("IncreaseFactor", 1.1);
                options.MaxDecreaseFactor = section.GetValue<double>("MaxDecreaseFactor", 2.0);
            });

            // Configure request throttling options
            services.Configure<RequestThrottlingOptions>(options =>
            {
                var section = configuration.GetSection("RequestThrottling");
                options.MaxConsecutiveRejections = section.GetValue<int>("MaxConsecutiveRejections", 5);
                options.BlockDuration = section.GetValue<TimeSpan>("BlockDuration", TimeSpan.FromMinutes(15));
                options.MinRequestInterval = section.GetValue<TimeSpan>("MinRequestInterval", TimeSpan.FromMilliseconds(100));
                options.MaxBurstRequests = section.GetValue<int>("MaxBurstRequests", 10);
                options.BurstBlockDuration = section.GetValue<TimeSpan>("BurstBlockDuration", TimeSpan.FromMinutes(5));
                options.BurstResetInterval = section.GetValue<TimeSpan>("BurstResetInterval", TimeSpan.FromSeconds(10));
                options.MaxRequestSize = section.GetValue<int>("MaxRequestSize", 1024 * 1024);
                options.MaxEndpointRequestsPerWindow = section.GetValue<int>("MaxEndpointRequestsPerWindow", 1000);
                options.ClientExpirationTime = section.GetValue<TimeSpan>("ClientExpirationTime", TimeSpan.FromHours(24));
                options.CleanupInterval = section.GetValue<TimeSpan>("CleanupInterval", TimeSpan.FromMinutes(30));
            });

            return services;
        }

        /// <summary>
        /// Adds WebSocket enhancement services
        /// </summary>
        private static IServiceCollection AddWebSocketEnhancements(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // WebSocket connection manager
            services.AddSingleton<WebSocketConnectionManager>();

            // Configure WebSocket options
            services.Configure<WebSocketOptions>(options =>
            {
                var section = configuration.GetSection("WebSocket");
                options.HeartbeatInterval = section.GetValue<TimeSpan>("HeartbeatInterval", TimeSpan.FromSeconds(30));
                options.ConnectionTimeout = section.GetValue<TimeSpan>("ConnectionTimeout", TimeSpan.FromMinutes(5));
                options.MaxConnections = section.GetValue<int>("MaxConnections", 1000);
                options.EnableHeartbeat = section.GetValue<bool>("EnableHeartbeat", true);
                options.EnableCompression = section.GetValue<bool>("EnableCompression", true);
            });

            return services;
        }

        /// <summary>
        /// Adds configuration management services
        /// </summary>
        private static IServiceCollection AddConfigurationManagement(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configuration hot-reload service
            services.AddSingleton<ConfigurationHotReloadService>();

            // Register as hosted service for lifecycle management
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ConfigurationHotReloadService>());

            // Configure hot-reload options
            services.Configure<HotReloadOptions>(options =>
            {
                var section = configuration.GetSection("HotReload");
                options.EnableHotReload = section.GetValue<bool>("EnableHotReload", true);
                options.WatchPaths = section.GetSection("WatchPaths").Get<List<string>>() ??
                    new List<string> { "appsettings.json", "appsettings.*.json" };
                options.DebounceInterval = section.GetValue<TimeSpan>("DebounceInterval", TimeSpan.FromSeconds(2));
                options.MaxRetryAttempts = section.GetValue<int>("MaxRetryAttempts", 3);
                options.RetryDelay = section.GetValue<TimeSpan>("RetryDelay", TimeSpan.FromSeconds(1));
            });

            return services;
        }

        /// <summary>
        /// Adds testing support services (conditional based on environment)
        /// </summary>
        private static IServiceCollection AddTestingSupport(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var environment = configuration.GetValue<string>("Environment", "Production");

            // Only add testing services in non-production environments
            if (environment != "Production")
            {
                services.AddSingleton<McpTestFixture>();
                services.AddSingleton<MockMcpClient>();
                services.AddSingleton<MockMcpServer>();
                services.AddSingleton<CertificateGenerator>();

                // Configure test options
                services.Configure<TestOptions>(options =>
                {
                    var section = configuration.GetSection("Testing");
                    options.EnableMockServices = section.GetValue<bool>("EnableMockServices", true);
                    options.MockResponseDelay = section.GetValue<TimeSpan>("MockResponseDelay", TimeSpan.FromMilliseconds(100));
                    options.SimulateFailures = section.GetValue<bool>("SimulateFailures", false);
                    options.FailureRate = section.GetValue<double>("FailureRate", 0.1);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds documentation generation services
        /// </summary>
        private static IServiceCollection AddDocumentationGeneration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // OpenAPI generator
            services.AddSingleton<McpOpenApiGenerator>();

            // Configure documentation options
            services.Configure<DocumentationOptions>(options =>
            {
                var section = configuration.GetSection("Documentation");
                options.EnableDocumentationGeneration = section.GetValue<bool>("EnableDocumentationGeneration", true);
                options.OutputDirectory = section.GetValue<string>("OutputDirectory", "docs");
                options.GenerateMarkdown = section.GetValue<bool>("GenerateMarkdown", true);
                options.GenerateOpenApi = section.GetValue<bool>("GenerateOpenApi", true);
                options.IncludeExamples = section.GetValue<bool>("IncludeExamples", true);
            });

            return services;
        }

        /// <summary>
        /// Adds enhanced connection string resolution with retry and validation
        /// </summary>
        public static IServiceCollection AddEnhancedConnectionStringResolution(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure enhanced resolver options
            services.Configure<ConnectionStringResolverOptions>(configuration.GetSection("ConnectionStringResolver"));

            // Register Azure Key Vault service
            services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();

            // Register enhanced resolver
            services.AddSingleton<IAzureKeyVaultConnectionStringResolver, AzureKeyVaultConnectionStringResolver>();

            // Register connection string cache service
            services.AddSingleton<IConnectionStringCacheService, ConnectionStringCacheService>();

            // Register resolver service
            services.AddSingleton<IConnectionStringResolverService, ConnectionStringResolverService>();

            return services;
        }

        /// <summary>
        /// Configures graceful shutdown for all enhanced services
        /// </summary>
        public static IServiceCollection AddGracefulShutdown(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<GracefulShutdownService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<GracefulShutdownService>());

            // Configure shutdown options
            services.Configure<ShutdownOptions>(options =>
            {
                var section = configuration.GetSection("Shutdown");
                options.GracefulShutdownTimeout = section.GetValue<TimeSpan>("GracefulShutdownTimeout", TimeSpan.FromSeconds(30));
                options.ForceShutdownTimeout = section.GetValue<TimeSpan>("ForceShutdownTimeout", TimeSpan.FromSeconds(60));
                options.EnableGracefulShutdown = section.GetValue<bool>("EnableGracefulShutdown", true);
            });

            return services;
        }
    }

    /// <summary>
    /// Configuration options for compression
    /// </summary>
    public class CompressionOptions
    {
        public bool EnableBrotli { get; set; } = true;
        public bool EnableGzip { get; set; } = true;
        public int MinimumCompressionSize { get; set; } = 1024;
        public int CompressionLevel { get; set; } = 6;
    }

    /// <summary>
    /// Configuration options for tracing
    /// </summary>
    public class TracingOptions
    {
        public string ServiceName { get; set; } = "MCP-Server";
        public string ServiceVersion { get; set; } = "1.0.0";
        public bool EnableMcpTracing { get; set; } = true;
        public bool EnableDatabaseTracing { get; set; } = true;
        public bool EnableHttpTracing { get; set; } = true;
        public double SamplingRatio { get; set; } = 1.0;
    }

    /// <summary>
    /// Configuration options for circuit breaker
    /// </summary>
    public class CircuitBreakerOptions
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan HalfOpenRetryTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxConcurrentCalls { get; set; } = 10;
    }

    /// <summary>
    /// Configuration options for WebSocket
    /// </summary>
    public class WebSocketOptions
    {
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxConnections { get; set; } = 1000;
        public bool EnableHeartbeat { get; set; } = true;
        public bool EnableCompression { get; set; } = true;
    }

    /// <summary>
    /// Configuration options for hot-reload
    /// </summary>
    public class HotReloadOptions
    {
        public bool EnableHotReload { get; set; } = true;
        public List<string> WatchPaths { get; set; } = new();
        public TimeSpan DebounceInterval { get; set; } = TimeSpan.FromSeconds(2);
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Configuration options for testing
    /// </summary>
    public class TestOptions
    {
        public bool EnableMockServices { get; set; } = true;
        public TimeSpan MockResponseDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public bool SimulateFailures { get; set; } = false;
        public double FailureRate { get; set; } = 0.1;
    }

    /// <summary>
    /// Configuration options for documentation
    /// </summary>
    public class DocumentationOptions
    {
        public bool EnableDocumentationGeneration { get; set; } = true;
        public string OutputDirectory { get; set; } = "docs";
        public bool GenerateMarkdown { get; set; } = true;
        public bool GenerateOpenApi { get; set; } = true;
        public bool IncludeExamples { get; set; } = true;
    }

    /// <summary>
    /// Configuration options for graceful shutdown
    /// </summary>
    public class ShutdownOptions
    {
        public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ForceShutdownTimeout { get; set; } = TimeSpan.FromSeconds(60);
        public bool EnableGracefulShutdown { get; set; } = true;
    }
}
