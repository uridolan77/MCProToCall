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

        /// <summary>
        /// Adds advanced caching features including cache warming and analytics
        /// </summary>
        public static IServiceCollection AddMcpAdvancedCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add cache warming service
            services.AddSingleton<ModelContextProtocol.Extensions.Caching.ICacheWarmingService,
                ModelContextProtocol.Extensions.Caching.CacheWarmingService>();

            // Add cache analytics
            services.AddSingleton<ModelContextProtocol.Extensions.Caching.ICacheAnalyticsService,
                ModelContextProtocol.Extensions.Caching.CacheAnalyticsService>();

            // Add predictive cache warming strategies
            services.AddTransient<ModelContextProtocol.Extensions.Caching.IPredictiveCacheWarmingStrategy,
                ModelContextProtocol.Extensions.Caching.PredictiveCacheWarmingStrategy>();

            return services;
        }

        /// <summary>
        /// Adds advanced protocol management and message routing
        /// </summary>
        public static IServiceCollection AddMcpAdvancedProtocol(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add protocol version management
            services.AddSingleton<ModelContextProtocol.Extensions.Protocol.IProtocolVersionManager,
                ModelContextProtocol.Extensions.Protocol.ProtocolVersionManager>();

            // Add adaptive protocol handler
            services.AddSingleton<ModelContextProtocol.Extensions.Protocol.IAdaptiveProtocolHandler,
                ModelContextProtocol.Extensions.Protocol.AdaptiveProtocolHandler>();

            // Add message routing
            services.AddSingleton<ModelContextProtocol.Extensions.Protocol.IMessageRouter,
                ModelContextProtocol.Extensions.Protocol.MessageRouter>();

            // Add message transformation pipeline
            services.AddSingleton<ModelContextProtocol.Extensions.Protocol.IMessageTransformationPipeline,
                ModelContextProtocol.Extensions.Protocol.MessageTransformationPipeline>();

            return services;
        }

        /// <summary>
        /// Adds intelligent resource management with adaptive pooling
        /// </summary>
        public static IServiceCollection AddMcpIntelligentResourceManagement(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add adaptive resource pools
            services.AddSingleton(typeof(ModelContextProtocol.Extensions.Performance.IAdaptiveResourcePool<>),
                typeof(ModelContextProtocol.Extensions.Performance.AdaptiveResourcePool<>));

            // Add resource quota manager
            services.AddSingleton<ModelContextProtocol.Extensions.Performance.IResourceQuotaManager,
                ModelContextProtocol.Extensions.Performance.ResourceQuotaManager>();

            // Add predictive models for resource scaling
            services.AddTransient<ModelContextProtocol.Extensions.Performance.IPredictiveModel,
                ModelContextProtocol.Extensions.Performance.ResourceDemandPredictionModel>();

            return services;
        }

        /// <summary>
        /// Adds stream processing and real-time data capabilities
        /// </summary>
        public static IServiceCollection AddMcpStreamProcessing(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add stream processor
            services.AddSingleton<ModelContextProtocol.Extensions.Streaming.IStreamProcessor,
                ModelContextProtocol.Extensions.Streaming.StreamProcessor>();

            // Add real-time data aggregator
            services.AddSingleton<ModelContextProtocol.Extensions.Streaming.IRealTimeDataAggregator,
                ModelContextProtocol.Extensions.Streaming.RealTimeDataAggregator>();

            // Add event store
            services.AddSingleton<ModelContextProtocol.Extensions.Streaming.IEventStore,
                ModelContextProtocol.Extensions.Streaming.EventStore>();

            return services;
        }

        /// <summary>
        /// Adds feature flag management and gradual rollouts
        /// </summary>
        public static IServiceCollection AddMcpFeatureManagement(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add feature flag service
            services.AddSingleton<ModelContextProtocol.Extensions.Features.IFeatureFlagService,
                ModelContextProtocol.Extensions.Features.FeatureFlagService>();

            // Add gradual rollout manager
            services.AddSingleton<ModelContextProtocol.Extensions.Features.IGradualRolloutManager,
                ModelContextProtocol.Extensions.Features.GradualRolloutManager>();

            // Add default feature flag providers
            services.AddTransient<ModelContextProtocol.Extensions.Features.IFeatureFlagProvider,
                ModelContextProtocol.Extensions.Features.ConfigurationFeatureFlagProvider>();

            return services;
        }

        /// <summary>
        /// Adds advanced observability with anomaly detection
        /// </summary>
        public static IServiceCollection AddMcpAdvancedObservability(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add anomaly detection service
            services.AddSingleton<ModelContextProtocol.Extensions.Observability.IAnomalyDetectionService,
                ModelContextProtocol.Extensions.Observability.AnomalyDetectionService>();

            // Add business metrics correlator
            services.AddSingleton<ModelContextProtocol.Extensions.Observability.IBusinessMetricsCorrelator,
                ModelContextProtocol.Extensions.Observability.BusinessMetricsCorrelator>();

            // Add anomaly detectors
            services.AddTransient<ModelContextProtocol.Extensions.Observability.IAnomalyDetector,
                ModelContextProtocol.Extensions.Observability.StatisticalAnomalyDetector>();
            services.AddTransient<ModelContextProtocol.Extensions.Observability.IAnomalyDetector,
                ModelContextProtocol.Extensions.Observability.MachineLearningAnomalyDetector>();

            return services;
        }

        /// <summary>
        /// Adds all advanced MCP extensions
        /// </summary>
        public static IServiceCollection AddMcpAdvancedExtensions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            return services
                .AddMcpAdvancedCaching(configuration)
                .AddMcpAdvancedProtocol(configuration)
                .AddMcpIntelligentResourceManagement(configuration)
                .AddMcpStreamProcessing(configuration)
                .AddMcpFeatureManagement(configuration)
                .AddMcpAdvancedObservability(configuration);
        }
    }
}
