using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ModelContextProtocol.Client.Tests
{
    public class McpClientTests : IDisposable
    {
        private readonly Mock<ILogger<McpClient>> _loggerMock;
        private readonly McpClient _mcpClient;

        public McpClientTests()
        {
            _loggerMock = new Mock<ILogger<McpClient>>();
            var options = new McpClientOptions
            {
                Host = "localhost",
                Port = 8080,
                Timeout = TimeSpan.FromSeconds(30)
            };
            _mcpClient = new McpClient(options, _loggerMock.Object);
        }

        [Fact]
        public async Task CallMethodAsync_ValidMethod_ReturnsExpectedResult()
        {
            // Arrange
            var expectedResult = new { Success = true };
            var methodName = "system.getCapabilities";
            var parameters = new { };

            // Act
            var result = await _mcpClient.CallMethodAsync<object>(methodName, parameters);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Success, result.Success);
        }

        [Fact]
        public async Task GetCapabilitiesAsync_ReturnsCapabilities()
        {
            // Act
            var capabilities = await _mcpClient.GetCapabilitiesAsync();

            // Assert
            Assert.NotNull(capabilities);
            Assert.IsType<McpCapabilities>(capabilities);
        }

        [Fact]
        public async Task GetResourceAsync_ValidResourceId_ReturnsResource()
        {
            // Arrange
            var resourceId = "resource1";

            // Act
            var resource = await _mcpClient.GetResourceAsync<object>(resourceId);

            // Assert
            Assert.NotNull(resource);
        }

        [Fact]
        public async Task ExecuteToolAsync_ValidToolId_ReturnsToolResult()
        {
            // Arrange
            var toolId = "tool1";
            var input = new { Data = "test" };

            // Act
            var result = await _mcpClient.ExecuteToolAsync<object>(toolId, input);

            // Assert
            Assert.NotNull(result);
        }

        public void Dispose()
        {
            _mcpClient?.Dispose();
        }
    }
}