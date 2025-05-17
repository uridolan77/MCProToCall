using Microsoft.Extensions.DependencyInjection;
using ProgressPlayReporting.Core.Interfaces;
using System;

namespace ProgressPlayReporting.SchemaExtractor
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSchemaExtraction(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Register the SQL Server schema extractor
            services.AddSingleton<SqlServerSchemaExtractor>();
            
            // Register the cached schema extractor wrapping the SQL Server implementation
            services.AddSingleton<ISchemaExtractor>(provider => 
            {
                var sqlExtractor = provider.GetRequiredService<SqlServerSchemaExtractor>();
                var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedSchemaExtractor>>();
                
                return new CachedSchemaExtractor(sqlExtractor, memoryCache, logger);
            });
            
            // Register memory cache if not already registered
            services.AddMemoryCache();
            
            return services;
        }
    }
}
