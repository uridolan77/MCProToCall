using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests
{
    public class McpServerTests : IDisposable
    {
        private readonly Mock<ILogger<McpServer>> _loggerMock;
        private readonly McpServerOptions _options;
        private readonly McpServer _mcpServer;

        public McpServerTests()
        {
            _loggerMock = new Mock<ILogger<McpServer>>();
            _options = new McpServerOptions
            {
                Host = "127.0.0.1",
                Port = 8080
            };
            _mcpServer = new McpServer(_options, _loggerMock.Object);
        }

        [Fact]
        public async Task StartAsync_ShouldStartServer()
        {
            await _mcpServer.StartAsync();

            // Assert that the server is listening
            Assert.True(_mcpServer.IsListening);
        }

        [Fact]
        public void Stop_ShouldStopServer()
        {
            _mcpServer.Stop();

            // Assert that the server is not listening
            Assert.False(_mcpServer.IsListening);
        }

        [Fact]
        public void RegisterMethod_ShouldAddMethodToServer()
        {
            _mcpServer.RegisterMethod("test.method", async (JsonElement parameters) => await Task.FromResult("success"));

            // Assert that the method is registered
            Assert.True(_mcpServer.Methods.ContainsKey("test.method"));
        }

        public void Dispose()
        {
            _mcpServer.Dispose();
        }
    }
}