using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Extensions.Client;
using ModelContextProtocol.Extensions.Security;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring MCP client services
    /// </summary>
    public static class McpClientExtensions
    {
        /// <summary>
        /// Adds the MCP client with the specified options
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="options">Client options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpClient(this IServiceCollection services, McpClientOptions options)
        {
            services.AddSingleton(options);

            // Register the McpClient
            services.AddSingleton<McpClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<McpClient>>();
                return new McpClient(options, logger);
            });

            // Register the adapter that implements IMcpClient
            services.AddSingleton<IMcpClient>(sp =>
            {
                var client = sp.GetRequiredService<McpClient>();
                var logger = sp.GetRequiredService<ILogger<McpClientAdapter>>();
                return new McpClientAdapter(client, logger);
            });

            services.AddHttpClient("McpClient", (sp, client) =>
                {
                    var logger = sp.GetRequiredService<ILogger<McpClient>>();

                    // Configure base address
                    string protocol = options.UseTls ? "https" : "http";
                    client.BaseAddress = new Uri($"{protocol}://{options.Host}:{options.Port}/");

                    // Configure timeout
                    if (options.Timeout > TimeSpan.Zero)
                    {
                        client.Timeout = options.Timeout;
                    }

                    // Add auth token if provided
                    if (!string.IsNullOrEmpty(options.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AuthToken);
                        logger.LogDebug("Added authorization token to client");
                    }

                    logger.LogInformation("Configured MCP client for {BaseAddress}", client.BaseAddress);
                });

            return services;
        }

        /// <summary>
        /// Adds the MCP client with configuration from appsettings
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var options = new McpClientOptions();
            configuration.GetSection("McpClient").Bind(options);

            // Handle client certificate if configured
            if (options.UseTls && !string.IsNullOrEmpty(options.ClientCertificatePath))
            {
                // Add client with certificate
                services.AddSingleton(options);

                // Register the McpClient
                services.AddSingleton<McpClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<McpClient>>();
                    return new McpClient(options, logger);
                });

                // Register the adapter that implements IMcpClient
                services.AddSingleton<IMcpClient>(sp =>
                {
                    var client = sp.GetRequiredService<McpClient>();
                    var logger = sp.GetRequiredService<ILogger<McpClientAdapter>>();
                    return new McpClientAdapter(client, logger);
                });

                return services;
            }

            return services.AddMcpClient(options);
        }

        /// <summary>
        /// Adds the MCP client with enhanced TLS security features and configurations
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddSecureMcpClient(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure client options from configuration
            var options = configuration.GetSection("McpClient").Get<McpClientOptions>();

            // Register security services for client
            services.AddSingleton<ICertificateValidator, CertificateValidator>();
            services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            services.AddSingleton<ICertificatePinningService, CertificatePinningService>();

            // Configure HttpClient with security settings
            // Register the McpClient
            services.AddSingleton<McpClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<McpClient>>();
                return new McpClient(options, logger);
            });

            // Register the adapter that implements IMcpClient
            services.AddSingleton<IMcpClient>(sp =>
            {
                var client = sp.GetRequiredService<McpClient>();
                var logger = sp.GetRequiredService<ILogger<McpClientAdapter>>();
                return new McpClientAdapter(client, logger);
            });

            services.AddHttpClient("McpClient", (sp, client) =>
                {
                    var logger = sp.GetRequiredService<ILogger<McpClient>>();

                    // Configure base address
                    string protocol = options.UseTls ? "https" : "http";
                    client.BaseAddress = new Uri($"{protocol}://{options.Host}:{options.Port}/");

                    // Configure timeout
                    if (options.Timeout > TimeSpan.Zero)
                    {
                        client.Timeout = options.Timeout;
                    }

                    // Add auth token if provided
                    if (!string.IsNullOrEmpty(options.AuthToken))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AuthToken);
                        logger.LogDebug("Added authorization token to client");
                    }
                });

            services.AddHttpClient("McpClient").ConfigurePrimaryHttpMessageHandler((sp) =>
                {
                    var logger = sp.GetRequiredService<ILogger<McpClient>>();
                    var certificateValidator = sp.GetService<ICertificateValidator>();
                    var pinningService = sp.GetService<ICertificatePinningService>();

                    // Create a handler with TLS settings
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) =>
                        {
                            if (!options.UseTls)
                                return true;

                            // Custom validation callback
                            if (options.ServerCertificateValidationCallback != null)
                            {
                                return options.ServerCertificateValidationCallback(sender, cert, chain, errors);
                            }

                            // Override with our validator if available
                            if (certificateValidator != null)
                            {
                                var certificate = new X509Certificate2(cert);

                                // Check certificate pinning if enabled
                                if (options.EnableCertificatePinning && pinningService != null)
                                {
                                    // Load server certificate pin
                                    if (options.ServerCertificatePin != null)
                                    {
                                        pinningService.AddCertificatePin(options.ServerCertificatePin, true);
                                    }
                                    else if (!string.IsNullOrEmpty(options.ServerCertificatePinPath) &&
                                            System.IO.File.Exists(options.ServerCertificatePinPath))
                                    {
                                        var pinnedCert = new X509Certificate2(options.ServerCertificatePinPath);
                                        pinningService.AddCertificatePin(pinnedCert, true);
                                        pinnedCert.Dispose();
                                    }

                                    // Validate against pinned certificates
                                    if (!pinningService.ValidateCertificatePin(certificate))
                                    {
                                        logger.LogWarning("Server certificate does not match pinned certificate");
                                        return false;
                                    }
                                }

                                // Validate certificate (including revocation if enabled)
                                return certificateValidator.ValidateServerCertificate(sender, cert, chain, errors);
                            }

                            // Fall back to default behavior based on the AllowUntrustedServerCertificate option
                            return options.AllowUntrustedServerCertificate || errors == System.Net.Security.SslPolicyErrors.None;
                        }
                    };

                    // Configure client certificate for mutual TLS
                    if (options.UseTls)
                    {
                        if (options.ClientCertificate != null)
                        {
                            handler.ClientCertificates.Add(options.ClientCertificate);
                            logger.LogDebug("Added client certificate from memory");
                        }
                        else if (!string.IsNullOrEmpty(options.ClientCertificatePath) &&
                                System.IO.File.Exists(options.ClientCertificatePath))
                        {
                            try
                            {
                                X509Certificate2 clientCert;
                                if (!string.IsNullOrEmpty(options.ClientCertificatePassword))
                                {
                                    clientCert = new X509Certificate2(
                                        options.ClientCertificatePath,
                                        options.ClientCertificatePassword,
                                        X509KeyStorageFlags.MachineKeySet |
                                        X509KeyStorageFlags.PersistKeySet |
                                        X509KeyStorageFlags.Exportable);
                                }
                                else
                                {
                                    clientCert = new X509Certificate2(options.ClientCertificatePath);
                                }

                                handler.ClientCertificates.Add(clientCert);
                                logger.LogDebug("Added client certificate from file: {CertPath}", options.ClientCertificatePath);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to load client certificate from {CertPath}", options.ClientCertificatePath);
                                throw;
                            }
                        }
                    }

                    // Configure certificate revocation checking
                    handler.CheckCertificateRevocationList = options.EnableRevocationCheck;

                    return handler;
                });

            // Register client options instance
            services.AddSingleton(options);

            return services;
        }
    }
}