using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Extensions.Security.Authentication;
using ModelContextProtocol.Extensions.Security.Authorization;
using Moq;
using Xunit;

namespace ModelContextProtocol.Server.Tests.Security
{
    public class AuthorizationMiddlewareTests
    {
        private readonly Mock<IJwtTokenProvider> _mockTokenProvider;
        private readonly Mock<ILogger<AuthorizationMiddleware>> _mockLogger;
        private readonly AuthorizationMiddleware _middleware;

        public AuthorizationMiddlewareTests()
        {
            _mockTokenProvider = new Mock<IJwtTokenProvider>();
            _mockLogger = new Mock<ILogger<AuthorizationMiddleware>>();
            _middleware = new AuthorizationMiddleware(_mockTokenProvider.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task PublicMethod_ShouldAllowAccess_WithoutToken()
        {
            // Arrange
            _middleware.RegisterPublicMethod("public.method");
            var request = new JsonRpcRequest
            {
                Method = "public.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, null);

            // Assert
            Assert.True(result.IsAuthorized);
        }

        [Fact]
        public async Task SecuredMethod_ShouldDenyAccess_WithoutToken()
        {
            // Arrange
            _middleware.RegisterMethodPermission("secured.method", new[] { "User" });
            var request = new JsonRpcRequest
            {
                Method = "secured.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, null);

            // Assert
            Assert.False(result.IsAuthorized);
            Assert.Equal(401, result.ErrorCode); // Unauthorized
        }

        [Fact]
        public async Task SecuredMethod_ShouldAllowAccess_WithValidToken()
        {
            // Arrange
            _middleware.RegisterMethodPermission("secured.method", new[] { "User" });
            var request = new JsonRpcRequest
            {
                Method = "secured.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            _mockTokenProvider
                .Setup(x => x.ValidateTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTokenProvider
                .Setup(x => x.GetClaimsFromTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, string> { { "role", "User" } });

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, "valid_token");

            // Assert
            Assert.True(result.IsAuthorized);
        }

        [Fact]
        public async Task AdminMethod_ShouldDenyAccess_WithUserRole()
        {
            // Arrange
            _middleware.RegisterMethodPermission("admin.method", new[] { "Admin" });
            var request = new JsonRpcRequest
            {
                Method = "admin.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            _mockTokenProvider
                .Setup(x => x.ValidateTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTokenProvider
                .Setup(x => x.GetClaimsFromTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, string> { { "role", "User" } });

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, "valid_token");

            // Assert
            Assert.False(result.IsAuthorized);
            Assert.Equal(403, result.ErrorCode); // Forbidden
        }

        [Fact]
        public async Task AdminMethod_ShouldAllowAccess_WithAdminRole()
        {
            // Arrange
            _middleware.RegisterMethodPermission("admin.method", new[] { "Admin" });
            var request = new JsonRpcRequest
            {
                Method = "admin.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            _mockTokenProvider
                .Setup(x => x.ValidateTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTokenProvider
                .Setup(x => x.GetClaimsFromTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, string> { { "role", "Admin" } });

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, "valid_token");

            // Assert
            Assert.True(result.IsAuthorized);
        }

        [Fact]
        public async Task MultiRoleMethod_ShouldAllowAccess_WithAnyRole()
        {
            // Arrange
            _middleware.RegisterMethodPermission("multi.method", new[] { "Admin", "Manager" });
            var request = new JsonRpcRequest
            {
                Method = "multi.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            _mockTokenProvider
                .Setup(x => x.ValidateTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTokenProvider
                .Setup(x => x.GetClaimsFromTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, string> { { "role", "Manager" } });

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, "valid_token");

            // Assert
            Assert.True(result.IsAuthorized);
        }

        [Fact]
        public async Task UnregisteredMethod_ShouldDenyAccess_WithoutToken()
        {
            // Arrange
            var request = new JsonRpcRequest
            {
                Method = "unknown.method",
                Id = "1",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var result = await _middleware.AuthorizeRequestAsync(request, null);

            // Assert
            Assert.False(result.IsAuthorized);
            Assert.Equal(401, result.ErrorCode); // Unauthorized
        }
    }
}
