using LLMGateway.Core.Options;
using LLMGateway.Core.Models.ContentFiltering;
using LLMGateway.Core.Models.Provider;
using LLMGateway.Providers.Anthropic;
using LLMGateway.Providers.Cohere;
using LLMGateway.Providers.HuggingFace;
using LLMGateway.Providers.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using LLMGateway.Core.Interfaces;
using LLMGateway.Infrastructure.Auth;

namespace LLMGateway.API.Extensions;

/// <summary>
/// Extensions for service collection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add LLM Gateway options
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddLLMGatewayOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlobalOptions>(configuration.GetSection("GlobalOptions"));
        services.Configure<LLMRoutingOptions>(configuration.GetSection("LLMRouting"));
        services.Configure<RoutingOptions>(configuration.GetSection("Routing"));
        services.Configure<UserPreferencesOptions>(configuration.GetSection("UserPreferences"));
        services.Configure<FallbackOptions>(configuration.GetSection("Fallbacks"));
        services.Configure<LoggingOptions>(configuration.GetSection("Logging"));
        services.Configure<TelemetryOptions>(configuration.GetSection("Telemetry"));
        services.Configure<ApiKeyOptions>(configuration.GetSection("ApiKeys"));
        services.Configure<TokenUsageOptions>(configuration.GetSection("TokenUsage"));
        services.Configure<RateLimitOptions>(configuration.GetSection("RateLimiting"));
        services.Configure<PersistenceOptions>(configuration.GetSection("Persistence"));
        services.Configure<MonitoringOptions>(configuration.GetSection("Monitoring"));
        services.Configure<BackgroundJobOptions>(configuration.GetSection("BackgroundJobs"));
        services.Configure<RetryPolicyOptions>(configuration.GetSection("RetryPolicy"));
        services.Configure<ContentFilteringOptions>(configuration.GetSection("ContentFiltering"));

        return services;
    }

    /// <summary>
    /// Add JWT authentication
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = new JwtOptions();
        configuration.GetSection("Jwt").Bind(jwtOptions);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
            };
        });

        return services;
    }

    /// <summary>
    /// Add authorization policies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("CompletionAccess", policy => policy.RequireClaim("llm-permissions", "completion"));
            options.AddPolicy("EmbeddingAccess", policy => policy.RequireClaim("llm-permissions", "embedding"));
            options.AddPolicy("AdminAccess", policy => policy.RequireClaim("llm-permissions", "admin"));
        });

        return services;
    }

    /// <summary>
    /// Add rate limiting
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitOptions = new RateLimitOptions();
        configuration.GetSection("RateLimiting").Bind(rateLimitOptions);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault() ?? string.Empty;

                return RateLimitPartition.GetTokenBucketLimiter(apiKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = rateLimitOptions.TokenLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitOptions.QueueLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitOptions.ReplenishmentPeriodSeconds),
                    TokensPerPeriod = rateLimitOptions.TokensPerPeriod,
                    AutoReplenishment = true
                });
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests. Please try again later.",
                    retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? (double)retryAfter.TotalSeconds : 0
                }, token);
            };
        });

        return services;
    }

    /// <summary>
    /// Add LLM providers
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddLLMProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Register all provider configurations
        services.Configure<Core.Models.Provider.OpenAIOptions>(configuration.GetSection("Providers:OpenAI"));
        services.Configure<Providers.Anthropic.AnthropicOptions>(configuration.GetSection("Providers:Anthropic"));
        services.Configure<Providers.Cohere.CohereOptions>(configuration.GetSection("Providers:Cohere"));
        services.Configure<Providers.HuggingFace.HuggingFaceOptions>(configuration.GetSection("Providers:HuggingFace"));
        services.Configure<Providers.AzureOpenAI.AzureOpenAIOptions>(configuration.GetSection("Providers:AzureOpenAI"));
        services.Configure<Core.Models.VectorDB.VectorDBOptions>(configuration.GetSection("VectorDB"));
        services.Configure<Core.Models.Cost.CostManagementOptions>(configuration.GetSection("CostManagement"));

        // Register provider services
        services.AddHttpClient<OpenAIProvider>()
            .AddPolicyHandler(PoliciesToProviders.GetRetryPolicy())
            .AddPolicyHandler(PoliciesToProviders.GetCircuitBreakerPolicy());

        services.AddHttpClient<AnthropicProvider>()
            .AddPolicyHandler(PoliciesToProviders.GetRetryPolicy())
            .AddPolicyHandler(PoliciesToProviders.GetCircuitBreakerPolicy());

        services.AddHttpClient<CohereProvider>()
            .AddPolicyHandler(PoliciesToProviders.GetRetryPolicy())
            .AddPolicyHandler(PoliciesToProviders.GetCircuitBreakerPolicy());

        services.AddHttpClient<HuggingFaceProvider>()
            .AddPolicyHandler(PoliciesToProviders.GetRetryPolicy())
            .AddPolicyHandler(PoliciesToProviders.GetCircuitBreakerPolicy());

        services.AddHttpClient<Providers.AzureOpenAI.AzureOpenAIProvider>()
            .AddPolicyHandler(PoliciesToProviders.GetRetryPolicy())
            .AddPolicyHandler(PoliciesToProviders.GetCircuitBreakerPolicy());

        return services;
    }

    /// <summary>
    /// Add authentication services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        // Register JWT options for dependency injection
        services.AddOptions<JwtOptions>()
            .BindConfiguration("Jwt")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register authentication services
        services.AddScoped<Core.Interfaces.ITokenService, Infrastructure.Auth.TokenService>();
        services.AddScoped<Core.Interfaces.IUserService, Infrastructure.Auth.UserService>();
        services.AddScoped<Core.Interfaces.IAuthService, Infrastructure.Auth.AuthService>();

        return services;
    }

    /// <summary>
    /// Add core services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Register core services
        services.AddSingleton<Core.Interfaces.IRetryPolicyService, Core.Services.RetryPolicyService>();
        services.AddSingleton<Core.Interfaces.ITokenCountingService, Core.Services.TokenCountingService>();
        services.AddSingleton<Core.Interfaces.ITokenCounterService, Core.Services.TokenCounterService>();
        services.AddSingleton<Core.Interfaces.IContentFilteringService, Core.Services.ContentFilteringService>();
        services.AddScoped<Core.Interfaces.IProviderService, Core.Services.ProviderService>();

        // Register prompt management services
        services.AddScoped<Core.Interfaces.IPromptTemplateRepository, Infrastructure.Repositories.PromptTemplateRepository>();
        services.AddScoped<Core.Interfaces.IPromptTemplateService, Core.Services.PromptTemplateService>();

        // Register conversation services
        services.AddScoped<Core.Interfaces.IConversationRepository, Infrastructure.Repositories.ConversationRepository>();
        services.AddScoped<Core.Interfaces.IConversationService, Core.Services.ConversationService>();

        // Register vector database services
        services.AddSingleton<Core.Interfaces.IVectorDBService, Core.Services.InMemoryVectorDBService>();

        // Register multi-modal services
        services.AddScoped<Core.Interfaces.IMultiModalService, Core.Services.MultiModalService>();

        // Register A/B testing services
        services.AddScoped<Core.Interfaces.IABTestingRepository, Infrastructure.Repositories.ABTestingRepository>();
        services.AddScoped<Core.Interfaces.IABTestingService, Core.Services.ABTestingService>();

        // Register fine-tuning services
        services.AddScoped<Core.Interfaces.IFineTuningRepository, Infrastructure.Repositories.FineTuningRepository>();
        services.AddScoped<Core.Interfaces.IFineTuningService, Core.Services.FineTuningService>();

        // Register cost management services
        services.AddScoped<Core.Interfaces.ICostRepository, Infrastructure.Repositories.CostRepository>();
        services.AddScoped<Core.Interfaces.ICostManagementService, Core.Services.CostManagementService>();

        return services;
    }
}
