using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PPrePorter.Core.Interfaces;
using PPrePorter.Core.Services;

namespace PPrePorter.Core.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering connection string resolver services
    /// </summary>
    public static class ConnectionStringResolverExtensions
    {
        /// <summary>
        /// Adds connection string resolver services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddConnectionStringResolver(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure connection string resolver options
            services.Configure<ConnectionStringResolverOptions>(configuration.GetSection("ConnectionStringResolver"));
            
            // Register the connection string resolver
            services.AddSingleton<IAzureKeyVaultConnectionStringResolver, AzureKeyVaultConnectionStringResolver>();
            
            return services;
        }
    }
}
