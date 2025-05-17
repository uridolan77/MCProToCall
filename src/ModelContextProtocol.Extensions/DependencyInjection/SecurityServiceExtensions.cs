using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Credentials;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring security services
    /// </summary>
    public static class SecurityServiceExtensions
    {
        /// <summary>
        /// Adds security services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Add TLS services
            services.Configure<TlsOptions>(configuration.GetSection("Tls"));
            services.AddSingleton<ICertificateValidator, CertificateValidator>();
            services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            services.AddSingleton<ICertificatePinningService, CertificatePinningService>();

            // Add credential management services
            services.Configure<KeyVaultOptions>(configuration.GetSection("KeyVault"));
            services.Configure<ConnectionStringOptions>(configuration.GetSection("ConnectionStrings"));
            services.AddSingleton<ISecretManager, KeyVaultSecretManager>();
            services.AddSingleton<IConnectionStringProvider, CachedConnectionStringProvider>();
            
            // Add memory cache for connection string caching
            services.AddMemoryCache();

            return services;
        }

        /// <summary>
        /// Adds Azure Key Vault integration to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddKeyVaultIntegration(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Configure Key Vault options
            services.Configure<KeyVaultOptions>(configuration.GetSection("KeyVault"));
            
            // Register the secret manager
            services.AddSingleton<ISecretManager, KeyVaultSecretManager>();

            return services;
        }

        /// <summary>
        /// Adds connection string management to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddConnectionStringManagement(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Configure connection string options
            services.Configure<ConnectionStringOptions>(configuration.GetSection("ConnectionStrings"));
            
            // Add memory cache for connection string caching
            services.AddMemoryCache();
            
            // Register the connection string provider
            services.AddSingleton<IConnectionStringProvider, CachedConnectionStringProvider>();

            return services;
        }
    }
}
