using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class TlsConnectionManagerTests
    {
        private readonly Mock<ILogger<TlsConnectionManager>> _loggerMock;
        private readonly Mock<IOptions<TlsOptions>> _optionsMock;
        private readonly TlsOptions _tlsOptions;
        
        public TlsConnectionManagerTests()
        {
            _loggerMock = new Mock<ILogger<TlsConnectionManager>>();
            _tlsOptions = new TlsOptions
            {
                MaxConnectionsPerIpAddress = 3,
                ConnectionRateLimitingWindowSeconds = 1
            };
            _optionsMock = new Mock<IOptions<TlsOptions>>();
            _optionsMock.Setup(o => o.Value).Returns(_tlsOptions);
        }
        
        [Fact]
        public void TryAddConnection_ReturnsTrue_ForFirstConnection()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.1";
            
            // Act
            var result = manager.TryAddConnection(ipAddress);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void TryAddConnection_ReturnsTrue_UnderMaxLimit()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.2";
            
            // Act & Assert
            // Add up to the limit
            for (int i = 0; i < _tlsOptions.MaxConnectionsPerIpAddress; i++)
            {
                Assert.True(manager.TryAddConnection(ipAddress));
            }
        }
        
        [Fact]
        public void TryAddConnection_ReturnsFalse_OverMaxLimit()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.3";
            
            // Act
            // Add up to the limit
            for (int i = 0; i < _tlsOptions.MaxConnectionsPerIpAddress; i++)
            {
                manager.TryAddConnection(ipAddress);
            }
            
            // Try to add one more
            var result = manager.TryAddConnection(ipAddress);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void RemoveConnection_DecreasesCounter()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.4";
            
            // Add up to the limit
            for (int i = 0; i < _tlsOptions.MaxConnectionsPerIpAddress; i++)
            {
                manager.TryAddConnection(ipAddress);
            }
            
            // Act
            // Remove one connection
            manager.RemoveConnection(ipAddress);
            
            // Try to add one more - should now succeed
            var result = manager.TryAddConnection(ipAddress);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void TryAddConnection_AllowsNewConnectionsAfterTimeWindow()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.5";
            
            // Add up to the limit
            for (int i = 0; i < _tlsOptions.MaxConnectionsPerIpAddress; i++)
            {
                manager.TryAddConnection(ipAddress);
            }
            
            // Try to add one more - should fail
            Assert.False(manager.TryAddConnection(ipAddress));
            
            // Act
            // Wait for the rate limiting window to expire
            Thread.Sleep(TimeSpan.FromSeconds(_tlsOptions.ConnectionRateLimitingWindowSeconds + 1));
            
            // Now should be able to add a new connection
            var result = manager.TryAddConnection(ipAddress);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void GetActiveConnectionCount_ReturnsCorrectCount()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.6";
            
            // Add some connections
            int numConnections = 2;
            for (int i = 0; i < numConnections; i++)
            {
                manager.TryAddConnection(ipAddress);
            }
            
            // Act
            var count = manager.GetActiveConnectionCount(ipAddress);
            
            // Assert
            Assert.Equal(numConnections, count);
        }
        
        [Fact]
        public void GetActiveConnectionCount_ReturnsZero_ForUnknownIp()
        {
            // Arrange
            var manager = new TlsConnectionManager(
                _loggerMock.Object,
                _optionsMock.Object);
            
            string ipAddress = "192.168.1.7";
            
            // Act
            var count = manager.GetActiveConnectionCount(ipAddress);
            
            // Assert
            Assert.Equal(0, count);
        }
    }
}
