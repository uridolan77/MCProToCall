using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.HSM;
using ModelContextProtocol.Extensions.Resilience;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.Validation;
using ModelContextProtocol.Extensions.Testing;
using ModelContextProtocol.Extensions.Factories;
using ModelContextProtocol.Extensions.Performance;
using ModelContextProtocol.Extensions.Testing.Chaos;
using ModelContextProtocol.Extensions.WebSocket;
using System;
using System.ComponentModel.DataAnnotations;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Comprehensive service registration extensions for MCP
    /// </summary>
    public static class McpExtensionsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds comprehensive MCP extensions with conditional registration
        /// </summary>
        public static IServiceCollection AddMcpExtensions(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<McpExtensionsOptions> configureOptions = null)
        {
            var options = new McpExtensionsOptions();
            configuration.GetSection("McpExtensions").Bind(options);
            configureOptions?.Invoke(options);

            // Validate options
            var validationContext = new ValidationContext(options);
            var validationResults = options.Validate(validationContext);
            foreach (var result in validationResults)
            {
                throw new InvalidOperationException($"Configuration validation failed: {result.ErrorMessage}");
            }

            return services
                .AddMcpSecurity(configuration, options.Security)
                .AddMcpResilience(configuration, options.Resilience)
                .AddMcpObservability(configuration, options.Observability)
                .AddMcpConfiguration(configuration, options.Configuration)
                .AddMcpValidation(configuration, options.Validation)
                .AddMcpFactories()
                .AddMcpPerformance(configuration)
                .AddMcpWebSocket(configuration)
                .AddMcpChaos(configuration);
        }

        /// <summary>
        /// Adds security services with conditional registration
        /// </summary>
        public static IServiceCollection AddMcpSecurity(
            this IServiceCollection services,
            IConfiguration configuration,
            SecurityOptions securityOptions)
        {
            // Conditional registration based on configuration
            if (securityOptions.EnableCertificateValidation)
            {
                services.AddScoped<ICertificateValidator, CertificateValidator>();

                if (securityOptions.EnableCertificatePinning)
                    services.AddSingleton<ICertificatePinningService, CertificatePinningService>();

                if (securityOptions.EnableRevocationChecking)
                    services.AddScoped<ICertificateRevocationChecker, CertificateRevocationChecker>();
            }

            // HSM registration with factory pattern
            if (securityOptions.EnableHsm)
            {
                services.AddSingleton<IHardwareSecurityModuleFactory>(provider =>
                    new HardwareSecurityModuleFactory(
                        provider.GetRequiredService<ILogger<HardwareSecurityModuleFactory>>(),
                        provider.GetRequiredService<IOptionsMonitor<ModelContextProtocol.Extensions.Security.HSM.HsmOptions>>(),
                        provider));

                services.AddScoped<IHardwareSecurityModule>(provider =>
                {
                    var factory = provider.GetRequiredService<IHardwareSecurityModuleFactory>();
                    var options = provider.GetRequiredService<IOptionsMonitor<ModelContextProtocol.Extensions.Security.HSM.HsmOptions>>().CurrentValue;
                    return factory.Create(securityOptions.HsmProviderType);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds resilience services with conditional patterns
        /// </summary>
        public static IServiceCollection AddMcpResilience(
            this IServiceCollection services,
            IConfiguration configuration,
            ResilienceOptions resilienceOptions)
        {
            // Conditional resilience patterns
            if (resilienceOptions.EnableRateLimiting)
            {
                services.AddSingleton<IRateLimiter>(provider =>
                    resilienceOptions.RateLimitingType switch
                    {
                        "TokenBucket" => new TokenBucketRateLimiter(
                            provider.GetRequiredService<IOptions<RateLimitOptions>>(),
                            provider.GetRequiredService<ILogger<TokenBucketRateLimiter>>()),
                        "SlidingWindow" => new SlidingWindowRateLimiter(
                            provider.GetRequiredService<IOptions<RateLimitOptions>>(),
                            provider.GetRequiredService<ILogger<SlidingWindowRateLimiter>>()),
                        "Adaptive" => new TokenBucketRateLimiter(
                            provider.GetRequiredService<IOptions<RateLimitOptions>>(),
                            provider.GetRequiredService<ILogger<TokenBucketRateLimiter>>()),
                        _ => throw new InvalidOperationException($"Unknown rate limiter type: {resilienceOptions.RateLimitingType}")
                    });
            }

            // Circuit breaker with health checks integration
            if (resilienceOptions.EnableCircuitBreaker)
            {
                services.AddSingleton<KeyVaultCircuitBreaker>();
                // Health checks will be added separately
            }

            return services;
        }

        /// <summary>
        /// Adds observability services with enhanced metrics
        /// </summary>
        public static IServiceCollection AddMcpObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            ObservabilityOptions observabilityOptions)
        {
            if (observabilityOptions.EnableMetrics)
            {
                services.AddSingleton<EnhancedMcpTelemetry>();
                services.AddSingleton<ModelContextProtocol.Extensions.Observability.McpAdvancedTelemetry>();
                services.AddSingleton<ModelContextProtocol.Extensions.Observability.IAlertingService, ModelContextProtocol.Extensions.Observability.AlertingService>();
            }

            if (observabilityOptions.EnableHealthChecks)
            {
                services.AddHealthChecks();
                // Comprehensive health checks will be added separately
            }

            return services;
        }

        /// <summary>
        /// Adds configuration management services
        /// </summary>
        public static IServiceCollection AddMcpConfiguration(
            this IServiceCollection services,
            IConfiguration configuration,
            ConfigurationOptions configOptions)
        {
            if (configOptions.EnableHotReload)
            {
                // Add hot reload capabilities
                services.AddSingleton(typeof(IOptionsMonitor<>), typeof(ValidatedConfigurationReloader<>));
            }

            if (configOptions.EnableDistributedConfig)
            {
                services.AddSingleton<DistributedConfigurationProvider>();
            }

            // Add secrets management
            services.AddSingleton<ModelContextProtocol.Extensions.Configuration.ISecretsManager, ModelContextProtocol.Extensions.Configuration.AzureKeyVaultSecretsManager>();

            return services;
        }

        /// <summary>
        /// Adds advanced caching services
        /// </summary>
        public static IServiceCollection AddMcpCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ModelContextProtocol.Extensions.Caching.MultiTierCacheOptions>(
                configuration.GetSection("McpExtensions:Caching"));
            services.Configure<ModelContextProtocol.Extensions.Caching.CacheInvalidationOptions>(
                configuration.GetSection("McpExtensions:CacheInvalidation"));

            services.AddSingleton<ModelContextProtocol.Extensions.Caching.ICacheInvalidationService, ModelContextProtocol.Extensions.Caching.CacheInvalidationService>();
            services.AddSingleton<ModelContextProtocol.Extensions.Caching.IDistributedMcpCache, ModelContextProtocol.Extensions.Caching.MultiTierMcpCache>();

            return services;
        }

        /// <summary>
        /// Adds validation services
        /// </summary>
        public static IServiceCollection AddMcpValidation(
            this IServiceCollection services,
            IConfiguration configuration,
            ValidationOptions validationOptions)
        {
            if (validationOptions.EnableEnvironmentValidation)
            {
                services.AddSingleton(typeof(IValidateOptions<>), typeof(EnvironmentAwareConfigurationValidator<>));
            }

            if (validationOptions.EnableSchemaValidation)
            {
                // Schema registry is static, no need to register
            }

            return services;
        }

        /// <summary>
        /// Adds factory services for creating components
        /// </summary>
        public static IServiceCollection AddMcpFactories(this IServiceCollection services)
        {
            services.AddSingleton<RateLimiterFactory>();
            services.AddSingleton<CircuitBreakerFactory>();

            return services;
        }

        /// <summary>
        /// Adds performance monitoring services
        /// </summary>
        public static IServiceCollection AddMcpPerformance(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<PerformanceProfilerOptions>(
                configuration.GetSection("McpExtensions:Performance"));

            services.AddSingleton<PerformanceProfiler>();
            services.AddSingleton<MessageProcessor>();
            services.AddSingleton<OptimizedJsonProcessor>();
            services.AddSingleton<MessagePackProcessor>();

            return services;
        }

        /// <summary>
        /// Adds WebSocket services with queuing support
        /// </summary>
        public static IServiceCollection AddMcpWebSocket(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<QueuedWebSocketOptions>(
                configuration.GetSection("McpExtensions:WebSocket"));

            return services;
        }

        /// <summary>
        /// Adds chaos engineering services for testing
        /// </summary>
        public static IServiceCollection AddMcpChaos(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ChaosOptions>(
                configuration.GetSection("McpExtensions:Chaos"));

            return services;
        }

        /// <summary>
        /// Adds enhanced configuration tracking
        /// </summary>
        public static IServiceCollection AddMcpConfigurationTracking(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<ConfigurationChangeTrackerOptions>(
                configuration.GetSection("McpExtensions:ConfigurationTracking"));

            services.AddSingleton<ConfigurationChangeTracker>();

            return services;
        }

        /// <summary>
        /// Adds zero-trust security pipeline
        /// </summary>
        public static IServiceCollection AddMcpZeroTrustSecurity(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<ZeroTrustSecurityPipeline>();

            // Register default security validators
            services.AddScoped<ISecurityValidator, AuthenticationValidator>();
            services.AddScoped<ISecurityValidator, AuthorizationValidator>();
            services.AddScoped<ISecurityValidator, RateLimitValidator>();
            services.AddScoped<ISecurityValidator, InputValidationValidator>();

            return services;
        }
    }
}
