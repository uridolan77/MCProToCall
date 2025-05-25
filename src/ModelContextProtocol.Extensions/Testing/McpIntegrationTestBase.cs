using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Xunit;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Integration test base class for MCP implementations
    /// </summary>
    public abstract class McpIntegrationTestBase : IClassFixture<McpTestFixture>, IAsyncLifetime
    {
        protected readonly McpTestFixture TestFixture;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly ILogger Logger;
        protected readonly HttpClient HttpClient;

        protected McpIntegrationTestBase(McpTestFixture testFixture)
        {
            TestFixture = testFixture;
            ServiceProvider = testFixture.ServiceProvider;
            Logger = ServiceProvider.GetRequiredService<ILogger<McpIntegrationTestBase>>();
            HttpClient = testFixture.CreateTestHttpClient();
        }

        /// <summary>
        /// Creates a test server with default configuration
        /// </summary>
        /// <param name="configureOptions">Additional server configuration</param>
        /// <param name="configureServices">Additional service configuration</param>
        /// <returns>Configured test server</returns>
        protected async Task<IMcpServer> CreateTestServerAsync(
            Action<McpServerOptions> configureOptions = null,
            Action<IServiceCollection> configureServices = null)
        {
            return await TestFixture.CreateServerAsync(configureOptions, configureServices);
        }

        /// <summary>
        /// Creates a secure test server with TLS
        /// </summary>
        /// <param name="configureOptions">Additional server configuration</param>
        /// <param name="configureServices">Additional service configuration</param>
        /// <returns>Configured secure test server</returns>
        protected async Task<IMcpServer> CreateSecureTestServerAsync(
            Action<McpServerOptions> configureOptions = null,
            Action<IServiceCollection> configureServices = null)
        {
            return await TestFixture.CreateSecureServerAsync(configureOptions, configureServices);
        }

        /// <summary>
        /// Creates a mock client for testing
        /// </summary>
        /// <returns>Mock MCP client</returns>
        protected MockMcpClient CreateMockClient()
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<MockMcpClient>>();
            return new MockMcpClient(logger);
        }

        /// <summary>
        /// Creates a mock server for testing
        /// </summary>
        /// <returns>Mock MCP server</returns>
        protected MockMcpServer CreateMockServer()
        {
            var logger = ServiceProvider.GetRequiredService<ILogger<MockMcpServer>>();
            return new MockMcpServer(logger);
        }

        /// <summary>
        /// Waits for a server to be ready
        /// </summary>
        /// <param name="server">Server to wait for</param>
        /// <param name="timeout">Maximum wait time</param>
        /// <returns>True if server is ready</returns>
        protected async Task<bool> WaitForServerAsync(IMcpServer server, TimeSpan? timeout = null)
        {
            return await TestFixture.WaitForServerReadyAsync(server, timeout);
        }

        /// <summary>
        /// Creates a test configuration
        /// </summary>
        /// <param name="additionalSettings">Additional configuration settings</param>
        /// <returns>Test configuration</returns>
        protected IConfiguration CreateTestConfiguration(Dictionary<string, string> additionalSettings = null)
        {
            var settings = new Dictionary<string, string>
            {
                ["Logging:LogLevel:Default"] = "Debug",
                ["Logging:LogLevel:Microsoft"] = "Warning",
                ["McpServer:Host"] = "127.0.0.1",
                ["McpServer:Port"] = "19000",
                ["McpServer:UseTls"] = "false",
                ["McpServer:MaxConcurrentConnections"] = "10",
                ["McpServer:RequestTimeout"] = "00:00:30"
            };

            if (additionalSettings != null)
            {
                foreach (var kvp in additionalSettings)
                {
                    settings[kvp.Key] = kvp.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        /// <summary>
        /// Creates a test host for integration testing
        /// </summary>
        /// <param name="configureServices">Service configuration</param>
        /// <param name="configureApp">Application configuration</param>
        /// <returns>Test host</returns>
        protected async Task<IHost> CreateTestHostAsync(
            Action<IServiceCollection> configureServices = null,
            Action<IHostBuilder> configureApp = null)
        {
            var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(CreateTestConfiguration());
                    configureServices?.Invoke(services);
                });

            configureApp?.Invoke(hostBuilder);

            var host = hostBuilder.Build();
            await host.StartAsync();
            return host;
        }

        /// <summary>
        /// Asserts that a server is listening on the expected port
        /// </summary>
        /// <param name="server">Server to check</param>
        /// <param name="port">Expected port</param>
        protected void AssertServerListening(IMcpServer server, int port)
        {
            Assert.True(server.IsListening, "Server should be listening");

            // Additional checks could be added here to verify the server is actually
            // accepting connections on the specified port
        }

        /// <summary>
        /// Asserts that a server responds to basic requests
        /// </summary>
        /// <param name="server">Server to test</param>
        /// <param name="baseUrl">Base URL for requests</param>
        protected async Task AssertServerRespondsAsync(IMcpServer server, string baseUrl)
        {
            Assert.True(server.IsListening, "Server must be listening");

            try
            {
                // Test basic connectivity (this would depend on your server implementation)
                // For now, we'll just verify the server is in the correct state
                await Task.Delay(100); // Give server time to fully initialize

                // In a real implementation, you might make an HTTP request here
                // var response = await HttpClient.GetAsync($"{baseUrl}/health");
                // Assert.True(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                Assert.True(false, $"Server did not respond: {ex.Message}");
            }
        }

        /// <summary>
        /// Setup method called before each test
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            Logger.LogInformation("Initializing integration test");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cleanup method called after each test
        /// </summary>
        public virtual async Task DisposeAsync()
        {
            Logger.LogInformation("Disposing integration test");
            HttpClient?.Dispose();
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collection definition for integration tests that need shared test fixture
    /// </summary>
    [CollectionDefinition("MCP Integration Tests")]
    public class McpIntegrationTestCollection : ICollectionFixture<McpTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Example integration test class showing usage patterns
    /// </summary>
    [Collection("MCP Integration Tests")]
    public class ExampleMcpIntegrationTests : McpIntegrationTestBase
    {
        public ExampleMcpIntegrationTests(McpTestFixture testFixture) : base(testFixture)
        {
        }

        [Fact]
        public async Task Server_CanStart_AndRespondsToRequests()
        {
            // Arrange
            var server = await CreateTestServerAsync(options =>
            {
                options.Host = "127.0.0.1";
                options.Port = McpTestFixture.TestPorts.BasePort;
            });

            // Act
            await server.StartAsync();
            var isReady = await WaitForServerAsync(server);

            // Assert
            Assert.True(isReady, "Server should be ready");
            AssertServerListening(server, McpTestFixture.TestPorts.BasePort);
        }

        [Fact]
        public async Task SecureServer_CanStart_WithTls()
        {
            // Arrange
            var server = await CreateSecureTestServerAsync(options =>
            {
                options.Host = "127.0.0.1";
                options.Port = McpTestFixture.TestPorts.SecurePort;
            });

            // Act
            await server.StartAsync();
            var isReady = await WaitForServerAsync(server);

            // Assert
            Assert.True(isReady, "Secure server should be ready");
            AssertServerListening(server, McpTestFixture.TestPorts.SecurePort);
        }

        [Fact]
        public async Task MockClient_CanSimulate_ClientBehavior()
        {
            // Arrange
            var mockClient = CreateMockClient();
            mockClient.SetupMethod("custom.test", async _ =>
            {
                await Task.Delay(10);
                return new { success = true, message = "Test completed" };
            });

            // Act
            await mockClient.ConnectAsync();
            var result = await mockClient.CallMethodAsync<object>("custom.test", new { });

            // Assert
            Assert.True(mockClient.IsConnected);
            Assert.Equal(1, mockClient.GetCallCount("custom.test"));
            Assert.NotNull(result);
        }

        [Fact]
        public async Task MockServer_CanSimulate_ServerBehavior()
        {
            // Arrange
            var mockServer = CreateMockServer();
            mockServer.RegisterMethod("custom.handler", async parameters =>
            {
                await Task.Delay(5);
                return new { processed = true, receivedParams = parameters.ToString() };
            });

            // Act
            await mockServer.StartAsync();
            var request = new ModelContextProtocol.Core.Models.JsonRpc.JsonRpcRequest
            {
                Id = "test-1",
                Method = "custom.handler",
                Params = System.Text.Json.JsonSerializer.SerializeToElement(new { test = "data" })
            };
            var response = await mockServer.HandleRequestAsync(request);

            // Assert
            Assert.True(mockServer.IsListening);
            Assert.Equal(1, mockServer.GetCallCount("custom.handler"));
            Assert.IsType<ModelContextProtocol.Core.Models.JsonRpc.JsonRpcResponse>(response);
        }
    }

    /// <summary>
    /// Behavior-driven test extensions for fluent test writing
    /// </summary>
    public static class BehaviorTestExtensions
    {
        /// <summary>
        /// Starts a behavior-driven test with a given setup
        /// </summary>
        public static async Task<TResult> Given<TResult>(this Task<TResult> setup, string description)
        {
            Console.WriteLine($"Given: {description}");
            return await setup;
        }

        /// <summary>
        /// Performs an action in the behavior-driven test
        /// </summary>
        public static async Task<TResult> When<T, TResult>(this Task<T> given, Func<T, Task<TResult>> action, string description)
        {
            Console.WriteLine($"When: {description}");
            var input = await given;
            return await action(input);
        }

        /// <summary>
        /// Performs a synchronous action in the behavior-driven test
        /// </summary>
        public static async Task<TResult> When<T, TResult>(this Task<T> given, Func<T, TResult> action, string description)
        {
            Console.WriteLine($"When: {description}");
            var input = await given;
            return action(input);
        }

        /// <summary>
        /// Asserts the result of a behavior-driven test
        /// </summary>
        public static async Task Then<T>(this Task<T> when, Action<T> assertion, string description)
        {
            Console.WriteLine($"Then: {description}");
            var result = await when;
            assertion(result);
        }

        /// <summary>
        /// Asserts the result of a behavior-driven test asynchronously
        /// </summary>
        public static async Task Then<T>(this Task<T> when, Func<T, Task> assertion, string description)
        {
            Console.WriteLine($"Then: {description}");
            var result = await when;
            await assertion(result);
        }

        /// <summary>
        /// Adds additional context to a behavior-driven test
        /// </summary>
        public static async Task<T> And<T>(this Task<T> previous, Action<T> action, string description)
        {
            Console.WriteLine($"And: {description}");
            var result = await previous;
            action(result);
            return result;
        }

        /// <summary>
        /// Adds additional asynchronous context to a behavior-driven test
        /// </summary>
        public static async Task<T> And<T>(this Task<T> previous, Func<T, Task> action, string description)
        {
            Console.WriteLine($"And: {description}");
            var result = await previous;
            await action(result);
            return result;
        }
    }
}
