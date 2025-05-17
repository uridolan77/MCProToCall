using Microsoft.Extensions.DependencyInjection;
using ProgressPlayReporting.Core.Interfaces;
using LLMGateway.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// Extension methods for registering LLM integration services
    /// </summary>
    public static class ServiceCollectionExtensions
    {        /// <summary>
        /// Adds LLM integration services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>        /// <param name="defaultModelId">Default model ID to use</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddLlmIntegration(this IServiceCollection services, string defaultModelId = "gpt-4")
        {            // Register our CompletionService
            services.AddSingleton<ICompletionService, CompletionService>();
            
            // Register our ILlmGateway implementation
            services.AddScoped<ILlmGateway>(provider =>
            {
                var completionService = provider.GetRequiredService<ICompletionService>();
                var logger = provider.GetRequiredService<ILogger<LlmGatewayService>>();
                
                return new LlmGatewayService(completionService, logger, defaultModelId);
            });
            
            // Register SQL query generator
            services.AddScoped<ISqlQueryGenerator, SqlQueryGenerator>();
            
            // Register report generator
            services.AddScoped<IReportGenerator, ReportGenerator>();
            
            // Register prompt management service
            services.AddSingleton<PromptManagementService>();
            
            return services;
        }
    }
}
