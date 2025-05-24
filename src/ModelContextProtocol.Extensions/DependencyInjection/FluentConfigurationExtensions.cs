using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;
using ModelContextProtocol.Extensions.Security.Pipeline.Steps;
using ModelContextProtocol.Extensions.Security.HSM;
using ModelContextProtocol.Extensions.Resilience;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Fluent configuration extensions for MCP server
    /// </summary>
    public static class FluentConfigurationExtensions
    {
        /// <summary>
        /// Adds MCP server with fluent configuration
        /// </summary>
        public static IServiceCollection AddMcpServer(
            this IServiceCollection services,
            Action<IMcpServerBuilder> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new McpServerBuilder(services);
            configure(builder);

            return services;
        }

        /// <summary>
        /// Adds MCP client with fluent configuration
        /// </summary>
        public static IServiceCollection AddMcpClient(
            this IServiceCollection services,
            Action<IMcpClientBuilder> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new McpClientBuilder(services);
            configure(builder);

            return services;
        }
    }

    /// <summary>
    /// Interface for MCP server builder
    /// </summary>
    public interface IMcpServerBuilder
    {
        /// <summary>
        /// The service collection
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Configures TLS settings
        /// </summary>
        IMcpServerBuilder UseTls(Action<ITlsBuilder> configure);

        /// <summary>
        /// Configures authentication
        /// </summary>
        IMcpServerBuilder UseAuthentication(Action<IAuthenticationBuilder> configure);

        /// <summary>
        /// Configures rate limiting
        /// </summary>
        IMcpServerBuilder UseRateLimiting(Action<IRateLimitingBuilder> configure);

        /// <summary>
        /// Configures resilience patterns
        /// </summary>
        IMcpServerBuilder UseResilience(Action<IResilienceBuilder> configure);

        /// <summary>
        /// Configures protocol handlers
        /// </summary>
        IMcpServerBuilder UseProtocols(Action<IProtocolBuilder> configure);

        /// <summary>
        /// Adds health checks
        /// </summary>
        IMcpServerBuilder AddHealthChecks();

        /// <summary>
        /// Adds metrics collection
        /// </summary>
        IMcpServerBuilder AddMetrics();

        /// <summary>
        /// Adds distributed tracing
        /// </summary>
        IMcpServerBuilder AddTracing();
    }

    /// <summary>
    /// Interface for MCP client builder
    /// </summary>
    public interface IMcpClientBuilder
    {
        /// <summary>
        /// The service collection
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Configures TLS settings
        /// </summary>
        IMcpClientBuilder UseTls(Action<ITlsBuilder> configure);

        /// <summary>
        /// Configures resilience patterns
        /// </summary>
        IMcpClientBuilder UseResilience(Action<IResilienceBuilder> configure);

        /// <summary>
        /// Configures protocol handlers
        /// </summary>
        IMcpClientBuilder UseProtocols(Action<IProtocolBuilder> configure);
    }

    /// <summary>
    /// Interface for TLS configuration builder
    /// </summary>
    public interface ITlsBuilder
    {
        /// <summary>
        /// Configures certificate from file
        /// </summary>
        ITlsBuilder WithCertificate(string certificatePath, string password = null);

        /// <summary>
        /// Configures certificate from store
        /// </summary>
        ITlsBuilder WithCertificateFromStore(string thumbprint);

        /// <summary>
        /// Requires client certificates
        /// </summary>
        ITlsBuilder RequireClientCertificate();

        /// <summary>
        /// Enables certificate pinning
        /// </summary>
        ITlsBuilder EnableCertificatePinning(Action<ICertificatePinningBuilder> configure = null);

        /// <summary>
        /// Configures certificate validation pipeline
        /// </summary>
        ITlsBuilder UseValidationPipeline(Action<IValidationPipelineBuilder> configure);

        /// <summary>
        /// Configures Hardware Security Module
        /// </summary>
        ITlsBuilder UseHsm(Action<IHsmBuilder> configure);

        /// <summary>
        /// Sets minimum TLS version
        /// </summary>
        ITlsBuilder WithMinimumTlsVersion(string version);
    }

    /// <summary>
    /// Interface for authentication configuration builder
    /// </summary>
    public interface IAuthenticationBuilder
    {
        /// <summary>
        /// Adds JWT Bearer authentication
        /// </summary>
        IAuthenticationBuilder AddJwtBearer(Action<IJwtBearerBuilder> configure);

        /// <summary>
        /// Adds API key authentication
        /// </summary>
        IAuthenticationBuilder AddApiKey(Action<IApiKeyBuilder> configure);

        /// <summary>
        /// Adds certificate authentication
        /// </summary>
        IAuthenticationBuilder AddCertificate();
    }

    /// <summary>
    /// Interface for rate limiting configuration builder
    /// </summary>
    public interface IRateLimitingBuilder
    {
        /// <summary>
        /// Uses adaptive rate limiting policy
        /// </summary>
        IRateLimitingBuilder WithAdaptivePolicy();

        /// <summary>
        /// Sets maximum requests per minute
        /// </summary>
        IRateLimitingBuilder WithMaxRequestsPerMinute(int maxRequests);

        /// <summary>
        /// Sets maximum requests per hour
        /// </summary>
        IRateLimitingBuilder WithMaxRequestsPerHour(int maxRequests);

        /// <summary>
        /// Configures burst allowance
        /// </summary>
        IRateLimitingBuilder WithBurstAllowance(int burstSize);
    }

    /// <summary>
    /// Interface for resilience configuration builder
    /// </summary>
    public interface IResilienceBuilder
    {
        /// <summary>
        /// Configures bulkhead isolation
        /// </summary>
        IResilienceBuilder UseBulkhead(Action<IBulkheadBuilder> configure);

        /// <summary>
        /// Configures request hedging
        /// </summary>
        IResilienceBuilder UseHedging(Action<IHedgingBuilder> configure);

        /// <summary>
        /// Configures circuit breaker
        /// </summary>
        IResilienceBuilder UseCircuitBreaker(Action<ICircuitBreakerBuilder> configure);

        /// <summary>
        /// Configures retry policy
        /// </summary>
        IResilienceBuilder UseRetry(Action<IRetryBuilder> configure);
    }

    /// <summary>
    /// Interface for protocol configuration builder
    /// </summary>
    public interface IProtocolBuilder
    {
        /// <summary>
        /// Adds JSON-RPC protocol support
        /// </summary>
        IProtocolBuilder AddJsonRpc();

        /// <summary>
        /// Adds MessagePack protocol support
        /// </summary>
        IProtocolBuilder AddMessagePack();

        /// <summary>
        /// Adds gRPC protocol support
        /// </summary>
        IProtocolBuilder AddGrpc();

        /// <summary>
        /// Enables protocol negotiation
        /// </summary>
        IProtocolBuilder EnableNegotiation();

        /// <summary>
        /// Sets default protocol
        /// </summary>
        IProtocolBuilder WithDefaultProtocol(string protocolName);
    }

    /// <summary>
    /// Interface for certificate pinning configuration builder
    /// </summary>
    public interface ICertificatePinningBuilder
    {
        /// <summary>
        /// Pins specific certificates by thumbprint
        /// </summary>
        ICertificatePinningBuilder PinCertificates(params string[] thumbprints);

        /// <summary>
        /// Enables auto-pinning of first certificate
        /// </summary>
        ICertificatePinningBuilder AutoPinFirstCertificate();

        /// <summary>
        /// Allows self-signed certificates if pinned
        /// </summary>
        ICertificatePinningBuilder AllowSelfSignedIfPinned();
    }

    /// <summary>
    /// Interface for validation pipeline configuration builder
    /// </summary>
    public interface IValidationPipelineBuilder
    {
        /// <summary>
        /// Adds expiry validation step
        /// </summary>
        IValidationPipelineBuilder AddExpiryValidation();

        /// <summary>
        /// Adds key usage validation step
        /// </summary>
        IValidationPipelineBuilder AddKeyUsageValidation();

        /// <summary>
        /// Adds revocation validation step
        /// </summary>
        IValidationPipelineBuilder AddRevocationValidation();

        /// <summary>
        /// Adds certificate transparency validation step
        /// </summary>
        IValidationPipelineBuilder AddTransparencyValidation();

        /// <summary>
        /// Adds pinning validation step
        /// </summary>
        IValidationPipelineBuilder AddPinningValidation();

        /// <summary>
        /// Adds custom validation step
        /// </summary>
        IValidationPipelineBuilder AddCustomValidation<T>() where T : class, ICertificateValidationStep;
    }

    /// <summary>
    /// Interface for HSM configuration builder
    /// </summary>
    public interface IHsmBuilder
    {
        /// <summary>
        /// Uses Azure Key Vault as HSM provider
        /// </summary>
        IHsmBuilder UseAzureKeyVault(string vaultUrl);

        /// <summary>
        /// Uses PKCS#11 as HSM provider
        /// </summary>
        IHsmBuilder UsePkcs11(string libraryPath);

        /// <summary>
        /// Sets certificate identifier
        /// </summary>
        IHsmBuilder WithCertificate(string identifier);

        /// <summary>
        /// Sets signing key identifier
        /// </summary>
        IHsmBuilder WithSigningKey(string identifier);

        /// <summary>
        /// Sets encryption key identifier
        /// </summary>
        IHsmBuilder WithEncryptionKey(string identifier);
    }

    /// <summary>
    /// Interface for JWT Bearer configuration builder
    /// </summary>
    public interface IJwtBearerBuilder
    {
        /// <summary>
        /// Sets the secret key
        /// </summary>
        IJwtBearerBuilder WithSecret(string secret);

        /// <summary>
        /// Sets the issuer
        /// </summary>
        IJwtBearerBuilder WithIssuer(string issuer);

        /// <summary>
        /// Sets the audience
        /// </summary>
        IJwtBearerBuilder WithAudience(string audience);
    }

    /// <summary>
    /// Interface for API key configuration builder
    /// </summary>
    public interface IApiKeyBuilder
    {
        /// <summary>
        /// Sets the header name for API key
        /// </summary>
        IApiKeyBuilder WithHeaderName(string headerName);

        /// <summary>
        /// Sets valid API keys
        /// </summary>
        IApiKeyBuilder WithKeys(params string[] keys);
    }

    /// <summary>
    /// Interface for bulkhead configuration builder
    /// </summary>
    public interface IBulkheadBuilder
    {
        /// <summary>
        /// Sets maximum concurrent executions
        /// </summary>
        IBulkheadBuilder WithMaxConcurrentExecutions(int maxExecutions);

        /// <summary>
        /// Sets maximum queue size
        /// </summary>
        IBulkheadBuilder WithMaxQueueSize(int maxQueueSize);

        /// <summary>
        /// Sets queue timeout
        /// </summary>
        IBulkheadBuilder WithQueueTimeout(TimeSpan timeout);
    }

    /// <summary>
    /// Interface for hedging configuration builder
    /// </summary>
    public interface IHedgingBuilder
    {
        /// <summary>
        /// Sets hedging delay
        /// </summary>
        IHedgingBuilder WithDelay(TimeSpan delay);

        /// <summary>
        /// Sets maximum hedged requests
        /// </summary>
        IHedgingBuilder WithMaxHedgedRequests(int maxRequests);

        /// <summary>
        /// Sets operations that support hedging
        /// </summary>
        IHedgingBuilder ForOperations(params string[] operations);
    }

    /// <summary>
    /// Interface for circuit breaker configuration builder
    /// </summary>
    public interface ICircuitBreakerBuilder
    {
        /// <summary>
        /// Sets failure threshold
        /// </summary>
        ICircuitBreakerBuilder WithFailureThreshold(int threshold);

        /// <summary>
        /// Sets timeout duration
        /// </summary>
        ICircuitBreakerBuilder WithTimeout(TimeSpan timeout);
    }

    /// <summary>
    /// Interface for retry configuration builder
    /// </summary>
    public interface IRetryBuilder
    {
        /// <summary>
        /// Sets maximum retry attempts
        /// </summary>
        IRetryBuilder WithMaxAttempts(int maxAttempts);

        /// <summary>
        /// Uses exponential backoff
        /// </summary>
        IRetryBuilder WithExponentialBackoff(TimeSpan baseDelay);

        /// <summary>
        /// Uses linear backoff
        /// </summary>
        IRetryBuilder WithLinearBackoff(TimeSpan delay);
    }

    /// <summary>
    /// Implementation of MCP server builder
    /// </summary>
    internal class McpServerBuilder : IMcpServerBuilder
    {
        public IServiceCollection Services { get; }

        public McpServerBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            // Add core services
            Services.AddSingleton<ICertificateValidationPipeline, CertificateValidationPipeline>();
        }

        public IMcpServerBuilder UseTls(Action<ITlsBuilder> configure)
        {
            var builder = new TlsBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpServerBuilder UseAuthentication(Action<IAuthenticationBuilder> configure)
        {
            var builder = new AuthenticationBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpServerBuilder UseRateLimiting(Action<IRateLimitingBuilder> configure)
        {
            var builder = new RateLimitingBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpServerBuilder UseResilience(Action<IResilienceBuilder> configure)
        {
            var builder = new ResilienceBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpServerBuilder UseProtocols(Action<IProtocolBuilder> configure)
        {
            var builder = new ProtocolBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpServerBuilder AddHealthChecks()
        {
            Services.AddHealthChecks();
            return this;
        }

        public IMcpServerBuilder AddMetrics()
        {
            // Add metrics services
            return this;
        }

        public IMcpServerBuilder AddTracing()
        {
            // Add tracing services
            return this;
        }
    }

    /// <summary>
    /// Implementation of MCP client builder
    /// </summary>
    internal class McpClientBuilder : IMcpClientBuilder
    {
        public IServiceCollection Services { get; }

        public McpClientBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            // Add core client services - TODO: Add when Core classes are available
        }

        public IMcpClientBuilder UseTls(Action<ITlsBuilder> configure)
        {
            var builder = new TlsBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpClientBuilder UseResilience(Action<IResilienceBuilder> configure)
        {
            var builder = new ResilienceBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }

        public IMcpClientBuilder UseProtocols(Action<IProtocolBuilder> configure)
        {
            var builder = new ProtocolBuilder(Services);
            configure?.Invoke(builder);
            return this;
        }
    }
}
