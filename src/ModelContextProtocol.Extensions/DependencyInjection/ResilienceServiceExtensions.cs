using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Extensions.Resilience;
using Polly;
using Polly.Extensions.Http;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring resilience services
    /// </summary>
    public static class ResilienceServiceExtensions
    {
        /// <summary>
        /// Adds resilience services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddResilienceServices(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Configure resilience options
            services.Configure<ResilienceOptions>(configuration.GetSection("Resilience"));
            services.Configure<RateLimitOptions>(configuration.GetSection("RateLimit"));

            // Register rate limiters
            services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
            services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitOptions>>();
                var logger = sp.GetRequiredService<ILogger<SlidingWindowRateLimiter>>();
                return new SlidingWindowRateLimiter(options, logger);
            });

            // Register HTTP client factory
            services.AddSingleton<ResilientHttpClientFactory>();

            return services;
        }

        /// <summary>
        /// Adds a resilient HTTP client to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="name">The name of the client</param>
        /// <param name="configureClient">Action to configure the client</param>
        /// <returns>The service collection for chaining</returns>
        public static IHttpClientBuilder AddResilientHttpClient(
            this IServiceCollection services,
            string name,
            Action<IServiceProvider, HttpClient> configureClient = null)
        {
            return services.AddHttpClient(name, (sp, client) =>
            {
                // Apply default configuration
                client.Timeout = TimeSpan.FromSeconds(30);

                // Apply custom configuration if provided
                configureClient?.Invoke(sp, client);
            })
            .AddHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ResilienceOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<ResilientHttpClientFactory>>();

                // Create a delegating handler that applies the resilience policy
                var policy = ResilientHttpClientFactory.CreateResiliencePolicy(options, logger);
                return new PolicyHttpMessageHandler(policy);
            });
        }

        /// <summary>
        /// Adds a rate limited HTTP client to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="name">The name of the client</param>
        /// <param name="configureClient">Action to configure the client</param>
        /// <returns>The service collection for chaining</returns>
        public static IHttpClientBuilder AddRateLimitedHttpClient(
            this IServiceCollection services,
            string name,
            Action<IServiceProvider, HttpClient> configureClient = null)
        {
            return services.AddResilientHttpClient(name, configureClient)
                .AddHttpMessageHandler(sp =>
                {
                    var rateLimiter = sp.GetRequiredService<IRateLimiter>();
                    var logger = sp.GetRequiredService<ILogger<RateLimitingHandler>>();

                    return new RateLimitingHandler(rateLimiter, logger);
                });
        }
    }

    /// <summary>
    /// HTTP message handler that applies rate limiting
    /// </summary>
    public class RateLimitingHandler : DelegatingHandler
    {
        private readonly IRateLimiter _rateLimiter;
        private readonly ILogger<RateLimitingHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the RateLimitingHandler class
        /// </summary>
        /// <param name="rateLimiter">The rate limiter</param>
        /// <param name="logger">The logger</param>
        public RateLimitingHandler(IRateLimiter rateLimiter, ILogger<RateLimitingHandler> logger)
        {
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends an HTTP request
        /// </summary>
        /// <param name="request">The HTTP request message</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The HTTP response message</returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Acquire a permit from the rate limiter
            await _rateLimiter.AcquireAsync(cancellationToken);

            _logger.LogDebug("Rate limit permit acquired, sending request to {RequestUri}",
                request.RequestUri);

            // Send the request
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
