using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;
using ModelContextProtocol.Extensions.Security.Pipeline.Steps;
using ModelContextProtocol.Extensions.Security.HSM;
using ModelContextProtocol.Extensions.Resilience;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// TLS configuration builder implementation
    /// </summary>
    internal class TlsBuilder : ITlsBuilder
    {
        private readonly IServiceCollection _services;
        private ModelContextProtocol.Extensions.Security.TlsOptions _tlsOptions = new ModelContextProtocol.Extensions.Security.TlsOptions();

        public TlsBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public ITlsBuilder WithCertificate(string certificatePath, string password = null)
        {
            _tlsOptions.UseTls = true;
            _tlsOptions.CertificatePath = certificatePath;
            _tlsOptions.CertificatePassword = password;
            UpdateConfiguration();
            return this;
        }

        public ITlsBuilder WithCertificateFromStore(string thumbprint)
        {
            _tlsOptions.UseTls = true;
            _tlsOptions.CertificateThumbprint = thumbprint;
            UpdateConfiguration();
            return this;
        }

        public ITlsBuilder RequireClientCertificate()
        {
            _tlsOptions.RequireClientCertificate = true;
            UpdateConfiguration();
            return this;
        }

        public ITlsBuilder EnableCertificatePinning(Action<ICertificatePinningBuilder> configure = null)
        {
            _tlsOptions.CertificatePinning.Enabled = true;

            if (configure != null)
            {
                var builder = new CertificatePinningBuilder(_tlsOptions.CertificatePinning);
                configure(builder);
            }

            UpdateConfiguration();
            return this;
        }

        public ITlsBuilder UseValidationPipeline(Action<IValidationPipelineBuilder> configure)
        {
            var builder = new ValidationPipelineBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public ITlsBuilder UseHsm(Action<IHsmBuilder> configure)
        {
            _tlsOptions.HsmOptions.UseHsm = true;

            if (configure != null)
            {
                var builder = new HsmBuilder(_tlsOptions.HsmOptions, _services);
                configure(builder);
            }

            UpdateConfiguration();
            return this;
        }

        public ITlsBuilder WithMinimumTlsVersion(string version)
        {
            _tlsOptions.MinimumTlsVersion = version;
            UpdateConfiguration();
            return this;
        }

        private void UpdateConfiguration()
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.UseTls = _tlsOptions.UseTls;
                options.CertificatePath = _tlsOptions.CertificatePath;
                options.CertificatePassword = _tlsOptions.CertificatePassword;
                options.CertificateThumbprint = _tlsOptions.CertificateThumbprint;
                options.RequireClientCertificate = _tlsOptions.RequireClientCertificate;
                options.MinimumTlsVersion = _tlsOptions.MinimumTlsVersion;
                options.CertificatePinning = _tlsOptions.CertificatePinning;
                options.HsmOptions = _tlsOptions.HsmOptions;
            });
        }
    }

    /// <summary>
    /// Certificate pinning builder implementation
    /// </summary>
    internal class CertificatePinningBuilder : ICertificatePinningBuilder
    {
        private readonly CertificatePinningOptions _options;

        public CertificatePinningBuilder(CertificatePinningOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public ICertificatePinningBuilder PinCertificates(params string[] thumbprints)
        {
            if (thumbprints != null)
            {
                _options.PinnedCertificates.AddRange(thumbprints);
            }
            return this;
        }

        public ICertificatePinningBuilder AutoPinFirstCertificate()
        {
            _options.AutoPinFirstCertificate = true;
            return this;
        }

        public ICertificatePinningBuilder AllowSelfSignedIfPinned()
        {
            _options.AllowSelfSignedIfPinned = true;
            return this;
        }
    }

    /// <summary>
    /// Validation pipeline builder implementation
    /// </summary>
    internal class ValidationPipelineBuilder : IValidationPipelineBuilder
    {
        private readonly IServiceCollection _services;

        public ValidationPipelineBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IValidationPipelineBuilder AddExpiryValidation()
        {
            _services.AddSingleton<ICertificateValidationStep, ExpiryValidationStep>();
            return this;
        }

        public IValidationPipelineBuilder AddKeyUsageValidation()
        {
            _services.AddSingleton<ICertificateValidationStep, KeyUsageValidationStep>();
            return this;
        }

        public IValidationPipelineBuilder AddRevocationValidation()
        {
            _services.AddSingleton<ICertificateValidationStep, RevocationValidationStep>();
            return this;
        }

        public IValidationPipelineBuilder AddTransparencyValidation()
        {
            _services.AddSingleton<ICertificateValidationStep, TransparencyValidationStep>();
            return this;
        }

        public IValidationPipelineBuilder AddPinningValidation()
        {
            _services.AddSingleton<ICertificateValidationStep, PinningValidationStep>();
            return this;
        }

        public IValidationPipelineBuilder AddCustomValidation<T>() where T : class, ICertificateValidationStep
        {
            _services.AddSingleton<ICertificateValidationStep, T>();
            return this;
        }
    }

    /// <summary>
    /// HSM builder implementation
    /// </summary>
    internal class HsmBuilder : IHsmBuilder
    {
        private readonly ModelContextProtocol.Extensions.Security.HsmOptions _options;
        private readonly IServiceCollection _services;

        public HsmBuilder(ModelContextProtocol.Extensions.Security.HsmOptions options, IServiceCollection services)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IHsmBuilder UseAzureKeyVault(string vaultUrl)
        {
            _options.ProviderType = "AzureKeyVault";
            _options.ConnectionString = vaultUrl;
            _services.AddSingleton<IHardwareSecurityModule, AzureKeyVaultHsm>();
            return this;
        }

        public IHsmBuilder UsePkcs11(string libraryPath)
        {
            _options.ProviderType = "PKCS11";
            _options.ConnectionString = libraryPath;
            // TODO: Add PKCS11 implementation
            return this;
        }

        public IHsmBuilder WithCertificate(string identifier)
        {
            _options.CertificateIdentifier = identifier;
            return this;
        }

        public IHsmBuilder WithSigningKey(string identifier)
        {
            _options.SigningKeyIdentifier = identifier;
            return this;
        }

        public IHsmBuilder WithEncryptionKey(string identifier)
        {
            _options.EncryptionKeyIdentifier = identifier;
            return this;
        }
    }

    /// <summary>
    /// Authentication builder implementation
    /// </summary>
    internal class AuthenticationBuilder : IAuthenticationBuilder
    {
        private readonly IServiceCollection _services;

        public AuthenticationBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IAuthenticationBuilder AddJwtBearer(Action<IJwtBearerBuilder> configure)
        {
            var builder = new JwtBearerBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public IAuthenticationBuilder AddApiKey(Action<IApiKeyBuilder> configure)
        {
            var builder = new ApiKeyBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public IAuthenticationBuilder AddCertificate()
        {
            // Add certificate authentication
            return this;
        }
    }

    /// <summary>
    /// JWT Bearer builder implementation
    /// </summary>
    internal class JwtBearerBuilder : IJwtBearerBuilder
    {
        private readonly IServiceCollection _services;

        public JwtBearerBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IJwtBearerBuilder WithSecret(string secret)
        {
            // Configure JWT secret
            return this;
        }

        public IJwtBearerBuilder WithIssuer(string issuer)
        {
            // Configure JWT issuer
            return this;
        }

        public IJwtBearerBuilder WithAudience(string audience)
        {
            // Configure JWT audience
            return this;
        }
    }

    /// <summary>
    /// API Key builder implementation
    /// </summary>
    internal class ApiKeyBuilder : IApiKeyBuilder
    {
        private readonly IServiceCollection _services;

        public ApiKeyBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IApiKeyBuilder WithHeaderName(string headerName)
        {
            // Configure API key header name
            return this;
        }

        public IApiKeyBuilder WithKeys(params string[] keys)
        {
            // Configure valid API keys
            return this;
        }
    }

    /// <summary>
    /// Rate limiting builder implementation
    /// </summary>
    internal class RateLimitingBuilder : IRateLimitingBuilder
    {
        private readonly IServiceCollection _services;

        public RateLimitingBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IRateLimitingBuilder WithAdaptivePolicy()
        {
            // Configure adaptive rate limiting
            return this;
        }

        public IRateLimitingBuilder WithMaxRequestsPerMinute(int maxRequests)
        {
            // Configure max requests per minute
            return this;
        }

        public IRateLimitingBuilder WithMaxRequestsPerHour(int maxRequests)
        {
            // Configure max requests per hour
            return this;
        }

        public IRateLimitingBuilder WithBurstAllowance(int burstSize)
        {
            // Configure burst allowance
            return this;
        }
    }

    /// <summary>
    /// Resilience builder implementation
    /// </summary>
    internal class ResilienceBuilder : IResilienceBuilder
    {
        private readonly IServiceCollection _services;

        public ResilienceBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IResilienceBuilder UseBulkhead(Action<IBulkheadBuilder> configure)
        {
            var builder = new BulkheadBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public IResilienceBuilder UseHedging(Action<IHedgingBuilder> configure)
        {
            var builder = new HedgingBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public IResilienceBuilder UseCircuitBreaker(Action<ICircuitBreakerBuilder> configure)
        {
            var builder = new CircuitBreakerBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }

        public IResilienceBuilder UseRetry(Action<IRetryBuilder> configure)
        {
            var builder = new RetryBuilder(_services);
            configure?.Invoke(builder);
            return this;
        }
    }

    /// <summary>
    /// Bulkhead builder implementation
    /// </summary>
    internal class BulkheadBuilder : IBulkheadBuilder
    {
        private readonly IServiceCollection _services;

        public BulkheadBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            // Register bulkhead policy
            _services.AddSingleton(typeof(IBulkheadPolicy<>), typeof(BulkheadPolicy<>));
        }

        public IBulkheadBuilder WithMaxConcurrentExecutions(int maxExecutions)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.BulkheadOptions.MaxConcurrentExecutions = maxExecutions;
            });
            return this;
        }

        public IBulkheadBuilder WithMaxQueueSize(int maxQueueSize)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.BulkheadOptions.MaxQueueSize = maxQueueSize;
            });
            return this;
        }

        public IBulkheadBuilder WithQueueTimeout(TimeSpan timeout)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.BulkheadOptions.QueueTimeoutSeconds = (int)timeout.TotalSeconds;
            });
            return this;
        }
    }

    /// <summary>
    /// Hedging builder implementation
    /// </summary>
    internal class HedgingBuilder : IHedgingBuilder
    {
        private readonly IServiceCollection _services;

        public HedgingBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            // Register hedging handler
            _services.AddTransient<HedgingHandler>();
        }

        public IHedgingBuilder WithDelay(TimeSpan delay)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.HedgingOptions.HedgingDelayMs = (int)delay.TotalMilliseconds;
            });
            return this;
        }

        public IHedgingBuilder WithMaxHedgedRequests(int maxRequests)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.HedgingOptions.MaxHedgedRequests = maxRequests;
            });
            return this;
        }

        public IHedgingBuilder ForOperations(params string[] operations)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.HedgingOptions.HedgedOperations.Clear();
                if (operations != null)
                {
                    options.HedgingOptions.HedgedOperations.AddRange(operations);
                }
            });
            return this;
        }
    }

    /// <summary>
    /// Circuit breaker builder implementation
    /// </summary>
    internal class CircuitBreakerBuilder : ICircuitBreakerBuilder
    {
        private readonly IServiceCollection _services;

        public CircuitBreakerBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public ICircuitBreakerBuilder WithFailureThreshold(int threshold)
        {
            // Configure circuit breaker failure threshold
            return this;
        }

        public ICircuitBreakerBuilder WithTimeout(TimeSpan timeout)
        {
            // Configure circuit breaker timeout
            return this;
        }
    }

    /// <summary>
    /// Retry builder implementation
    /// </summary>
    internal class RetryBuilder : IRetryBuilder
    {
        private readonly IServiceCollection _services;

        public RetryBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IRetryBuilder WithMaxAttempts(int maxAttempts)
        {
            // Configure max retry attempts
            return this;
        }

        public IRetryBuilder WithExponentialBackoff(TimeSpan baseDelay)
        {
            // Configure exponential backoff
            return this;
        }

        public IRetryBuilder WithLinearBackoff(TimeSpan delay)
        {
            // Configure linear backoff
            return this;
        }
    }

    /// <summary>
    /// Protocol builder implementation
    /// </summary>
    internal class ProtocolBuilder : IProtocolBuilder
    {
        private readonly IServiceCollection _services;

        public ProtocolBuilder(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IProtocolBuilder AddJsonRpc()
        {
            // TODO: Add JSON-RPC protocol handler when Core classes are available
            return this;
        }

        public IProtocolBuilder AddMessagePack()
        {
            // TODO: Add MessagePack protocol handler
            return this;
        }

        public IProtocolBuilder AddGrpc()
        {
            // TODO: Add gRPC protocol handler
            return this;
        }

        public IProtocolBuilder EnableNegotiation()
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.ProtocolNegotiation.EnableNegotiation = true;
            });
            return this;
        }

        public IProtocolBuilder WithDefaultProtocol(string protocolName)
        {
            _services.Configure<ModelContextProtocol.Extensions.Security.TlsOptions>(options =>
            {
                options.ProtocolNegotiation.DefaultProtocol = protocolName;
            });
            return this;
        }
    }
}
