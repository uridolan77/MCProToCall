using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Security.Authentication;
using ModelContextProtocol.Extensions.Security.Authorization;
using ModelContextProtocol.Extensions.Validation;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding MCP security services to the DI container
    /// </summary>
    public static class McpSecurityExtensions
    {
        /// <summary>
        /// Adds MCP security services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            // Get server options from configuration
            var serverOptions = configuration.GetSection("McpServer").Get<McpServerOptions>();
            if (serverOptions?.EnableAuthentication == true)
            {
                // Add JWT token provider
                services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
                
                // Add token store (in-memory for now, can be replaced with a persistent implementation)
                services.AddSingleton<ITokenStore, InMemoryTokenStore>();
                
                // Add authorization middleware
                services.AddSingleton<AuthorizationMiddleware>();
            }
            
            // Add input validator
            services.AddSingleton<InputValidator>();
            
            return services;
        }
    }
}
