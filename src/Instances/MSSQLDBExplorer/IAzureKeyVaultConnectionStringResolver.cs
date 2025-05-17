using System.Threading.Tasks;

namespace PPrePorter.Core.Interfaces
{
    /// <summary>
    /// Interface for a service that resolves connection strings with Azure Key Vault placeholders
    /// </summary>
    public interface IAzureKeyVaultConnectionStringResolver
    {
        /// <summary>
        /// Resolve a connection string by replacing Azure Key Vault placeholders with actual values
        /// </summary>
        /// <param name="connectionString">The connection string with Azure Key Vault placeholders</param>
        /// <returns>The resolved connection string</returns>
        Task<string> ResolveConnectionStringAsync(string connectionString);
    }
}
