using System.Threading.Tasks;

namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// Interface for token storage
    /// </summary>
    public interface ITokenStore
    {
        /// <summary>
        /// Stores a token
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="token">Token</param>
        Task StoreTokenAsync(string username, string token);

        /// <summary>
        /// Checks if a token is valid
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="token">Token</param>
        /// <returns>True if the token is valid, false otherwise</returns>
        Task<bool> IsTokenValidAsync(string username, string token);

        /// <summary>
        /// Revokes a token
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="token">Token</param>
        Task RevokeTokenAsync(string username, string token);
    }
}
