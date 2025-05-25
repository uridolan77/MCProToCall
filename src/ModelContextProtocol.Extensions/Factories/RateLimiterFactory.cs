using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Resilience;

namespace ModelContextProtocol.Extensions.Factories
{
    /// <summary>
    /// Factory for creating rate limiters with different strategies
    /// </summary>
    public class RateLimiterFactory : McpComponentFactoryBase<IRateLimiter>
    {
        public RateLimiterFactory(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override void RegisterCreators()
        {
            _creators["TokenBucket"] = CreateTokenBucketRateLimiter;
            _creators["SlidingWindow"] = CreateSlidingWindowRateLimiter;
            _creators["Adaptive"] = CreateAdaptiveRateLimiter;
            _creators["Fixed"] = CreateFixedWindowRateLimiter;
        }

        private IRateLimiter CreateTokenBucketRateLimiter(IConfiguration config)
        {
            var options = new RateLimitOptions();
            config.Bind(options);

            var optionsWrapper = Options.Create(options);
            var logger = _serviceProvider.GetRequiredService<ILogger<TokenBucketRateLimiter>>();

            return new TokenBucketRateLimiter(optionsWrapper, logger);
        }

        private IRateLimiter CreateSlidingWindowRateLimiter(IConfiguration config)
        {
            var options = new RateLimitOptions();
            config.Bind(options);

            var optionsWrapper = Options.Create(options);
            var logger = _serviceProvider.GetRequiredService<ILogger<SlidingWindowRateLimiter>>();

            return new SlidingWindowRateLimiter(optionsWrapper, logger);
        }

        private IRateLimiter CreateAdaptiveRateLimiter(IConfiguration config)
        {
            var options = new AdaptiveRateLimitOptions();
            config.Bind(options);

            var optionsWrapper = Options.Create(options);
            var logger = _serviceProvider.GetRequiredService<ILogger<AdaptiveRateLimiter>>();

            return new AdaptiveRateLimiter(logger, optionsWrapper);
        }

        private IRateLimiter CreateFixedWindowRateLimiter(IConfiguration config)
        {
            // For now, use TokenBucket as a fallback for FixedWindow
            // This can be replaced with a proper FixedWindowRateLimiter implementation
            return CreateTokenBucketRateLimiter(config);
        }
    }

    /// <summary>
    /// Factory for creating circuit breakers
    /// </summary>
    public class CircuitBreakerFactory : McpComponentFactoryBase<object>
    {
        public CircuitBreakerFactory(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        protected override void RegisterCreators()
        {
            _creators["KeyVault"] = CreateKeyVaultCircuitBreaker;
            _creators["Smart"] = CreateSmartCircuitBreaker;
            _creators["Basic"] = CreateBasicCircuitBreaker;
        }

        private object CreateKeyVaultCircuitBreaker(IConfiguration config)
        {
            var options = new KeyVaultCircuitBreakerOptions();
            config.Bind(options);

            var optionsWrapper = Options.Create(options);
            var logger = _serviceProvider.GetRequiredService<ILogger<KeyVaultCircuitBreaker>>();
            return new KeyVaultCircuitBreaker(logger, optionsWrapper);
        }

        private object CreateSmartCircuitBreaker(IConfiguration config)
        {
            // This would create a SmartCircuitBreaker when implemented
            return CreateKeyVaultCircuitBreaker(config);
        }

        private object CreateBasicCircuitBreaker(IConfiguration config)
        {
            return CreateKeyVaultCircuitBreaker(config);
        }
    }
}
