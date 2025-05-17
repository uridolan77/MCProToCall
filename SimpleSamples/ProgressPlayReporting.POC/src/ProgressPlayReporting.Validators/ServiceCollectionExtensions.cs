using Microsoft.Extensions.DependencyInjection;
using ProgressPlayReporting.Validators.Interfaces;
using ProgressPlayReporting.Validators.Sql;

namespace ProgressPlayReporting.Validators
{
    /// <summary>
    /// Extension methods for adding validation services to DI
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds validation services to the service collection
        /// </summary>
        public static IServiceCollection AddValidationServices(this IServiceCollection services)
        {
            // Register our validators
            services.AddTransient<IQueryValidator, SqlQueryValidator>();
            
            return services;
        }
    }
}
