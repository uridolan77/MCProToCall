using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProgressPlayReporting.SchemaExtractor
{
    public class CachedSchemaExtractor : ISchemaExtractor
    {
        private readonly ISchemaExtractor _innerExtractor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedSchemaExtractor> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

        public CachedSchemaExtractor(
            ISchemaExtractor innerExtractor,
            IMemoryCache cache,
            ILogger<CachedSchemaExtractor> logger)
        {
            _innerExtractor = innerExtractor ?? throw new ArgumentNullException(nameof(innerExtractor));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString)
        {
            var cacheKey = $"schema:{GetConnectionStringHash(connectionString)}";
            
            if (_cache.TryGetValue(cacheKey, out DatabaseSchema cachedSchema))
            {
                _logger.LogInformation("Returning cached database schema");
                return cachedSchema;
            }

            _logger.LogInformation("Cache miss for database schema; extracting from database");
            var schema = await _innerExtractor.ExtractSchemaAsync(connectionString);
            
            // Cache the schema with expiration
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheDuration);
            
            _cache.Set(cacheKey, schema, cacheOptions);
            
            return schema;
        }

        public async Task<TableSchema> GetTableSchemaAsync(string connectionString, string tableName)
        {
            var cacheKey = $"table:{GetConnectionStringHash(connectionString)}:{tableName}";
            
            if (_cache.TryGetValue(cacheKey, out TableSchema cachedTableSchema))
            {
                _logger.LogInformation("Returning cached schema for table {TableName}", tableName);
                return cachedTableSchema;
            }

            _logger.LogInformation("Cache miss for table {TableName}; extracting from database", tableName);
            var tableSchema = await _innerExtractor.GetTableSchemaAsync(connectionString, tableName);
            
            // Cache the table schema with expiration
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheDuration);
            
            _cache.Set(cacheKey, tableSchema, cacheOptions);
            
            return tableSchema;
        }

        public async Task<IEnumerable<string>> GetAllTableNamesAsync(string connectionString)
        {
            var cacheKey = $"tables:{GetConnectionStringHash(connectionString)}";
            
            if (_cache.TryGetValue(cacheKey, out IEnumerable<string> cachedTableNames))
            {
                _logger.LogInformation("Returning cached table names");
                return cachedTableNames;
            }

            _logger.LogInformation("Cache miss for table names; extracting from database");
            var tableNames = await _innerExtractor.GetAllTableNamesAsync(connectionString);
            
            // Cache the table names with expiration
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheDuration);
            
            _cache.Set(cacheKey, tableNames, cacheOptions);
            
            return tableNames;
        }

        /// <summary>
        /// Creates a hash of the connection string to use as part of the cache key
        /// The hash is used to avoid storing sensitive connection string details in cache keys
        /// </summary>
        private string GetConnectionStringHash(string connectionString)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(connectionString));
            return Convert.ToBase64String(bytes).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }
}
