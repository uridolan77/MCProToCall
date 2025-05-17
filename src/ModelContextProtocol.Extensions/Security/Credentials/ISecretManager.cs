using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Interface for managing secrets securely
    /// </summary>
    public interface ISecretManager
    {
        /// <summary>
        /// Gets a secret by name
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        Task<string> GetSecretAsync(string secretName);

        /// <summary>
        /// Gets a secret by name with rotation capabilities
        /// </summary>
        /// <param name="secretName">The name of the secret to retrieve</param>
        /// <returns>The secret value</returns>
        Task<string> GetSecretWithRotationAsync(string secretName);

        /// <summary>
        /// Sets a secret value
        /// </summary>
        /// <param name="secretName">The name of the secret</param>
        /// <param name="secretValue">The value of the secret</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetSecretAsync(string secretName, string secretValue);

        /// <summary>
        /// Checks if a secret needs rotation based on age or other criteria
        /// </summary>
        /// <param name="secretName">The name of the secret to check</param>
        /// <returns>True if the secret needs rotation, false otherwise</returns>
        Task<bool> IsRotationNeededAsync(string secretName);

        /// <summary>
        /// Rotates a secret by generating a new value
        /// </summary>
        /// <param name="secretName">The name of the secret to rotate</param>
        /// <returns>The new secret value</returns>
        Task<string> RotateSecretAsync(string secretName);
    }
}
