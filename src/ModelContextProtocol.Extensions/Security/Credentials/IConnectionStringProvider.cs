using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Interface for providing connection strings
    /// </summary>
    public interface IConnectionStringProvider
    {
        /// <summary>
        /// Gets a connection string by name
        /// </summary>
        /// <param name="name">The name of the connection string</param>
        /// <returns>The connection string</returns>
        Task<string> GetConnectionStringAsync(string name);

        /// <summary>
        /// Gets a sanitized connection string (with sensitive information removed) for logging
        /// </summary>
        /// <param name="connectionString">The original connection string</param>
        /// <returns>A sanitized version of the connection string</returns>
        string GetSanitizedConnectionString(string connectionString);
    }
}
