using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.Authentication
{
    /// <summary>
    /// Interface for JWT token generation and validation
    /// </summary>
    public interface IJwtTokenProvider
    {
        /// <summary>
        /// Generates a new JWT access token
        /// </summary>
        /// <param name="userId">User ID claim</param>
        /// <param name="roles">User roles</param>
        /// <param name="additionalClaims">Additional claims to include</param>
        /// <returns>JWT token string</returns>
        Task<string> GenerateAccessTokenAsync(string userId, IEnumerable<string> roles, IDictionary<string, string> additionalClaims = null);
        
        /// <summary>
        /// Generates a new refresh token
        /// </summary>
        /// <param name="userId">User ID the token is for</param>
        /// <returns>Refresh token and its expiration</returns>
        Task<(string Token, DateTime Expiration)> GenerateRefreshTokenAsync(string userId);
        
        /// <summary>
        /// Validates a JWT token
        /// </summary>
        /// <param name="token">The token to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        Task<bool> ValidateTokenAsync(string token);
        
        /// <summary>
        /// Gets claims from a token
        /// </summary>
        /// <param name="token">The token to extract claims from</param>
        /// <returns>Dictionary of claims</returns>
        Task<IDictionary<string, string>> GetClaimsFromTokenAsync(string token);
        
        /// <summary>
        /// Validates a refresh token and issues new access and refresh tokens
        /// </summary>
        /// <param name="refreshToken">The refresh token to validate</param>
        /// <returns>New access token, refresh token, and expiration</returns>
        Task<(string AccessToken, string RefreshToken, DateTime Expiration)> RefreshTokenAsync(string refreshToken);
        
        /// <summary>
        /// Revokes a specific refresh token
        /// </summary>
        /// <param name="refreshToken">The token to revoke</param>
        Task RevokeTokenAsync(string refreshToken);
        
        /// <summary>
        /// Revokes all refresh tokens for a user
        /// </summary>
        /// <param name="userId">User ID to revoke tokens for</param>
        Task RevokeAllUserTokensAsync(string userId);
    }
}
