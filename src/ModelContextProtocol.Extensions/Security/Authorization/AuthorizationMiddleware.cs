using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Extensions.Security.Authentication;

namespace ModelContextProtocol.Extensions.Security.Authorization
{
    /// <summary>
    /// Middleware for enforcing authorization rules on MCP requests
    /// </summary>
    public class AuthorizationMiddleware
    {
        private readonly IJwtTokenProvider _tokenProvider;
        private readonly ILogger<AuthorizationMiddleware> _logger;
        private readonly Dictionary<string, MethodPermission> _methodPermissions;

        public AuthorizationMiddleware(
            IJwtTokenProvider tokenProvider,
            ILogger<AuthorizationMiddleware> logger)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _methodPermissions = new Dictionary<string, MethodPermission>();
        }

        /// <summary>
        /// Registers a method with required permissions
        /// </summary>
        /// <param name="methodName">Name of the MCP method</param>
        /// <param name="requiredRoles">Roles authorized to use this method</param>
        /// <param name="requiresAuthentication">Whether authentication is required</param>
        public void RegisterMethodPermission(string methodName, IEnumerable<string> requiredRoles, bool requiresAuthentication = true)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));
            
            var permission = new MethodPermission
            {
                RequiresAuthentication = requiresAuthentication,
                RequiredRoles = new HashSet<string>(requiredRoles ?? Array.Empty<string>())
            };
            
            _methodPermissions[methodName] = permission;
            _logger.LogDebug("Registered method {Method} with permissions. Auth required: {RequiresAuth}, Roles: {Roles}",
                methodName, requiresAuthentication, string.Join(", ", permission.RequiredRoles));
        }

        /// <summary>
        /// Registers a method as publicly accessible (no authentication required)
        /// </summary>
        /// <param name="methodName">Name of the MCP method</param>
        public void RegisterPublicMethod(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));
            
            var permission = new MethodPermission
            {
                RequiresAuthentication = false,
                RequiredRoles = new HashSet<string>()
            };
            
            _methodPermissions[methodName] = permission;
            _logger.LogDebug("Registered public method {Method}", methodName);
        }

        /// <summary>
        /// Authorizes a request based on the JWT token and method permissions
        /// </summary>
        /// <param name="request">The JSON-RPC request</param>
        /// <param name="authToken">The authorization token from the request</param>
        /// <returns>Result of authorization check</returns>
        public async Task<AuthorizationResult> AuthorizeRequestAsync(JsonRpcRequest request, string authToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            // Check if method is registered
            if (!_methodPermissions.TryGetValue(request.Method, out var permission))
            {
                // If method is not explicitly registered, default to requiring authentication
                _logger.LogWarning("Method {Method} has no registered permissions, defaulting to secured", request.Method);
                permission = new MethodPermission
                {
                    RequiresAuthentication = true,
                    RequiredRoles = new HashSet<string>()
                };
            }
            
            // If method doesn't require authentication, allow it
            if (!permission.RequiresAuthentication)
            {
                return new AuthorizationResult { IsAuthorized = true };
            }
            
            // Check if token is provided
            if (string.IsNullOrEmpty(authToken))
            {
                _logger.LogWarning("Authorization token missing for secured method {Method}", request.Method);
                return new AuthorizationResult
                {
                    IsAuthorized = false,
                    ErrorCode = (int)HttpStatusCode.Unauthorized,
                    ErrorMessage = "Authorization token required"
                };
            }
            
            // Validate token
            if (!await _tokenProvider.ValidateTokenAsync(authToken))
            {
                _logger.LogWarning("Invalid authorization token for method {Method}", request.Method);
                return new AuthorizationResult
                {
                    IsAuthorized = false,
                    ErrorCode = (int)HttpStatusCode.Unauthorized,
                    ErrorMessage = "Invalid or expired token"
                };
            }
            
            // If role requirements exist, check them
            if (permission.RequiredRoles.Count > 0)
            {
                var claims = await _tokenProvider.GetClaimsFromTokenAsync(authToken);
                bool hasRequiredRole = false;
                
                // Check if user has any of the required roles
                if (claims.TryGetValue("role", out var userRoles))
                {
                    // Multiple roles might be in a comma-separated list
                    foreach (var role in userRoles.Split(','))
                    {
                        if (permission.RequiredRoles.Contains(role.Trim()))
                        {
                            hasRequiredRole = true;
                            break;
                        }
                    }
                }
                
                if (!hasRequiredRole)
                {
                    _logger.LogWarning("User lacks required role for method {Method}", request.Method);
                    return new AuthorizationResult
                    {
                        IsAuthorized = false,
                        ErrorCode = (int)HttpStatusCode.Forbidden,
                        ErrorMessage = "Insufficient permissions"
                    };
                }
            }
            
            return new AuthorizationResult { IsAuthorized = true };
        }

        /// <summary>
        /// Model for method permissions
        /// </summary>
        private class MethodPermission
        {
            public bool RequiresAuthentication { get; set; }
            public HashSet<string> RequiredRoles { get; set; }
        }
    }

    /// <summary>
    /// Result of an authorization check
    /// </summary>
    public class AuthorizationResult
    {
        public bool IsAuthorized { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
