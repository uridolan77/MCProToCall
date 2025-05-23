using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Interface for a service that resolves connection strings
    /// </summary>
    public interface IConnectionStringResolverService
    {
        /// <summary>
        /// Resolve a connection string template by replacing placeholders with actual values
        /// </summary>
        /// <param name="connectionStringTemplate">The connection string template</param>
        /// <returns>The resolved connection string</returns>
        Task<string> ResolveConnectionStringAsync(string connectionStringTemplate);

        /// <summary>
        /// Clear all caches
        /// </summary>
        void ClearCaches();
    }
}
