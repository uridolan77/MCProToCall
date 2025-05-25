using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.HSM
{
    /// <summary>
    /// Factory interface for creating hardware security module instances
    /// </summary>
    public interface IHardwareSecurityModuleFactory
    {
        /// <summary>
        /// Creates an HSM instance based on the provider type
        /// </summary>
        /// <param name="providerType">Type of HSM provider</param>
        /// <returns>HSM instance</returns>
        IHardwareSecurityModule Create(string providerType);
    }



    /// <summary>
    /// Configuration options for HSM
    /// </summary>
    public class HsmOptions
    {
        public string ConnectionString { get; set; }
        public string ProviderType { get; set; } = "AzureKeyVault";
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxRetryAttempts { get; set; } = 3;
        public bool EnableCaching { get; set; } = true;
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(15);
    }






}
