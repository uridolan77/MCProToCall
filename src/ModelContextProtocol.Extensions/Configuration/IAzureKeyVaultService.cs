using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Interface for a service that interacts with Azure Key Vault
    /// </summary>
    public interface IAzureKeyVaultService
    {
        /// <summary>
        /// Get a secret from Azure Key Vault
        /// </summary>
        /// <param name="vaultName">The name of the vault</param>
        /// <param name="secretName">The name of the secret</param>
        /// <returns>The secret value</returns>
        Task<string> GetSecretAsync(string vaultName, string secretName);
    }
}
