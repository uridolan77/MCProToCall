using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Authentication;
using ModelContextProtocol.Extensions.Validation;
using ModelContextProtocol.Server;
using ModelContextProtocol.Server.Security.Authentication;
using ModelContextProtocol.Server.Security.TLS;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring MCP server services
    /// </summary>
    public static class McpServerExtensions
    {
        /// <summary>
        /// Adds the MCP server services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpServer(this IServiceCollection services)
        {
            services.AddSingleton<IMcpServer, McpServer>();

            return services;
        }

        /// <summary>
        /// Adds the MCP server with configuration from appsettings
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<McpServerOptions>(options =>
            {
                configuration.GetSection("McpServer").Bind(options);
            });
            services.AddSingleton<IMcpServer, McpServer>();

            return services;
        }

        /// <summary>
        /// Adds MCP server input validation
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpInputValidation(this IServiceCollection services)
        {
            services.AddSingleton<InputValidator>();
            return services;
        }

        /// <summary>
        /// Adds MCP server authentication
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure JWT options from appsettings
            services.Configure<JwtOptions>(options =>
            {
                var jwtSection = configuration.GetSection("McpServer:JwtAuth");
                options.SecretKey = jwtSection["SecretKey"];
                options.Issuer = jwtSection["Issuer"];
                options.Audience = jwtSection["Audience"];

                if (int.TryParse(jwtSection["AccessTokenExpirationMinutes"], out var minutes))
                {
                    options.AccessTokenExpirationMinutes = minutes;
                }

                if (int.TryParse(jwtSection["RefreshTokenExpirationDays"], out var days))
                {
                    options.RefreshTokenExpirationDays = days;
                }
            });

            // Register token services
            services.AddSingleton<ITokenStore, InMemoryTokenStore>();
            services.AddSingleton<ModelContextProtocol.Extensions.Security.Authentication.IJwtTokenProvider, ModelContextProtocol.Extensions.Security.Authentication.JwtTokenProvider>();

            return services;
        }

        /// <summary>
        /// Adds MCP server authorization
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpAuthorization(this IServiceCollection services)
        {
            services.AddSingleton<AuthorizationMiddleware>();
            return services;
        }

        /// <summary>
        /// Adds all MCP security features (validation, authentication, authorization)
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpSecurity(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            return services
                .AddMcpInputValidation()
                .AddMcpAuthentication(configuration)
                .AddMcpAuthorization();
        }

        /// <summary>
        /// Adds TLS configuration for the MCP server
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure TLS options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpTls(
            this IServiceCollection services,
            Action<TlsOptions> configureOptions)
        {
            services.Configure<TlsOptions>(configureOptions);
            return services;
        }

        /// <summary>
        /// Adds TLS configuration for the MCP server from configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpTls(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<TlsOptions>(options =>
            {
                configuration.GetSection("McpServer:Tls").Bind(options);
            });
            return services;
        }

        /// <summary>
        /// Adds comprehensive MCP server security including TLS, authentication and authorization
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpSecureServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure server options from configuration
            services.Configure<McpServerOptions>(options =>
            {
                configuration.GetSection("McpServer").Bind(options);
            });

            // Register base services
            services.AddSingleton<IMcpServer, McpServer>();

            // Register security services
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificateValidator, ModelContextProtocol.Extensions.Security.CertificateValidator>();
            services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificatePinningService, ModelContextProtocol.Extensions.Security.CertificatePinningService>();
            services.AddSingleton(typeof(ModelContextProtocol.Extensions.Security.TlsConnectionManager));

            // Register authentication services if enabled
            var authEnabled = configuration.GetValue<bool>("McpServer:EnableAuthentication");
            if (authEnabled)
            {
                services.AddSingleton<ModelContextProtocol.Extensions.Security.Authentication.IJwtTokenProvider, ModelContextProtocol.Extensions.Security.Authentication.JwtTokenProvider>();
                services.AddSingleton<ITokenStore, InMemoryTokenStore>();
                services.AddSingleton<AuthorizationMiddleware>();
                services.AddSingleton<IInputValidator>(provider =>
                new InputValidator(provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InputValidator>>()));
            }

            return services;
        }

        /// <summary>
        /// Adds the MCP server with enhanced TLS security features and custom options
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpSecureServer(
            this IServiceCollection services,
            Action<McpServerOptions> configureOptions)
        {
            // Configure server options from action
            services.Configure(configureOptions);

            // Register base services
            services.AddSingleton<IMcpServer, McpServer>();

            // Register security services
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificateValidator, ModelContextProtocol.Extensions.Security.CertificateValidator>();
            services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificatePinningService, ModelContextProtocol.Extensions.Security.CertificatePinningService>();
            services.AddSingleton(typeof(ModelContextProtocol.Extensions.Security.TlsConnectionManager));

            return services;
        }

        /// <summary>
        /// Adds TLS middleware services for certificate validation, revocation checking, and pinning
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddMcpTlsMiddleware(this IServiceCollection services)
        {
            // Add HTTP client factory for CRL downloads
            services.AddHttpClient();

            // Configure HTTP client
            services.Configure<HttpClientFactoryOptions>("CrlDownloader", options =>
            {
                options.HttpClientActions.Add(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "MCP TLS Certificate Revocation Checker");
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
            });

            // Register TLS connection manager
            services.AddSingleton(typeof(ModelContextProtocol.Extensions.Security.TlsConnectionManager));

            // Register certificate validation services
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificateValidator, ModelContextProtocol.Extensions.Security.CertificateValidator>();
            services.AddSingleton<ICertificateRevocationChecker, CertificateRevocationChecker>();
            services.AddSingleton<ModelContextProtocol.Extensions.Security.ICertificatePinningService, ModelContextProtocol.Extensions.Security.CertificatePinningService>();

            return services;
        }
    }

    /// <summary>
    /// TLS configuration options
    /// </summary>
    public class TlsOptions
    {
        /// <summary>
        /// Path to the server certificate file
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// Password for the server certificate
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// Thumbprint of the server certificate in the certificate store
        /// </summary>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Whether to require client certificates
        /// </summary>
        public bool RequireClientCertificate { get; set; } = false;

        /// <summary>
        /// List of allowed client certificate thumbprints (if empty, all valid client certificates are accepted)
        /// </summary>
        public List<string> AllowedClientCertificateThumbprints { get; set; } = new List<string>();

        /// <summary>
        /// Whether to check certificate revocation
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = true;

        /// <summary>
        /// The mode to use for certificate revocation checking
        /// </summary>
        public RevocationCheckMode RevocationCheckMode { get; set; } = RevocationCheckMode.OcspAndCrl;

        /// <summary>
        /// How to handle revocation check failures
        /// </summary>
        public RevocationFailureMode RevocationFailureMode { get; set; } = RevocationFailureMode.Deny;

        /// <summary>
        /// Path to store the revocation cache
        /// </summary>
        public string RevocationCachePath { get; set; } = "certs/revocation";

        /// <summary>
        /// How often to update the CRL in hours
        /// </summary>
        public int CrlUpdateIntervalHours { get; set; } = 24;

        /// <summary>
        /// Whether to allow untrusted certificates in development mode
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; } = false;

        /// <summary>
        /// Whether to use certificate pinning
        /// </summary>
        public bool UseCertificatePinning { get; set; } = false;

        /// <summary>
        /// List of pinned certificate paths
        /// </summary>
        public List<string> PinnedCertificates { get; set; } = new List<string>();

        /// <summary>
        /// Path to store the certificate pin information
        /// </summary>
        public string CertificatePinStoragePath { get; set; } = "certs/pins";

        /// <summary>
        /// Whether to require exact certificate matches for pinning
        /// </summary>
        public bool RequireExactCertificateMatch { get; set; } = true;

        /// <summary>
        /// Whether to allow connections when pinning validation fails
        /// </summary>
        public bool AllowOnPinningFailure { get; set; } = false;

        /// <summary>
        /// Maximum number of TLS connections allowed per client IP address
        /// </summary>
        public int MaxConnectionsPerIpAddress { get; set; } = 100;

        /// <summary>
        /// Time window in seconds for rate limiting connections
        /// </summary>
        public int ConnectionRateLimitingWindowSeconds { get; set; } = 60;
    }
}