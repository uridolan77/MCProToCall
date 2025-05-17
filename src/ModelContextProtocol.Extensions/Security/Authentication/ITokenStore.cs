using System;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.Authentication
{
    /// <summary>
    /// Interface for storing and retrieving refresh tokens
    /// </summary>
    public interface ITokenStore
    {
        /// <summary>
        /// Stores a refresh token
        /// </summary>
        /// <param name="refreshToken">The refresh token to store</param>
        /// <param name="userId">The user ID associated with the token</param>
        /// <param name="expiration">Token expiration date</param>
        Task StoreRefreshTokenAsync(string refreshToken, string userId, DateTime expiration);
        
        /// <summary>
        /// Validates a refresh token and returns the associated user ID if valid
        /// </summary>
        /// <param name="refreshToken">The refresh token to validate</param>
        /// <returns>User ID if valid, null otherwise</returns>
        Task<string> ValidateRefreshTokenAsync(string refreshToken);
        
        /// <summary>
        /// Revokes a specific refresh token
        /// </summary>
        /// <param name="refreshToken">The token to revoke</param>
        Task RevokeRefreshTokenAsync(string refreshToken);
        
        /// <summary>
        /// Revokes all refresh tokens for a user
        /// </summary>
        /// <param name="userId">User ID to revoke tokens for</param>
        Task RevokeAllUserTokensAsync(string userId);
        
        /// <summary>
        /// Purges expired tokens from storage
        /// </summary>
        Task PurgeExpiredTokensAsync();
    }
}
