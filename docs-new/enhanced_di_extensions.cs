// Comprehensive service registration extensions
public static class McpExtensionsServiceCollectionExtensions
{
    public static IServiceCollection AddMcpExtensions(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<McpExtensionsOptions> configureOptions = null)
    {
        var options = new McpExtensionsOptions();
        configuration.GetSection("McpExtensions").Bind(options);
        configureOptions?.Invoke(options);
        
        return services
            .AddMcpSecurity(configuration, options.Security)
            .AddMcpResilience(configuration, options.Resilience)
            .AddMcpObservability(configuration, options.Observability)
            .AddMcpConfiguration(configuration, options.Configuration)
            .AddMcpValidation(configuration, options.Validation);
    }
    
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
                    provider.GetRequiredService<IOptionsMonitor<HsmOptions>>()));
                    
            services.AddScoped<IHardwareSecurityModule>(provider =>
            {
                var factory = provider.GetRequiredService<IHardwareSecurityModuleFactory>();
                var options = provider.GetRequiredService<IOptionsMonitor<HsmOptions>>().CurrentValue;
                return factory.Create(options.ProviderType);
            });
        }
        
        return services;
    }
    
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
                    "Adaptive" => provider.GetRequiredService<AdaptiveRateLimiter>(),
                    _ => throw new InvalidOperationException($"Unknown rate limiter type: {resilienceOptions.RateLimitingType}")
                });
        }
        
        // Circuit breaker with health checks integration
        if (resilienceOptions.EnableCircuitBreaker)
        {
            services.AddSingleton<KeyVaultCircuitBreaker>();
            services.AddHealthChecks()
                .AddCheck<CircuitBreakerHealthCheck>("circuit_breaker");
        }
        
        return services;
    }
}

// Options classes with validation
public class McpExtensionsOptions : IValidatableObject
{
    public SecurityOptions Security { get; set; } = new();
    public ResilienceOptions Resilience { get; set; } = new();
    public ObservabilityOptions Observability { get; set; } = new();
    public ConfigurationOptions Configuration { get; set; } = new();
    public ValidationOptions Validation { get; set; } = new();
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();
        
        // Cross-cutting validation logic
        if (Security.EnableHsm && string.IsNullOrEmpty(Security.HsmConnectionString))
        {
            results.Add(new ValidationResult(
                "HSM connection string is required when HSM is enabled",
                new[] { nameof(Security.HsmConnectionString) }));
        }
        
        return results;
    }
}

// Factory pattern for pluggable components
public interface IHardwareSecurityModuleFactory
{
    IHardwareSecurityModule Create(string providerType);
}

public class HardwareSecurityModuleFactory : IHardwareSecurityModuleFactory
{
    private readonly ILogger<HardwareSecurityModuleFactory> _logger;
    private readonly IOptionsMonitor<HsmOptions> _options;
    
    public HardwareSecurityModuleFactory(
        ILogger<HardwareSecurityModuleFactory> logger,
        IOptionsMonitor<HsmOptions> options)
    {
        _logger = logger;
        _options = options;
    }
    
    public IHardwareSecurityModule Create(string providerType)
    {
        return providerType switch
        {
            "AzureKeyVault" => new AzureKeyVaultHsm(_logger, _options),
            "PKCS11" => new Pkcs11Hsm(_logger, _options),
            "LocalCertStore" => new LocalCertStoreHsm(_logger, _options),
            _ => throw new NotSupportedException($"HSM provider '{providerType}' is not supported")
        };
    }
}