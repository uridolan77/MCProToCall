using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// Interface for JWT token provider
    /// </summary>
    public interface IJwtTokenProvider
    {
        /// <summary>
        /// Generates a JWT token
        /// </summary>
        /// <param name="claims">The claims to include in the token</param>
        /// <param name="expires">When the token expires</param>
        /// <returns>The generated token</returns>
        Task<string> GenerateTokenAsync(IEnumerable<Claim> claims, DateTime expires);

        /// <summary>
        /// Validates a JWT token
        /// </summary>
        /// <param name="token">The token to validate</param>
        /// <returns>The claims from the token if valid, null otherwise</returns>
        Task<ClaimsPrincipal> ValidateTokenAsync(string token);

        /// <summary>
        /// Revokes a JWT token
        /// </summary>
        /// <param name="token">The token to revoke</param>
        Task RevokeTokenAsync(string token);

        /// <summary>
        /// Refreshes a JWT token
        /// </summary>
        /// <param name="token">The token to refresh</param>
        /// <param name="expires">When the new token expires</param>
        /// <returns>The new token</returns>
        Task<string> RefreshTokenAsync(string token, DateTime expires);
    }
}
