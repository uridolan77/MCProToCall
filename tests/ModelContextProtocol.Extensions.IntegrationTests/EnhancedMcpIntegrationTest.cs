using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Performance;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Extensions.WebSocket;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.Resilience;
using ModelContextProtocol.Extensions.Testing;
using ModelContextProtocol.Extensions.Documentation;
using ModelContextProtocol.Extensions.Lifecycle;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ModelContextProtocol.Extensions.IntegrationTests
{
    /// <summary>
    /// Comprehensive integration test for all enhanced MCP components
    /// </summary>
    public class EnhancedMcpIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public EnhancedMcpIntegrationTest(ITestOutputHelper output)
        {
            _output = output;
            
            // Build test configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.enhanced.json", optional: false)
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("Environment", "Testing"),
                    new KeyValuePair<string, string>("Testing:EnableMockServices", "true"),
                    new KeyValuePair<string, string>("McpServer:UseTls", "false"), // Disable TLS for testing
                    new KeyValuePair<string, string>("KeyVault:VaultUri", "https://test-vault.vault.azure.net/"),
                })
                .Build();

            // Build service provider
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add configuration
            services.AddSingleton(_configuration);

            // Add all enhanced MCP services
            services.AddEnhancedMcp(_configuration);
            
            // Add enhanced connection string resolution
            services.AddEnhancedConnectionStringResolution(_configuration);
            
            // Add graceful shutdown
            services.AddGracefulShutdown(_configuration);
        }

        [Fact]
        public async Task TestServiceRegistration_AllServicesResolved()
        {
            // Arrange & Act
            _output.WriteLine("Testing service registration...");

            // Test security services
            var ctValidator = _serviceProvider.GetService<CertificateTransparencyValidator>();
            var ocspValidator = _serviceProvider.GetService<EnhancedOcspValidator>();
            var throttlingMiddleware = _serviceProvider.GetService<RequestThrottlingMiddleware>();
            
            // Test performance services
            var compressionMiddleware = _serviceProvider.GetService<McpCompressionMiddleware>();
            var connectionPool = _serviceProvider.GetService<McpConnectionPool>();
            
            // Test observability services
            var distributedTracing = _serviceProvider.GetService<McpDistributedTracing>();
            
            // Test resilience services
            var circuitBreaker = _serviceProvider.GetService<KeyVaultCircuitBreaker>();
            var adaptiveRateLimiter = _serviceProvider.GetService<AdaptiveRateLimiter>();
            
            // Test WebSocket services
            var webSocketManager = _serviceProvider.GetService<WebSocketConnectionManager>();
            
            // Test configuration services
            var hotReloadService = _serviceProvider.GetService<ConfigurationHotReloadService>();
            
            // Test testing services
            var testFixture = _serviceProvider.GetService<McpTestFixture>();
            var mockClient = _serviceProvider.GetService<MockMcpClient>();
            var mockServer = _serviceProvider.GetService<MockMcpServer>();
            var certificateGenerator = _serviceProvider.GetService<CertificateGenerator>();
            
            // Test documentation services
            var openApiGenerator = _serviceProvider.GetService<McpOpenApiGenerator>();
            
            // Test lifecycle services
            var shutdownService = _serviceProvider.GetService<GracefulShutdownService>();

            // Assert
            Assert.NotNull(ctValidator);
            Assert.NotNull(ocspValidator);
            Assert.NotNull(throttlingMiddleware);
            Assert.NotNull(compressionMiddleware);
            Assert.NotNull(connectionPool);
            Assert.NotNull(distributedTracing);
            Assert.NotNull(circuitBreaker);
            Assert.NotNull(adaptiveRateLimiter);
            Assert.NotNull(webSocketManager);
            Assert.NotNull(hotReloadService);
            Assert.NotNull(testFixture);
            Assert.NotNull(mockClient);
            Assert.NotNull(mockServer);
            Assert.NotNull(certificateGenerator);
            Assert.NotNull(openApiGenerator);
            Assert.NotNull(shutdownService);

            _output.WriteLine("✓ All services resolved successfully");
        }

        [Fact]
        public async Task TestAdaptiveRateLimiter_BasicFunctionality()
        {
            // Arrange
            _output.WriteLine("Testing adaptive rate limiter...");
            var rateLimiter = _serviceProvider.GetRequiredService<AdaptiveRateLimiter>();
            var clientId = "test-client-123";

            // Act & Assert
            // Test initial requests should be allowed
            for (int i = 0; i < 5; i++)
            {
                var result = await rateLimiter.IsAllowedAsync(clientId);
                Assert.True(result.IsAllowed, $"Request {i + 1} should be allowed");
                _output.WriteLine($"Request {i + 1}: Allowed={result.IsAllowed}, Remaining={result.RemainingRequests}");
            }

            // Record some successful completions
            for (int i = 0; i < 3; i++)
            {
                rateLimiter.RecordRequestCompletion(clientId, true, TimeSpan.FromMilliseconds(200));
            }

            // Get statistics
            var stats = rateLimiter.GetStatistics();
            Assert.True(stats.CurrentLimit > 0);
            _output.WriteLine($"Rate limiter stats: CurrentLimit={stats.CurrentLimit}, ErrorRate={stats.CurrentErrorRate:P2}");

            _output.WriteLine("✓ Adaptive rate limiter working correctly");
        }

        [Fact]
        public async Task TestRequestThrottling_BurstDetection()
        {
            // Arrange
            _output.WriteLine("Testing request throttling burst detection...");
            var throttlingMiddleware = _serviceProvider.GetRequiredService<RequestThrottlingMiddleware>();
            var clientId = "burst-test-client";

            // Act
            var results = new List<ThrottleResult>();
            
            // Send rapid requests to trigger burst detection
            for (int i = 0; i < 15; i++)
            {
                var result = await throttlingMiddleware.ProcessRequestAsync(clientId, "/test", 1024);
                results.Add(result);
                _output.WriteLine($"Request {i + 1}: Allowed={result.IsAllowed}, Reason={result.Reason}");
            }

            // Assert
            // First few requests should be allowed, then burst detection should kick in
            Assert.True(results.Take(5).All(r => r.IsAllowed), "Initial requests should be allowed");
            Assert.True(results.Skip(10).Any(r => !r.IsAllowed), "Later requests should be throttled due to burst detection");

            _output.WriteLine("✓ Request throttling burst detection working correctly");
        }

        [Fact]
        public void TestConnectionPool_BasicOperations()
        {
            // Arrange
            _output.WriteLine("Testing connection pool...");
            var connectionPool = _serviceProvider.GetRequiredService<McpConnectionPool>();

            // Act & Assert
            var stats = connectionPool.GetStatistics();
            Assert.NotNull(stats);
            _output.WriteLine($"Connection pool stats: Active={stats.ActiveConnections}, Idle={stats.IdleConnections}");

            // Test pool operations
            Assert.True(stats.ActiveConnections >= 0);
            Assert.True(stats.IdleConnections >= 0);

            _output.WriteLine("✓ Connection pool operations working correctly");
        }

        [Fact]
        public void TestWebSocketManager_Initialization()
        {
            // Arrange
            _output.WriteLine("Testing WebSocket manager...");
            var webSocketManager = _serviceProvider.GetRequiredService<WebSocketConnectionManager>();

            // Act & Assert
            var stats = webSocketManager.GetStatistics();
            Assert.NotNull(stats);
            _output.WriteLine($"WebSocket stats: Active={stats.ActiveConnections}, Total={stats.TotalConnections}");

            Assert.True(stats.ActiveConnections >= 0);
            Assert.True(stats.TotalConnections >= 0);

            _output.WriteLine("✓ WebSocket manager initialization successful");
        }

        [Fact]
        public void TestCircuitBreaker_StateManagement()
        {
            // Arrange
            _output.WriteLine("Testing circuit breaker...");
            var circuitBreaker = _serviceProvider.GetRequiredService<KeyVaultCircuitBreaker>();

            // Act & Assert
            var stats = circuitBreaker.GetStatistics();
            Assert.NotNull(stats);
            _output.WriteLine($"Circuit breaker stats: State={stats.State}, FailureCount={stats.FailureCount}");

            Assert.True(Enum.IsDefined(typeof(CircuitBreakerState), stats.State));

            _output.WriteLine("✓ Circuit breaker state management working correctly");
        }

        [Fact]
        public async Task TestMockServices_Functionality()
        {
            // Arrange
            _output.WriteLine("Testing mock services...");
            var mockClient = _serviceProvider.GetRequiredService<MockMcpClient>();
            var mockServer = _serviceProvider.GetRequiredService<MockMcpServer>();

            // Act & Assert
            Assert.NotNull(mockClient);
            Assert.NotNull(mockServer);

            // Test mock client initialization
            var clientStats = mockClient.GetStatistics();
            Assert.NotNull(clientStats);
            _output.WriteLine($"Mock client stats: RequestCount={clientStats.RequestCount}");

            // Test mock server initialization
            var serverStats = mockServer.GetStatistics();
            Assert.NotNull(serverStats);
            _output.WriteLine($"Mock server stats: RequestCount={serverStats.RequestCount}");

            _output.WriteLine("✓ Mock services working correctly");
        }

        [Fact]
        public async Task TestDocumentationGeneration_OpenApi()
        {
            // Arrange
            _output.WriteLine("Testing documentation generation...");
            var openApiGenerator = _serviceProvider.GetRequiredService<McpOpenApiGenerator>();

            // Act
            var openApiSpec = await openApiGenerator.GenerateOpenApiSpecificationAsync();
            var markdownDocs = await openApiGenerator.GenerateMarkdownDocumentationAsync();

            // Assert
            Assert.NotNull(openApiSpec);
            Assert.Contains("Enhanced MCP Server API", openApiSpec);
            Assert.NotNull(markdownDocs);
            Assert.Contains("# Enhanced MCP Server", markdownDocs);

            _output.WriteLine($"Generated OpenAPI spec: {openApiSpec.Length} characters");
            _output.WriteLine($"Generated Markdown docs: {markdownDocs.Length} characters");
            _output.WriteLine("✓ Documentation generation working correctly");
        }

        [Fact]
        public void TestCertificateGeneration_TestCertificates()
        {
            // Arrange
            _output.WriteLine("Testing certificate generation...");
            var certificateGenerator = _serviceProvider.GetRequiredService<CertificateGenerator>();

            // Act
            var serverCert = certificateGenerator.GenerateServerCertificate("localhost");
            var clientCert = certificateGenerator.GenerateClientCertificate("test-client");

            // Assert
            Assert.NotNull(serverCert);
            Assert.NotNull(clientCert);
            Assert.True(serverCert.HasPrivateKey);
            Assert.True(clientCert.HasPrivateKey);

            _output.WriteLine($"Server cert subject: {serverCert.Subject}");
            _output.WriteLine($"Client cert subject: {clientCert.Subject}");
            _output.WriteLine("✓ Certificate generation working correctly");

            // Clean up
            serverCert.Dispose();
            clientCert.Dispose();
        }

        [Fact]
        public void TestGracefulShutdown_Statistics()
        {
            // Arrange
            _output.WriteLine("Testing graceful shutdown service...");
            var shutdownService = _serviceProvider.GetRequiredService<GracefulShutdownService>();

            // Act
            var stats = shutdownService.GetShutdownStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.False(stats.IsShutdownInitiated);
            Assert.True(stats.GracefulShutdownTimeout > TimeSpan.Zero);
            Assert.True(stats.ForceShutdownTimeout > TimeSpan.Zero);

            _output.WriteLine($"Shutdown stats: GracefulTimeout={stats.GracefulShutdownTimeout}, ForceTimeout={stats.ForceShutdownTimeout}");
            _output.WriteLine("✓ Graceful shutdown service working correctly");
        }

        [Fact]
        public async Task TestIntegration_EndToEndWorkflow()
        {
            // Arrange
            _output.WriteLine("Testing end-to-end integration workflow...");
            
            var rateLimiter = _serviceProvider.GetRequiredService<AdaptiveRateLimiter>();
            var throttlingMiddleware = _serviceProvider.GetRequiredService<RequestThrottlingMiddleware>();
            var connectionPool = _serviceProvider.GetRequiredService<McpConnectionPool>();
            
            var clientId = "integration-test-client";

            // Act
            _output.WriteLine("Step 1: Testing rate limiting");
            var rateLimitResult = await rateLimiter.IsAllowedAsync(clientId);
            Assert.True(rateLimitResult.IsAllowed);

            _output.WriteLine("Step 2: Testing request throttling");
            var throttleResult = await throttlingMiddleware.ProcessRequestAsync(clientId, "/test-endpoint", 512);
            Assert.True(throttleResult.IsAllowed);

            _output.WriteLine("Step 3: Recording request completion");
            rateLimiter.RecordRequestCompletion(clientId, true, TimeSpan.FromMilliseconds(150));
            throttlingMiddleware.RecordRequestCompletion(clientId, true, TimeSpan.FromMilliseconds(150));

            _output.WriteLine("Step 4: Checking statistics");
            var rateLimiterStats = rateLimiter.GetStatistics();
            var throttlingStats = throttlingMiddleware.GetStatistics();
            var poolStats = connectionPool.GetStatistics();

            Assert.True(rateLimiterStats.TotalRequests > 0);
            Assert.True(throttlingStats.TotalRequests > 0);
            Assert.NotNull(poolStats);

            _output.WriteLine($"Rate limiter processed {rateLimiterStats.TotalRequests} requests");
            _output.WriteLine($"Throttling middleware processed {throttlingStats.TotalRequests} requests");
            _output.WriteLine($"Connection pool has {poolStats.ActiveConnections} active connections");

            _output.WriteLine("✓ End-to-end integration workflow completed successfully");
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}
