using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// Middleware for handling authorization
    /// </summary>
    public class AuthorizationMiddleware
    {
        private readonly ILogger<AuthorizationMiddleware> _logger;
        private readonly IJwtTokenProvider _jwtTokenProvider;
        private readonly Dictionary<string, IEnumerable<string>> _methodPermissions = new Dictionary<string, IEnumerable<string>>();
        private readonly HashSet<string> _publicMethods = new HashSet<string>();

        /// <summary>
        /// Creates a new authorization middleware
        /// </summary>
        public AuthorizationMiddleware(
            ILogger<AuthorizationMiddleware> logger,
            IJwtTokenProvider jwtTokenProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jwtTokenProvider = jwtTokenProvider ?? throw new ArgumentNullException(nameof(jwtTokenProvider));
        }

        /// <summary>
        /// Authorizes a request
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>The claims principal if authorized, null otherwise</returns>
        public async Task<ClaimsPrincipal> AuthorizeAsync(HttpListenerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Get the authorization header
            string authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader))
            {
                _logger.LogWarning("No Authorization header found");
                return null;
            }

            // Check if it's a Bearer token
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Authorization header is not a Bearer token");
                return null;
            }

            // Extract the token
            string token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Bearer token is empty");
                return null;
            }

            try
            {
                // Validate the token
                ClaimsPrincipal principal = await _jwtTokenProvider.ValidateTokenAsync(token);
                if (principal == null)
                {
                    _logger.LogWarning("Token validation failed");
                    return null;
                }

                _logger.LogInformation("Token validation succeeded for user {UserId}", principal.Identity.Name);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return null;
            }
        }

        /// <summary>
        /// Authorizes a JSON-RPC request
        /// </summary>
        /// <param name="request">The JSON-RPC request</param>
        /// <param name="token">The authorization token</param>
        /// <returns>Authorization result</returns>
        public async Task<AuthorizationResult> AuthorizeRequestAsync(JsonRpcRequest request, string token)
        {
            // Check if the method is public
            if (_publicMethods.Contains(request.Method))
            {
                return AuthorizationResult.Success();
            }

            // If no token is provided, the request is not authorized
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No token provided for method {Method}", request.Method);
                return AuthorizationResult.Fail(401, "Authentication required");
            }

            try
            {
                // Validate the token
                ClaimsPrincipal principal = await _jwtTokenProvider.ValidateTokenAsync(token);
                if (principal == null)
                {
                    _logger.LogWarning("Token validation failed for method {Method}", request.Method);
                    return AuthorizationResult.Fail(401, "Invalid token");
                }

                // Check if the method requires specific roles
                if (_methodPermissions.TryGetValue(request.Method, out var requiredRoles))
                {
                    foreach (var role in requiredRoles)
                    {
                        if (principal.IsInRole(role))
                        {
                            _logger.LogInformation("User {UserId} authorized for method {Method} with role {Role}",
                                principal.Identity.Name, request.Method, role);
                            return AuthorizationResult.Success();
                        }
                    }

                    _logger.LogWarning("User {UserId} does not have required roles for method {Method}",
                        principal.Identity.Name, request.Method);
                    return AuthorizationResult.Fail(403, "Insufficient permissions");
                }

                // If no specific roles are required, the request is authorized
                _logger.LogInformation("User {UserId} authorized for method {Method}",
                    principal.Identity.Name, request.Method);
                return AuthorizationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authorizing request for method {Method}", request.Method);
                return AuthorizationResult.Fail(500, "Authorization error");
            }
        }

        /// <summary>
        /// Registers a method as publicly accessible (no authentication required)
        /// </summary>
        /// <param name="methodName">The method name</param>
        public void RegisterPublicMethod(string methodName)
        {
            _publicMethods.Add(methodName);
            _logger.LogInformation("Registered public method: {MethodName}", methodName);
        }

        /// <summary>
        /// Registers the required roles for a method
        /// </summary>
        /// <param name="methodName">The method name</param>
        /// <param name="requiredRoles">The required roles</param>
        public void RegisterMethodPermission(string methodName, IEnumerable<string> requiredRoles)
        {
            _methodPermissions[methodName] = requiredRoles;
            _logger.LogInformation("Registered method permissions for {MethodName}", methodName);
        }

        /// <summary>
        /// Checks if a claims principal has a specific role
        /// </summary>
        public bool HasRole(ClaimsPrincipal principal, string role)
        {
            if (principal == null)
            {
                return false;
            }

            return principal.IsInRole(role);
        }

        /// <summary>
        /// Checks if a claims principal has a specific claim
        /// </summary>
        public bool HasClaim(ClaimsPrincipal principal, string type, string value)
        {
            if (principal == null)
            {
                return false;
            }

            return principal.HasClaim(type, value);
        }
    }
}
