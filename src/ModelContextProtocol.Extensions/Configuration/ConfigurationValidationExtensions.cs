using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Extension methods for configuration validation
    /// </summary>
    public static class ConfigurationValidationExtensions
    {
        /// <summary>
        /// Adds validated options to the service collection
        /// </summary>
        /// <typeparam name="TOptions">Options type</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="configSection">Configuration section</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddValidatedOptions<TOptions>(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection) where TOptions : class
        {
            services.AddSingleton<ConfigurationValidator>();
            
            services.AddSingleton<IConfigureOptions<TOptions>>(sp =>
                new ValidatingOptionsSetup<TOptions>(
                    configuration,
                    configSection,
                    sp.GetRequiredService<ConfigurationValidator>(),
                    sp.GetRequiredService<ILogger<ValidatingOptionsSetup<TOptions>>>()));

            services.AddSingleton<IPostConfigureOptions<TOptions>>(sp =>
                new ValidatingOptionsSetup<TOptions>(
                    configuration,
                    configSection,
                    sp.GetRequiredService<ConfigurationValidator>(),
                    sp.GetRequiredService<ILogger<ValidatingOptionsSetup<TOptions>>>()));

            return services;
        }

        /// <summary>
        /// Adds MCP server with configuration validation
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddMcpServerWithValidation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add validated options
            services.AddValidatedOptions<ValidatedMcpServerOptions>(configuration, "McpServer");

            // Add the server
            services.AddSingleton<IMcpServer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ValidatedMcpServerOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                
                return new McpServer(
                    Options.Create((McpServerOptions)options),
                    logger);
            });

            return services;
        }
    }

    /// <summary>
    /// Startup configuration validator
    /// </summary>
    public static class StartupConfigurationValidator
    {
        /// <summary>
        /// Validates the configuration at startup
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="logger">Logger</param>
        public static void ValidateConfiguration(IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Validating configuration...");

            var errors = new System.Collections.Generic.List<string>();

            // Check required sections
            if (!configuration.GetSection("McpServer").Exists())
            {
                errors.Add("Required configuration section 'McpServer' is missing");
            }

            // Validate environment
            var environment = configuration["Environment"];
            if (string.IsNullOrEmpty(environment))
            {
                logger.LogWarning("Environment not specified, defaulting to Production");
            }

            // Validate logging configuration
            var logLevel = configuration["Logging:LogLevel:Default"];
            if (string.IsNullOrEmpty(logLevel))
            {
                logger.LogWarning("Default log level not specified");
            }

            // Production-specific checks
            if (environment?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true)
            {
                var useTls = configuration.GetValue<bool>("McpServer:UseTls");
                if (!useTls)
                {
                    errors.Add("TLS must be enabled in Production environment");
                }

                var allowUntrusted = configuration.GetValue<bool>("McpServer:AllowUntrustedCertificates");
                if (allowUntrusted)
                {
                    errors.Add("Untrusted certificates must not be allowed in Production");
                }
            }

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    logger.LogError("Configuration error: {Error}", error);
                }
                
                throw new ConfigurationException("Configuration validation failed. See logs for details.");
            }

            logger.LogInformation("Configuration validation completed successfully");
        }
    }
}
