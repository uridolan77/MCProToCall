# **ProgressPlay Reporting MCP: Proof of Concept**

I'll create a comprehensive POC that implements the three core components you requested. This proof of concept builds upon the Model Context Protocol framework we reviewed while focusing specifically on AI-driven report generation.

## **Project Structure**

ProgressPlayReporting.POC/  
├── src/  
│   ├── ProgressPlayReporting.Core/           \# Core libraries and interfaces  
│   ├── ProgressPlayReporting.SchemaExtractor/ \# Schema extraction service  
│   ├── ProgressPlayReporting.LlmIntegration/  \# LLM integration  
│   ├── ProgressPlayReporting.Validators/     \# Validation components  
│   └── ProgressPlayReporting.Api/            \# API endpoints  
├── tests/  
│   ├── ProgressPlayReporting.SchemaExtractor.Tests/  
│   ├── ProgressPlayReporting.LlmIntegration.Tests/  
│   └── ProgressPlayReporting.Validators.Tests/  
└── docker-compose.yml                        \# For local development

Let's implement each component:

## **1\. Schema Extraction Service**

First, let's create the interfaces in the Core project:

// ProgressPlayReporting.Core/Interfaces/ISchemaExtractor.cs  
namespace ProgressPlayReporting.Core.Interfaces  
{  
    public interface ISchemaExtractor  
    {  
        Task\<DatabaseSchema\> ExtractSchemaAsync(string connectionString);  
        Task\<TableSchema\> GetTableSchemaAsync(string connectionString, string tableName);  
        Task\<IEnumerable\<string\>\> GetAllTableNamesAsync(string connectionString);  
    }

    public class DatabaseSchema  
    {  
        public string DatabaseName { get; set; }  
        public List\<TableSchema\> Tables { get; set; } \= new List\<TableSchema\>();  
        public List\<string\> Dimensions { get; set; } \= new List\<string\>();  
        public List\<string\> Facts { get; set; } \= new List\<string\>();  
    }

    public class TableSchema  
    {  
        public string TableName { get; set; }  
        public List\<ColumnSchema\> Columns { get; set; } \= new List\<ColumnSchema\>();  
        public List\<IndexSchema\> Indexes { get; set; } \= new List\<IndexSchema\>();  
        public List\<ForeignKeySchema\> ForeignKeys { get; set; } \= new List\<ForeignKeySchema\>();  
    }

    public class ColumnSchema  
    {  
        public string ColumnName { get; set; }  
        public string DataType { get; set; }  
        public bool IsNullable { get; set; }  
        public bool IsPrimaryKey { get; set; }  
        public bool IsForeignKey { get; set; }  
        public string ReferencedTable { get; set; }  
        public string ReferencedColumn { get; set; }  
    }

    public class IndexSchema  
    {  
        public string IndexName { get; set; }  
        public bool IsUnique { get; set; }  
        public List\<string\> Columns { get; set; } \= new List\<string\>();  
    }

    public class ForeignKeySchema  
    {  
        public string ConstraintName { get; set; }  
        public string ColumnName { get; set; }  
        public string ReferencedTable { get; set; }  
        public string ReferencedColumn { get; set; }  
    }  
}

Now let's implement the SQL Server schema extractor:

// ProgressPlayReporting.SchemaExtractor/SqlServerSchemaExtractor.cs  
using Microsoft.Data.SqlClient;  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;  
using System.Data;

namespace ProgressPlayReporting.SchemaExtractor  
{  
    public class SqlServerSchemaExtractor : ISchemaExtractor  
    {  
        private readonly ILogger\<SqlServerSchemaExtractor\> \_logger;

        public SqlServerSchemaExtractor(ILogger\<SqlServerSchemaExtractor\> logger)  
        {  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public async Task\<DatabaseSchema\> ExtractSchemaAsync(string connectionString)  
        {  
            \_logger.LogInformation("Extracting database schema");  
              
            using var connection \= new SqlConnection(connectionString);  
            await connection.OpenAsync();  
              
            var databaseName \= connection.Database;  
            \_logger.LogInformation("Connected to database: {Database}", databaseName);  
              
            var tableNames \= await GetAllTableNamesAsync(connection);  
              
            var schema \= new DatabaseSchema   
            {   
                DatabaseName \= databaseName   
            };  
              
            foreach (var tableName in tableNames)  
            {  
                var tableSchema \= await GetTableSchemaInternalAsync(connection, tableName);  
                schema.Tables.Add(tableSchema);  
            }  
              
            // Infer dimensions and facts based on column naming conventions  
            InferDimensionsAndFacts(schema);  
              
            \_logger.LogInformation("Schema extraction completed. Found {TableCount} tables", schema.Tables.Count);  
              
            return schema;  
        }

        public async Task\<TableSchema\> GetTableSchemaAsync(string connectionString, string tableName)  
        {  
            using var connection \= new SqlConnection(connectionString);  
            await connection.OpenAsync();  
              
            return await GetTableSchemaInternalAsync(connection, tableName);  
        }

        public async Task\<IEnumerable\<string\>\> GetAllTableNamesAsync(string connectionString)  
        {  
            using var connection \= new SqlConnection(connectionString);  
            await connection.OpenAsync();  
              
            return await GetAllTableNamesAsync(connection);  
        }

        private async Task\<IEnumerable\<string\>\> GetAllTableNamesAsync(SqlConnection connection)  
        {  
            var tableNames \= new List\<string\>();  
              
            const string sql \= @"  
                SELECT TABLE\_NAME   
                FROM INFORMATION\_SCHEMA.TABLES   
                WHERE TABLE\_TYPE \= 'BASE TABLE'   
                ORDER BY TABLE\_NAME";  
              
            using var command \= new SqlCommand(sql, connection);  
            using var reader \= await command.ExecuteReaderAsync();  
              
            while (await reader.ReadAsync())  
            {  
                tableNames.Add(reader.GetString(0));  
            }  
              
            return tableNames;  
        }

        private async Task\<TableSchema\> GetTableSchemaInternalAsync(SqlConnection connection, string tableName)  
        {  
            \_logger.LogDebug("Extracting schema for table: {TableName}", tableName);  
              
            var table \= new TableSchema { TableName \= tableName };  
              
            // Get columns  
            await GetColumnsAsync(connection, tableName, table);  
              
            // Get primary keys  
            await GetPrimaryKeysAsync(connection, tableName, table);  
              
            // Get foreign keys  
            await GetForeignKeysAsync(connection, tableName, table);  
              
            // Get indexes  
            await GetIndexesAsync(connection, tableName, table);  
              
            return table;  
        }

        private async Task GetColumnsAsync(SqlConnection connection, string tableName, TableSchema table)  
        {  
            const string sql \= @"  
                SELECT   
                    COLUMN\_NAME,   
                    DATA\_TYPE,  
                    IS\_NULLABLE,  
                    CHARACTER\_MAXIMUM\_LENGTH,  
                    NUMERIC\_PRECISION,  
                    NUMERIC\_SCALE  
                FROM INFORMATION\_SCHEMA.COLUMNS  
                WHERE TABLE\_NAME \= @TableName  
                ORDER BY ORDINAL\_POSITION";  
              
            using var command \= new SqlCommand(sql, connection);  
            command.Parameters.AddWithValue("@TableName", tableName);  
              
            using var reader \= await command.ExecuteReaderAsync();  
              
            while (await reader.ReadAsync())  
            {  
                var dataType \= reader.GetString(1);  
                  
                // Append length/precision/scale to data type where applicable  
                if (\!reader.IsDBNull(3) && reader.GetInt32(3) \!= \-1)  
                {  
                    dataType \+= $"({reader.GetInt32(3)})";  
                }  
                else if (\!reader.IsDBNull(4))  
                {  
                    var precision \= reader.GetInt32(4);  
                    var scale \= reader.IsDBNull(5) ? 0 : reader.GetInt32(5);  
                    dataType \+= $"({precision},{scale})";  
                }  
                  
                var column \= new ColumnSchema  
                {  
                    ColumnName \= reader.GetString(0),  
                    DataType \= dataType,  
                    IsNullable \= reader.GetString(2) \== "YES"  
                };  
                  
                table.Columns.Add(column);  
            }  
        }

        private async Task GetPrimaryKeysAsync(SqlConnection connection, string tableName, TableSchema table)  
        {  
            const string sql \= @"  
                SELECT   
                    TC.CONSTRAINT\_NAME,   
                    KCU.COLUMN\_NAME  
                FROM INFORMATION\_SCHEMA.TABLE\_CONSTRAINTS TC  
                JOIN INFORMATION\_SCHEMA.KEY\_COLUMN\_USAGE KCU   
                    ON TC.CONSTRAINT\_NAME \= KCU.CONSTRAINT\_NAME  
                WHERE TC.CONSTRAINT\_TYPE \= 'PRIMARY KEY'  
                AND TC.TABLE\_NAME \= @TableName";  
              
            using var command \= new SqlCommand(sql, connection);  
            command.Parameters.AddWithValue("@TableName", tableName);  
              
            using var reader \= await command.ExecuteReaderAsync();  
              
            while (await reader.ReadAsync())  
            {  
                var columnName \= reader.GetString(1);  
                var column \= table.Columns.FirstOrDefault(c \=\> c.ColumnName \== columnName);  
                  
                if (column \!= null)  
                {  
                    column.IsPrimaryKey \= true;  
                }  
            }  
        }

        private async Task GetForeignKeysAsync(SqlConnection connection, string tableName, TableSchema table)  
        {  
            const string sql \= @"  
                SELECT   
                    RC.CONSTRAINT\_NAME,  
                    KCU.COLUMN\_NAME,  
                    KCU2.TABLE\_NAME AS REFERENCED\_TABLE,  
                    KCU2.COLUMN\_NAME AS REFERENCED\_COLUMN  
                FROM INFORMATION\_SCHEMA.REFERENTIAL\_CONSTRAINTS RC  
                JOIN INFORMATION\_SCHEMA.KEY\_COLUMN\_USAGE KCU   
                    ON RC.CONSTRAINT\_NAME \= KCU.CONSTRAINT\_NAME  
                JOIN INFORMATION\_SCHEMA.KEY\_COLUMN\_USAGE KCU2  
                    ON RC.UNIQUE\_CONSTRAINT\_NAME \= KCU2.CONSTRAINT\_NAME  
                WHERE KCU.TABLE\_NAME \= @TableName";  
              
            using var command \= new SqlCommand(sql, connection);  
            command.Parameters.AddWithValue("@TableName", tableName);  
              
            using var reader \= await command.ExecuteReaderAsync();  
              
            while (await reader.ReadAsync())  
            {  
                var columnName \= reader.GetString(1);  
                var referencedTable \= reader.GetString(2);  
                var referencedColumn \= reader.GetString(3);  
                  
                var column \= table.Columns.FirstOrDefault(c \=\> c.ColumnName \== columnName);  
                if (column \!= null)  
                {  
                    column.IsForeignKey \= true;  
                    column.ReferencedTable \= referencedTable;  
                    column.ReferencedColumn \= referencedColumn;  
                }  
                  
                var fk \= new ForeignKeySchema  
                {  
                    ConstraintName \= reader.GetString(0),  
                    ColumnName \= columnName,  
                    ReferencedTable \= referencedTable,  
                    ReferencedColumn \= referencedColumn  
                };  
                  
                table.ForeignKeys.Add(fk);  
            }  
        }

        private async Task GetIndexesAsync(SqlConnection connection, string tableName, TableSchema table)  
        {  
            const string sql \= @"  
                SELECT   
                    idx.name AS IndexName,  
                    idx.is\_unique AS IsUnique,  
                    col.name AS ColumnName  
                FROM sys.indexes idx  
                JOIN sys.tables t ON idx.object\_id \= t.object\_id  
                JOIN sys.index\_columns idxcol ON idx.object\_id \= idxcol.object\_id   
                    AND idx.index\_id \= idxcol.index\_id  
                JOIN sys.columns col ON idxcol.object\_id \= col.object\_id   
                    AND idxcol.column\_id \= col.column\_id  
                WHERE t.name \= @TableName  
                AND idx.is\_primary\_key \= 0  
                ORDER BY idx.name, idxcol.key\_ordinal";  
              
            using var command \= new SqlCommand(sql, connection);  
            command.Parameters.AddWithValue("@TableName", tableName);  
              
            using var reader \= await command.ExecuteReaderAsync();  
              
            var currentIndexName \= string.Empty;  
            IndexSchema currentIndex \= null;  
              
            while (await reader.ReadAsync())  
            {  
                var indexName \= reader.GetString(0);  
                  
                if (indexName \!= currentIndexName)  
                {  
                    currentIndexName \= indexName;  
                    currentIndex \= new IndexSchema  
                    {  
                        IndexName \= indexName,  
                        IsUnique \= reader.GetBoolean(1)  
                    };  
                    table.Indexes.Add(currentIndex);  
                }  
                  
                currentIndex.Columns.Add(reader.GetString(2));  
            }  
        }

        private void InferDimensionsAndFacts(DatabaseSchema schema)  
        {  
            var dimensions \= new HashSet\<string\>();  
            var facts \= new HashSet\<string\>();  
              
            // Simple heuristics for inferring dimensions and facts  
            foreach (var table in schema.Tables)  
            {  
                // Tables with "dim\_" prefix or dimension in name are dimensions  
                if (table.TableName.StartsWith("dim\_", StringComparison.OrdinalIgnoreCase) ||  
                    table.TableName.Contains("dimension", StringComparison.OrdinalIgnoreCase))  
                {  
                    dimensions.Add(table.TableName.Replace("dim\_", "").Replace("dimension", ""));  
                    continue;  
                }  
                  
                // Tables with "fact\_" prefix or fact in name are facts  
                if (table.TableName.StartsWith("fact\_", StringComparison.OrdinalIgnoreCase) ||  
                    table.TableName.Contains("fact", StringComparison.OrdinalIgnoreCase))  
                {  
                    facts.Add(table.TableName.Replace("fact\_", "").Replace("fact", ""));  
                    continue;  
                }  
                  
                // Check columns for common dimension/fact patterns  
                bool hasMeasures \= false;  
                foreach (var column in table.Columns)  
                {  
                    // Number columns with Amount, Total, Sum are likely measures  
                    if ((column.DataType.StartsWith("decimal") ||   
                         column.DataType.StartsWith("money") ||  
                         column.DataType.StartsWith("int")) &&   
                        (column.ColumnName.Contains("Amount") ||  
                         column.ColumnName.Contains("Total") ||  
                         column.ColumnName.Contains("Sum") ||  
                         column.ColumnName.Contains("Count") ||  
                         column.ColumnName.Contains("Wagered") ||  
                         column.ColumnName.EndsWith("Value")))  
                    {  
                        hasMeasures \= true;  
                    }  
                }  
                  
                // Tables with ID, name, and few measures are likely dimensions  
                if (\!hasMeasures && table.Columns.Any(c \=\> c.ColumnName.EndsWith("ID") ||   
                                                          c.ColumnName.EndsWith("Name") ||   
                                                          c.ColumnName.EndsWith("Code")))  
                {  
                    dimensions.Add(table.TableName.Replace("tbl\_", ""));  
                }  
                  
                // Tables with several measures are likely facts  
                if (hasMeasures)  
                {  
                    facts.Add(table.TableName.Replace("tbl\_", ""));  
                }  
            }  
              
            // Common dimension names for gaming industry  
            var commonDimensions \= new\[\] { "Player", "Brand", "Game", "Country", "Currency", "Platform" };  
            foreach (var dim in commonDimensions)  
            {  
                if (schema.Tables.Any(t \=\> t.TableName.Contains(dim, StringComparison.OrdinalIgnoreCase)))  
                {  
                    dimensions.Add(dim);  
                }  
            }  
              
            // Common facts for gaming industry  
            var commonFacts \= new\[\] { "Wagers", "Deposits", "Wins", "Bonuses", "Withdrawals" };  
            foreach (var fact in commonFacts)  
            {  
                if (schema.Tables.Any(t \=\> t.TableName.Contains(fact, StringComparison.OrdinalIgnoreCase)))  
                {  
                    facts.Add(fact);  
                }  
            }  
              
            schema.Dimensions \= dimensions.ToList();  
            schema.Facts \= facts.ToList();  
        }  
    }  
}

Let's also implement a caching layer for the schema to avoid redundant database calls:

// ProgressPlayReporting.SchemaExtractor/CachedSchemaExtractor.cs  
using Microsoft.Extensions.Caching.Memory;  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.SchemaExtractor  
{  
    public class CachedSchemaExtractor : ISchemaExtractor  
    {  
        private readonly ISchemaExtractor \_innerExtractor;  
        private readonly IMemoryCache \_cache;  
        private readonly ILogger\<CachedSchemaExtractor\> \_logger;  
        private readonly TimeSpan \_cacheDuration \= TimeSpan.FromHours(24);

        public CachedSchemaExtractor(  
            ISchemaExtractor innerExtractor,  
            IMemoryCache cache,  
            ILogger\<CachedSchemaExtractor\> logger)  
        {  
            \_innerExtractor \= innerExtractor ?? throw new ArgumentNullException(nameof(innerExtractor));  
            \_cache \= cache ?? throw new ArgumentNullException(nameof(cache));  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public async Task\<DatabaseSchema\> ExtractSchemaAsync(string connectionString)  
        {  
            var cacheKey \= $"schema:{GetConnectionStringHash(connectionString)}";  
              
            if (\!\_cache.TryGetValue(cacheKey, out DatabaseSchema cachedSchema))  
            {  
                \_logger.LogInformation("Schema not found in cache. Extracting from database...");  
                cachedSchema \= await \_innerExtractor.ExtractSchemaAsync(connectionString);  
                  
                var cacheOptions \= new MemoryCacheEntryOptions()  
                    .SetSlidingExpiration(\_cacheDuration)  
                    .SetPriority(CacheItemPriority.High);  
                  
                \_cache.Set(cacheKey, cachedSchema, cacheOptions);  
                \_logger.LogInformation("Schema cached for {Duration}", \_cacheDuration);  
            }  
            else  
            {  
                \_logger.LogInformation("Schema retrieved from cache");  
            }  
              
            return cachedSchema;  
        }

        public async Task\<TableSchema\> GetTableSchemaAsync(string connectionString, string tableName)  
        {  
            var cacheKey \= $"schema:table:{GetConnectionStringHash(connectionString)}:{tableName}";  
              
            if (\!\_cache.TryGetValue(cacheKey, out TableSchema cachedTableSchema))  
            {  
                \_logger.LogInformation("Table schema not found in cache. Extracting from database...");  
                cachedTableSchema \= await \_innerExtractor.GetTableSchemaAsync(connectionString, tableName);  
                  
                var cacheOptions \= new MemoryCacheEntryOptions()  
                    .SetSlidingExpiration(\_cacheDuration)  
                    .SetPriority(CacheItemPriority.High);  
                  
                \_cache.Set(cacheKey, cachedTableSchema, cacheOptions);  
            }  
            else  
            {  
                \_logger.LogInformation("Table schema retrieved from cache");  
            }  
              
            return cachedTableSchema;  
        }

        public async Task\<IEnumerable\<string\>\> GetAllTableNamesAsync(string connectionString)  
        {  
            var cacheKey \= $"schema:tables:{GetConnectionStringHash(connectionString)}";  
              
            if (\!\_cache.TryGetValue(cacheKey, out IEnumerable\<string\> cachedTableNames))  
            {  
                \_logger.LogInformation("Table names not found in cache. Extracting from database...");  
                cachedTableNames \= await \_innerExtractor.GetAllTableNamesAsync(connectionString);  
                  
                var cacheOptions \= new MemoryCacheEntryOptions()  
                    .SetSlidingExpiration(\_cacheDuration)  
                    .SetPriority(CacheItemPriority.High);  
                  
                \_cache.Set(cacheKey, cachedTableNames, cacheOptions);  
            }  
            else  
            {  
                \_logger.LogInformation("Table names retrieved from cache");  
            }  
              
            return cachedTableNames;  
        }

        private string GetConnectionStringHash(string connectionString)  
        {  
            // Create a simplified hash of the connection string to use as cache key  
            // Do not use the actual connection string as it contains sensitive data  
            using var sha \= System.Security.Cryptography.SHA256.Create();  
            var hash \= sha.ComputeHash(Encoding.UTF8.GetBytes(connectionString));  
            return Convert.ToBase64String(hash);  
        }  
    }  
}

Add the dependency injection registration:

// ProgressPlayReporting.SchemaExtractor/ServiceCollectionExtensions.cs  
using Microsoft.Extensions.DependencyInjection;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.SchemaExtractor  
{  
    public static class ServiceCollectionExtensions  
    {  
        public static IServiceCollection AddSchemaExtraction(this IServiceCollection services)  
        {  
            services.AddMemoryCache();  
            services.AddSingleton\<ISchemaExtractor, SqlServerSchemaExtractor\>();  
            services.Decorate\<ISchemaExtractor, CachedSchemaExtractor\>();  
              
            return services;  
        }  
    }  
}

## **2\. LLM Integration**

First, let's create the interfaces:

// ProgressPlayReporting.Core/Interfaces/ILlmGateway.cs  
namespace ProgressPlayReporting.Core.Interfaces  
{  
    public interface ILlmGateway  
    {  
        Task\<LlmResponse\> GenerateReportComponentsAsync(ReportPrompt prompt, CancellationToken cancellationToken \= default);  
    }

    public class ReportPrompt  
    {  
        public string Description { get; set; }  
        public DatabaseSchema Schema { get; set; }  
        public CompanyGuidelines Guidelines { get; set; }  
        public Dictionary\<string, object\> AdditionalContext { get; set; } \= new Dictionary\<string, object\>();  
    }

    public class LlmResponse  
    {  
        public string SqlQuery { get; set; }  
        public string ReactComponent { get; set; }  
        public string ServerComponent { get; set; }  
        public Dictionary\<string, string\> AdditionalComponents { get; set; } \= new Dictionary\<string, string\>();  
        public bool Success { get; set; }  
        public string ErrorMessage { get; set; }  
    }

    public class CompanyGuidelines  
    {  
        public NamingConvention NamingConvention { get; set; } \= new NamingConvention();  
        public ReportRules ReportRules { get; set; } \= new ReportRules();  
        public Branding Branding { get; set; } \= new Branding();  
    }

    public class NamingConvention  
    {  
        public string Frontend { get; set; } \= "PascalCase for components, camelCase for props";  
        public string Backend { get; set; } \= "C\# .NET, Service-Repository pattern";  
        public string Database { get; set; } \= "tbl\_ prefix for tables";  
    }

    public class ReportRules  
    {  
        public string TimeFiltering { get; set; } \= "Every report must support date range filtering";  
        public string Grouping { get; set; } \= "Reports must be grouped by Brand and Game when relevant";  
        public Dictionary\<string, string\> Roles { get; set; } \= new Dictionary\<string, string\>();  
    }

    public class Branding  
    {  
        public string PrimaryColor { get; set; } \= "\#10B981";  
        public string Font { get; set; } \= "Inter";  
        public string LogoUrl { get; set; } \= "https://progressplay.com/assets/logo.svg";  
        public string Vibe { get; set; } \= "clean, fast, pro-gaming";  
    }  
}

Now, let's implement the Claude integration:

// ProgressPlayReporting.LlmIntegration/ClaudeLlmGateway.cs  
using Microsoft.Extensions.Logging;  
using Microsoft.Extensions.Options;  
using ProgressPlayReporting.Core.Interfaces;  
using System.Net.Http.Json;  
using System.Text;  
using System.Text.Json;

namespace ProgressPlayReporting.LlmIntegration  
{  
    public class ClaudeLlmGateway : ILlmGateway  
    {  
        private readonly HttpClient \_httpClient;  
        private readonly ILogger\<ClaudeLlmGateway\> \_logger;  
        private readonly ClaudeOptions \_options;  
        private readonly CircuitBreaker \_circuitBreaker;

        public ClaudeLlmGateway(  
            HttpClient httpClient,  
            IOptions\<ClaudeOptions\> options,  
            ILogger\<ClaudeLlmGateway\> logger)  
        {  
            \_httpClient \= httpClient ?? throw new ArgumentNullException(nameof(httpClient));  
            \_options \= options?.Value ?? throw new ArgumentNullException(nameof(options));  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
              
            \_httpClient.DefaultRequestHeaders.Add("x-api-key", \_options.ApiKey);  
            \_httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");  
              
            \_circuitBreaker \= new CircuitBreaker(3, TimeSpan.FromMinutes(1));  
        }

        public async Task\<LlmResponse\> GenerateReportComponentsAsync(ReportPrompt prompt, CancellationToken cancellationToken \= default)  
        {  
            return await \_circuitBreaker.ExecuteAsync(async () \=\>  
            {  
                try  
                {  
                    \_logger.LogInformation("Generating report components for prompt: {Description}", prompt.Description);  
                      
                    var systemMessage \= BuildSystemMessage(prompt);  
                    var userMessage \= prompt.Description;  
                      
                    var request \= new ClaudeRequest  
                    {  
                        Model \= \_options.Model,  
                        MaxTokens \= \_options.MaxTokens,  
                        Temperature \= \_options.Temperature,  
                        Messages \= new List\<ClaudeMessage\>  
                        {  
                            new ClaudeMessage { Role \= "system", Content \= systemMessage },  
                            new ClaudeMessage { Role \= "user", Content \= userMessage }  
                        }  
                    };  
                      
                    var content \= JsonContent.Create(request);  
                    var response \= await \_httpClient.PostAsync(\_options.ApiEndpoint, content, cancellationToken);  
                      
                    response.EnsureSuccessStatusCode();  
                      
                    var claudeResponse \= await response.Content.ReadFromJsonAsync\<ClaudeResponse\>(cancellationToken: cancellationToken);  
                      
                    var parsedResponse \= ParseClaudeResponse(claudeResponse.Content\[0\].Text);  
                      
                    \_logger.LogInformation("Successfully generated report components");  
                      
                    return parsedResponse;  
                }  
                catch (Exception ex)  
                {  
                    \_logger.LogError(ex, "Error generating report components");  
                      
                    return new LlmResponse  
                    {  
                        Success \= false,  
                        ErrorMessage \= $"Failed to generate report components: {ex.Message}"  
                    };  
                }  
            });  
        }

        private string BuildSystemMessage(ReportPrompt prompt)  
        {  
            var sb \= new StringBuilder();  
              
            sb.AppendLine("\# ProgressPlay Reporting System Context");  
            sb.AppendLine();  
            sb.AppendLine("You're helping to generate reporting components for the ProgressPlay gaming platform. Your output must match company conventions and branding.");  
            sb.AppendLine();  
              
            // Add company guidelines  
            sb.AppendLine("\#\# Company Guidelines");  
            sb.AppendLine();  
            sb.AppendLine("\#\#\# Naming Conventions");  
            sb.AppendLine($"- Frontend: {prompt.Guidelines.NamingConvention.Frontend}");  
            sb.AppendLine($"- Backend: {prompt.Guidelines.NamingConvention.Backend}");  
            sb.AppendLine($"- Database: {prompt.Guidelines.NamingConvention.Database}");  
            sb.AppendLine();  
              
            sb.AppendLine("\#\#\# Report Rules");  
            sb.AppendLine($"- {prompt.Guidelines.ReportRules.TimeFiltering}");  
            sb.AppendLine($"- {prompt.Guidelines.ReportRules.Grouping}");  
              
            if (prompt.Guidelines.ReportRules.Roles.Any())  
            {  
                sb.AppendLine("- Role-based access:");  
                foreach (var role in prompt.Guidelines.ReportRules.Roles)  
                {  
                    sb.AppendLine($"  \- {role.Key}: {role.Value}");  
                }  
            }  
            sb.AppendLine();  
              
            sb.AppendLine("\#\#\# Branding");  
            sb.AppendLine($"- Primary Color: {prompt.Guidelines.Branding.PrimaryColor}");  
            sb.AppendLine($"- Font: {prompt.Guidelines.Branding.Font}");  
            sb.AppendLine($"- Vibe: {prompt.Guidelines.Branding.Vibe}");  
            sb.AppendLine();  
              
            // Add database schema  
            sb.AppendLine("\#\# Database Schema");  
            sb.AppendLine();  
            sb.AppendLine($"Database: {prompt.Schema.DatabaseName}");  
            sb.AppendLine();  
              
            // Dimensions and Facts  
            sb.AppendLine("\#\#\# Dimensions and Facts");  
            sb.AppendLine("Dimensions: " \+ string.Join(", ", prompt.Schema.Dimensions));  
            sb.AppendLine("Facts: " \+ string.Join(", ", prompt.Schema.Facts));  
            sb.AppendLine();  
              
            // Add table information  
            sb.AppendLine("\#\#\# Tables");  
            foreach (var table in prompt.Schema.Tables)  
            {  
                sb.AppendLine($"- {table.TableName}");  
                foreach (var column in table.Columns)  
                {  
                    string metadata \= "";  
                    if (column.IsPrimaryKey) metadata \+= " \[PK\]";  
                    if (column.IsForeignKey) metadata \+= $" \[FK \-\> {column.ReferencedTable}.{column.ReferencedColumn}\]";  
                    if (\!column.IsNullable) metadata \+= " \[NOT NULL\]";  
                      
                    sb.AppendLine($"  \- {column.ColumnName} ({column.DataType}){metadata}");  
                }  
                sb.AppendLine();  
            }  
              
            // Instructions for output format  
            sb.AppendLine("\#\# Output Format");  
            sb.AppendLine();  
            sb.AppendLine("You must generate:");  
            sb.AppendLine();  
            sb.AppendLine("1. An SQL query for the report");  
            sb.AppendLine("2. A React component using Tailwind CSS");  
            sb.AppendLine();  
            sb.AppendLine("Format your response with markdown headers:");  
            sb.AppendLine();  
            sb.AppendLine("\`\`\`");  
            sb.AppendLine("\# SQL Query");  
            sb.AppendLine("\`\`\`sql");  
            sb.AppendLine("-- SQL query goes here");  
            sb.AppendLine("\`\`\`");  
            sb.AppendLine();  
            sb.AppendLine("\# React Component");  
            sb.AppendLine("\`\`\`tsx");  
            sb.AppendLine("// React component goes here");  
            sb.AppendLine("\`\`\`");  
            sb.AppendLine("\`\`\`");  
            sb.AppendLine();  
            sb.AppendLine("Ensure all reports follow best practices like parameterized queries, responsive design, and role-based access control.");  
              
            return sb.ToString();  
        }

        private LlmResponse ParseClaudeResponse(string responseText)  
        {  
            try  
            {  
                var response \= new LlmResponse { Success \= true };  
                  
                // Extract SQL Query  
                var sqlPattern \= @"\# SQL Query\\s+\`\`\`sql(.\*?)\`\`\`";  
                var sqlMatch \= System.Text.RegularExpressions.Regex.Match(responseText, sqlPattern, System.Text.RegularExpressions.RegexOptions.Singleline);  
                  
                if (sqlMatch.Success)  
                {  
                    response.SqlQuery \= sqlMatch.Groups\[1\].Value.Trim();  
                }  
                  
                // Extract React Component  
                var reactPattern \= @"\# React Component\\s+\`\`\`tsx(.\*?)\`\`\`";  
                var reactMatch \= System.Text.RegularExpressions.Regex.Match(responseText, reactPattern, System.Text.RegularExpressions.RegexOptions.Singleline);  
                  
                if (reactMatch.Success)  
                {  
                    response.ReactComponent \= reactMatch.Groups\[1\].Value.Trim();  
                }  
                  
                // Check if we successfully extracted both components  
                if (string.IsNullOrEmpty(response.SqlQuery) || string.IsNullOrEmpty(response.ReactComponent))  
                {  
                    response.Success \= false;  
                    response.ErrorMessage \= "Failed to parse LLM response into components";  
                    \_logger.LogWarning("Failed to parse LLM response into components. Raw response: {Response}", responseText);  
                }  
                  
                return response;  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogError(ex, "Error parsing LLM response");  
                return new LlmResponse  
                {  
                    Success \= false,  
                    ErrorMessage \= $"Error parsing LLM response: {ex.Message}"  
                };  
            }  
        }  
    }

    public class ClaudeOptions  
    {  
        public string ApiKey { get; set; }  
        public string ApiEndpoint { get; set; } \= "https://api.anthropic.com/v1/messages";  
        public string Model { get; set; } \= "claude-3-sonnet-20240229";  
        public int MaxTokens { get; set; } \= 4000;  
        public double Temperature { get; set; } \= 0.2;  
    }

    public class ClaudeRequest  
    {  
        public string Model { get; set; }  
        public int MaxTokens { get; set; }  
        public double Temperature { get; set; }  
        public List\<ClaudeMessage\> Messages { get; set; }  
    }

    public class ClaudeMessage  
    {  
        public string Role { get; set; }  
        public string Content { get; set; }  
    }

    public class ClaudeResponse  
    {  
        public string Id { get; set; }  
        public string Model { get; set; }  
        public List\<ClaudeContent\> Content { get; set; }  
    }

    public class ClaudeContent  
    {  
        public string Type { get; set; }  
        public string Text { get; set; }  
    }

    public class CircuitBreaker  
    {  
        private readonly int \_failureThreshold;  
        private readonly TimeSpan \_resetTimeout;  
        private int \_failureCount;  
        private DateTime \_lastFailure;  
        private bool \_isOpen;  
        private readonly object \_lock \= new object();  
          
        public CircuitBreaker(int failureThreshold, TimeSpan resetTimeout)  
        {  
            \_failureThreshold \= failureThreshold;  
            \_resetTimeout \= resetTimeout;  
        }  
          
        public async Task\<T\> ExecuteAsync\<T\>(Func\<Task\<T\>\> action)  
        {  
            lock (\_lock)  
            {  
                if (\_isOpen)  
                {  
                    if (DateTime.UtcNow \- \_lastFailure \> \_resetTimeout)  
                    {  
                        \_isOpen \= false;  
                        \_failureCount \= 0;  
                    }  
                    else  
                    {  
                        throw new CircuitBreakerOpenException("Circuit breaker is open");  
                    }  
                }  
            }  
              
            try  
            {  
                var result \= await action();  
                  
                lock (\_lock)  
                {  
                    \_failureCount \= 0;  
                }  
                  
                return result;  
            }  
            catch (Exception ex)  
            {  
                lock (\_lock)  
                {  
                    \_failureCount++;  
                    \_lastFailure \= DateTime.UtcNow;  
                      
                    if (\_failureCount \>= \_failureThreshold)  
                    {  
                        \_isOpen \= true;  
                    }  
                }  
                  
                throw;  
            }  
        }  
    }

    public class CircuitBreakerOpenException : Exception  
    {  
        public CircuitBreakerOpenException(string message) : base(message)  
        {  
        }  
    }  
}

Add dependency injection:

// ProgressPlayReporting.LlmIntegration/ServiceCollectionExtensions.cs  
using Microsoft.Extensions.Configuration;  
using Microsoft.Extensions.DependencyInjection;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.LlmIntegration  
{  
    public static class ServiceCollectionExtensions  
    {  
        public static IServiceCollection AddLlmIntegration(this IServiceCollection services, IConfiguration configuration)  
        {  
            services.Configure\<ClaudeOptions\>(configuration.GetSection("Claude"));  
              
            services.AddHttpClient\<ILlmGateway, ClaudeLlmGateway\>(client \=\>  
            {  
                client.Timeout \= TimeSpan.FromSeconds(120); // LLM requests can take time  
            });  
              
            return services;  
        }  
    }  
}

## **3\. Validation Service**

Let's create the validation interfaces and implementations:

// ProgressPlayReporting.Core/Interfaces/IReportValidator.cs  
namespace ProgressPlayReporting.Core.Interfaces  
{  
    public interface IReportValidator  
    {  
        Task\<ValidationResult\> ValidateAsync(LlmResponse response, ReportPrompt prompt, CancellationToken cancellationToken \= default);  
    }

    public interface IReportComponentValidator  
    {  
        Task\<ComponentValidationResult\> ValidateAsync(string component, ReportPrompt prompt, CancellationToken cancellationToken \= default);  
        string ComponentType { get; }  
    }

    public class ValidationResult  
    {  
        public bool IsValid { get; set; }  
        public List\<ValidationIssue\> Issues { get; set; } \= new List\<ValidationIssue\>();  
        public Dictionary\<string, ComponentValidationResult\> ComponentResults { get; set; } \= new Dictionary\<string, ComponentValidationResult\>();  
    }

    public class ComponentValidationResult  
    {  
        public bool IsValid { get; set; }  
        public List\<ValidationIssue\> Issues { get; set; } \= new List\<ValidationIssue\>();  
        public string ComponentType { get; set; }  
    }

    public class ValidationIssue  
    {  
        public string Code { get; set; }  
        public string Message { get; set; }  
        public IssueSeverity Severity { get; set; }  
        public string ComponentType { get; set; }  
        public int? LineNumber { get; set; }  
        public string Suggestion { get; set; }  
    }

    public enum IssueSeverity  
    {  
        Info,  
        Warning,  
        Error  
    }  
}

Now let's implement the validators:

// ProgressPlayReporting.Validators/SqlQueryValidator.cs  
using Microsoft.Data.SqlClient;  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;  
using System.Text.RegularExpressions;

namespace ProgressPlayReporting.Validators  
{  
    public class SqlQueryValidator : IReportComponentValidator  
    {  
        private readonly ILogger\<SqlQueryValidator\> \_logger;

        public string ComponentType \=\> "SqlQuery";

        public SqlQueryValidator(ILogger\<SqlQueryValidator\> logger)  
        {  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public async Task\<ComponentValidationResult\> ValidateAsync(  
            string component,   
            ReportPrompt prompt,   
            CancellationToken cancellationToken \= default)  
        {  
            var result \= new ComponentValidationResult  
            {  
                IsValid \= true,  
                ComponentType \= ComponentType,  
                Issues \= new List\<ValidationIssue\>()  
            };  
              
            if (string.IsNullOrWhiteSpace(component))  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL001",  
                    Message \= "SQL query is empty",  
                    Severity \= IssueSeverity.Error,  
                    ComponentType \= ComponentType  
                });  
                  
                return result;  
            }  
              
            // Check for SQL injection vulnerabilities  
            if (ContainsSqlInjectionRisk(component))  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL002",  
                    Message \= "SQL query may be vulnerable to SQL injection",  
                    Severity \= IssueSeverity.Error,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Use parameterized queries with @parameters"  
                });  
            }  
              
            // Check for destructive operations  
            if (ContainsDestructiveOperations(component))  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL003",  
                    Message \= "SQL query contains destructive operations (INSERT, UPDATE, DELETE, DROP, ALTER)",  
                    Severity \= IssueSeverity.Error,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Reports should only contain SELECT statements"  
                });  
            }  
              
            // Check for date range filter  
            if (\!ContainsDateRangeFilter(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL004",  
                    Message \= "SQL query does not contain date range filtering",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add WHERE clause with date range (e.g., BETWEEN @StartDate AND @EndDate)"  
                });  
            }  
              
            // Check for parameters  
            if (\!ContainsParameters(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL005",  
                    Message \= "SQL query does not use parameters",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Use parameters (e.g., @StartDate, @EndDate) for filtering"  
                });  
            }  
              
            // Check for table prefixes  
            if (\!UseCorrectTablePrefixes(component, prompt))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL006",  
                    Message \= "SQL query may not use correct table prefixes",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= $"Ensure table names use the correct prefix: {prompt.Guidelines.NamingConvention.Database}"  
                });  
            }  
              
            // Validate syntax (basic)  
            if (\!HasValidSyntax(component))  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "SQL007",  
                    Message \= "SQL query has syntax errors",  
                    Severity \= IssueSeverity.Error,  
                    ComponentType \= ComponentType  
                });  
            }  
              
            // Update overall validity  
            result.IsValid \= \!result.Issues.Any(i \=\> i.Severity \== IssueSeverity.Error);  
              
            return result;  
        }

        private bool ContainsSqlInjectionRisk(string sql)  
        {  
            // Check for string concatenation patterns that might indicate SQL injection risk  
            var pattern \= @"'\\s\*\\+|'\\s\*&|'\\s\*\\|\\|";  
            return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase);  
        }

        private bool ContainsDestructiveOperations(string sql)  
        {  
            // Check for potentially destructive SQL operations  
            var pattern \= @"\\b(INSERT|UPDATE|DELETE|DROP|ALTER)\\b";  
            return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase);  
        }

        private bool ContainsDateRangeFilter(string sql)  
        {  
            // Check for common date range filter patterns  
            var patterns \= new\[\]  
            {  
                @"\\bBETWEEN\\s+@\\w+\\s+AND\\s+@\\w+",  
                @"\\b(\>=|\>)\\s\*@\\w+\\s+AND\\s+(\<=|\<)\\s\*@\\w+",  
                @"\\b(\>=|\>)\\s\*@\\w+.\*?(\<=|\<)\\s\*@\\w+"  
            };  
              
            return patterns.Any(pattern \=\> Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase));  
        }

        private bool ContainsParameters(string sql)  
        {  
            // Check for parameters with @ prefix  
            var pattern \= @"@\\w+";  
            return Regex.IsMatch(sql, pattern);  
        }

        private bool UseCorrectTablePrefixes(string sql, ReportPrompt prompt)  
        {  
            var expectedPrefix \= "tbl\_";  
            if (prompt.Guidelines.NamingConvention.Database.Contains("tbl\_"))  
            {  
                // Extract table references  
                var tablePattern \= @"\\bFROM\\s+(\\w+)|JOIN\\s+(\\w+)";  
                var matches \= Regex.Matches(sql, tablePattern, RegexOptions.IgnoreCase);  
                  
                foreach (Match match in matches)  
                {  
                    var tableName \= match.Groups\[1\].Value;  
                    if (string.IsNullOrEmpty(tableName))  
                        tableName \= match.Groups\[2\].Value;  
                      
                    if (\!string.IsNullOrEmpty(tableName) && \!tableName.StartsWith(expectedPrefix))  
                    {  
                        return false;  
                    }  
                }  
            }  
              
            return true;  
        }

        private bool HasValidSyntax(string sql)  
        {  
            try  
            {  
                // Basic syntax check \- try to create a SQL command  
                // This won't catch all issues but will catch major syntax errors  
                using var connection \= new SqlConnection("Server=.;Database=master;Trusted\_Connection=True;");  
                using var command \= new SqlCommand(sql, connection);  
                  
                // Parse the command (doesn't execute it)  
                command.Prepare();  
                  
                return true;  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogWarning(ex, "SQL syntax validation failed");  
                return false;  
            }  
        }  
    }  
}

// ProgressPlayReporting.Validators/ReactComponentValidator.cs  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;  
using System.Text.RegularExpressions;

namespace ProgressPlayReporting.Validators  
{  
    public class ReactComponentValidator : IReportComponentValidator  
    {  
        private readonly ILogger\<ReactComponentValidator\> \_logger;

        public string ComponentType \=\> "ReactComponent";

        public ReactComponentValidator(ILogger\<ReactComponentValidator\> logger)  
        {  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public Task\<ComponentValidationResult\> ValidateAsync(  
            string component,   
            ReportPrompt prompt,   
            CancellationToken cancellationToken \= default)  
        {  
            var result \= new ComponentValidationResult  
            {  
                IsValid \= true,  
                ComponentType \= ComponentType,  
                Issues \= new List\<ValidationIssue\>()  
            };  
              
            if (string.IsNullOrWhiteSpace(component))  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT001",  
                    Message \= "React component is empty",  
                    Severity \= IssueSeverity.Error,  
                    ComponentType \= ComponentType  
                });  
                  
                return Task.FromResult(result);  
            }  
              
            // Check for React import  
            if (\!ContainsReactImport(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT002",  
                    Message \= "Component does not import React",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add import React from 'react'"  
                });  
            }  
              
            // Check for default export  
            if (\!ContainsDefaultExport(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT003",  
                    Message \= "Component does not have a default export",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add export default ComponentName"  
                });  
            }  
              
            // Check for date range filter  
            if (\!ContainsDateRangePicker(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT004",  
                    Message \= "Component does not include date range filtering",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add DateRangePicker component"  
                });  
            }  
              
            // Check for tailwind classes  
            if (\!UsesTailwind(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT005",  
                    Message \= "Component does not use Tailwind CSS classes",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Use Tailwind CSS classes for styling"  
                });  
            }  
              
            // Check for company branding color  
            if (\!UsesCompanyColors(component, prompt))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT006",  
                    Message \= "Component does not use company branding colors",  
                    Severity \= IssueSeverity.Info,  
                    ComponentType \= ComponentType,  
                    Suggestion \= $"Use primary color: {prompt.Guidelines.Branding.PrimaryColor}"  
                });  
            }  
              
            // Check for CSV export  
            if (\!ContainsCsvExport(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT007",  
                    Message \= "Component does not include CSV export functionality",  
                    Severity \= IssueSeverity.Info,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add CSV export button using react-csv or similar library"  
                });  
            }  
              
            // Check for PascalCase component name  
            if (\!UsesPascalCaseForComponent(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT008",  
                    Message \= "Component name does not use PascalCase",  
                    Severity \= IssueSeverity.Warning,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Rename component using PascalCase (e.g., DailyRevenueReport)"  
                });  
            }  
              
            // Check for responsive design  
            if (\!UsesResponsiveDesign(component))  
            {  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "REACT009",  
                    Message \= "Component may not be responsive",  
                    Severity \= IssueSeverity.Info,  
                    ComponentType \= ComponentType,  
                    Suggestion \= "Add responsive Tailwind classes (sm:, md:, lg:, etc.)"  
                });  
            }  
              
            // Update overall validity  
            result.IsValid \= \!result.Issues.Any(i \=\> i.Severity \== IssueSeverity.Error);  
              
            return Task.FromResult(result);  
        }

        private bool ContainsReactImport(string component)  
        {  
            var pattern \= @"import\\s+.\*?React.\*?from\\s+\['""\]react\['""\]";  
            return Regex.IsMatch(component, pattern);  
        }

        private bool ContainsDefaultExport(string component)  
        {  
            var pattern \= @"export\\s+default\\s+\\w+";  
            return Regex.IsMatch(component, pattern);  
        }

        private bool ContainsDateRangePicker(string component)  
        {  
            var patterns \= new\[\]  
            {  
                @"\<DateRangePicker",  
                @"\<DatePicker",  
                @"dateRange",  
                @"startDate.\*?endDate",  
                @"from.\*?to"  
            };  
              
            return patterns.Any(pattern \=\> Regex.IsMatch(component, pattern, RegexOptions.IgnoreCase));  
        }

        private bool UsesTailwind(string component)  
        {  
            var pattern \= @"className\\s\*=\\s\*\['""\]\[^""'\]\*?(flex|grid|p-\\d|m-\\d|text-|bg-|border-)";  
            return Regex.IsMatch(component, pattern);  
        }

        private bool UsesCompanyColors(string component, ReportPrompt prompt)  
        {  
            var primaryColorHex \= prompt.Guidelines.Branding.PrimaryColor.TrimStart('\#');  
            var primaryColorName \= "green-500"; // Assuming \#10B981 is close to green-500  
              
            var patterns \= new\[\]  
            {  
                $@"bg-\\\[{prompt.Guidelines.Branding.PrimaryColor}\\\]",  
                $@"text-\\\[{prompt.Guidelines.Branding.PrimaryColor}\\\]",  
                $@"border-\\\[{prompt.Guidelines.Branding.PrimaryColor}\\\]",  
                $@"bg-{primaryColorName}",  
                $@"text-{primaryColorName}",  
                $@"border-{primaryColorName}"  
            };  
              
            return patterns.Any(pattern \=\> Regex.IsMatch(component, pattern));  
        }

        private bool ContainsCsvExport(string component)  
        {  
            var patterns \= new\[\]  
            {  
                @"CSVLink",  
                @"exportCsv",  
                @"downloadCsv",  
                @"export.\*?csv",  
                @"csv.\*?export",  
                @"download.\*?csv"  
            };  
              
            return patterns.Any(pattern \=\> Regex.IsMatch(component, pattern, RegexOptions.IgnoreCase));  
        }

        private bool UsesPascalCaseForComponent(string component)  
        {  
            var pattern \= @"(?:function|const)\\s+(\[A-Z\]\\w\*)\\s\*(?:=|\\()";  
            var match \= Regex.Match(component, pattern);  
              
            return match.Success && match.Groups\[1\].Value\[0\] \== char.ToUpper(match.Groups\[1\].Value\[0\]);  
        }

        private bool UsesResponsiveDesign(string component)  
        {  
            var pattern \= @"(?:sm|md|lg|xl):";  
            return Regex.IsMatch(component, pattern);  
        }  
    }  
}

Now let's create the report validator that combines the individual component validators:

// ProgressPlayReporting.Validators/ReportValidator.cs  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Validators  
{  
    public class ReportValidator : IReportValidator  
    {  
        private readonly IEnumerable\<IReportComponentValidator\> \_validators;  
        private readonly ILogger\<ReportValidator\> \_logger;

        public ReportValidator(  
            IEnumerable\<IReportComponentValidator\> validators,  
            ILogger\<ReportValidator\> logger)  
        {  
            \_validators \= validators ?? throw new ArgumentNullException(nameof(validators));  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public async Task\<ValidationResult\> ValidateAsync(  
            LlmResponse response,   
            ReportPrompt prompt,   
            CancellationToken cancellationToken \= default)  
        {  
            \_logger.LogInformation("Validating LLM response");  
              
            var result \= new ValidationResult  
            {  
                IsValid \= true,  
                ComponentResults \= new Dictionary\<string, ComponentValidationResult\>()  
            };  
              
            if (response \== null)  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "VAL001",  
                    Message \= "LLM response is null",  
                    Severity \= IssueSeverity.Error  
                });  
                  
                return result;  
            }  
              
            if (\!response.Success)  
            {  
                result.IsValid \= false;  
                result.Issues.Add(new ValidationIssue  
                {  
                    Code \= "VAL002",  
                    Message \= $"LLM response indicates failure: {response.ErrorMessage}",  
                    Severity \= IssueSeverity.Error  
                });  
                  
                return result;  
            }  
              
            // Validate SQL query  
            var sqlValidator \= \_validators.FirstOrDefault(v \=\> v.ComponentType \== "SqlQuery");  
            if (sqlValidator \!= null)  
            {  
                var sqlResult \= await sqlValidator.ValidateAsync(response.SqlQuery, prompt, cancellationToken);  
                result.ComponentResults.Add("SqlQuery", sqlResult);  
                result.Issues.AddRange(sqlResult.Issues);  
                  
                if (\!sqlResult.IsValid)  
                {  
                    result.IsValid \= false;  
                }  
            }  
              
            // Validate React component  
            var reactValidator \= \_validators.FirstOrDefault(v \=\> v.ComponentType \== "ReactComponent");  
            if (reactValidator \!= null)  
            {  
                var reactResult \= await reactValidator.ValidateAsync(response.ReactComponent, prompt, cancellationToken);  
                result.ComponentResults.Add("ReactComponent", reactResult);  
                result.Issues.AddRange(reactResult.Issues);  
                  
                if (\!reactResult.IsValid)  
                {  
                    result.IsValid \= false;  
                }  
            }  
              
            // Validate additional components  
            foreach (var component in response.AdditionalComponents)  
            {  
                var validator \= \_validators.FirstOrDefault(v \=\> v.ComponentType \== component.Key);  
                if (validator \!= null)  
                {  
                    var componentResult \= await validator.ValidateAsync(component.Value, prompt, cancellationToken);  
                    result.ComponentResults.Add(component.Key, componentResult);  
                    result.Issues.AddRange(componentResult.Issues);  
                      
                    if (\!componentResult.IsValid)  
                    {  
                        result.IsValid \= false;  
                    }  
                }  
            }  
              
            \_logger.LogInformation(  
                "Validation completed with result: {IsValid}, Issues: {IssueCount}",   
                result.IsValid,   
                result.Issues.Count);  
              
            return result;  
        }  
    }  
}

Add dependency injection:

// ProgressPlayReporting.Validators/ServiceCollectionExtensions.cs  
using Microsoft.Extensions.DependencyInjection;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Validators  
{  
    public static class ServiceCollectionExtensions  
    {  
        public static IServiceCollection AddReportValidation(this IServiceCollection services)  
        {  
            // Register component validators  
            services.AddSingleton\<IReportComponentValidator, SqlQueryValidator\>();  
            services.AddSingleton\<IReportComponentValidator, ReactComponentValidator\>();  
              
            // Register the main validator  
            services.AddSingleton\<IReportValidator, ReportValidator\>();  
              
            return services;  
        }  
    }  
}

## **4\. API Controller and Report Generation Service**

Let's create a service that orchestrates the whole process:

// ProgressPlayReporting.Core/Interfaces/IReportGenerationService.cs  
namespace ProgressPlayReporting.Core.Interfaces  
{  
    public interface IReportGenerationService  
    {  
        Task\<ReportGenerationResult\> GenerateReportAsync(  
            string reportDescription,   
            string connectionString,  
            CancellationToken cancellationToken \= default);  
    }

    public class ReportGenerationResult  
    {  
        public bool Success { get; set; }  
        public string ErrorMessage { get; set; }  
        public string SqlQuery { get; set; }  
        public string ReactComponent { get; set; }  
        public ValidationResult ValidationResult { get; set; }  
    }  
}

Implementation:

// ProgressPlayReporting.Core/Services/ReportGenerationService.cs  
using Microsoft.Extensions.Logging;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Core.Services  
{  
    public class ReportGenerationService : IReportGenerationService  
    {  
        private readonly ISchemaExtractor \_schemaExtractor;  
        private readonly ILlmGateway \_llmGateway;  
        private readonly IReportValidator \_validator;  
        private readonly ILogger\<ReportGenerationService\> \_logger;

        public ReportGenerationService(  
            ISchemaExtractor schemaExtractor,  
            ILlmGateway llmGateway,  
            IReportValidator validator,  
            ILogger\<ReportGenerationService\> logger)  
        {  
            \_schemaExtractor \= schemaExtractor ?? throw new ArgumentNullException(nameof(schemaExtractor));  
            \_llmGateway \= llmGateway ?? throw new ArgumentNullException(nameof(llmGateway));  
            \_validator \= validator ?? throw new ArgumentNullException(nameof(validator));  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
        }

        public async Task\<ReportGenerationResult\> GenerateReportAsync(  
            string reportDescription,   
            string connectionString,  
            CancellationToken cancellationToken \= default)  
        {  
            try  
            {  
                \_logger.LogInformation("Starting report generation for: {Description}", reportDescription);  
                  
                // Step 1: Extract database schema  
                \_logger.LogInformation("Extracting database schema");  
                var schema \= await \_schemaExtractor.ExtractSchemaAsync(connectionString);  
                  
                // Step 2: Prepare the prompt with schema and company guidelines  
                var prompt \= new ReportPrompt  
                {  
                    Description \= reportDescription,  
                    Schema \= schema,  
                    Guidelines \= GetDefaultGuidelines()  
                };  
                  
                // Step 3: Generate report components using LLM  
                \_logger.LogInformation("Generating report components using LLM");  
                var llmResponse \= await \_llmGateway.GenerateReportComponentsAsync(prompt, cancellationToken);  
                  
                if (\!llmResponse.Success)  
                {  
                    \_logger.LogError("LLM generation failed: {Error}", llmResponse.ErrorMessage);  
                    return new ReportGenerationResult  
                    {  
                        Success \= false,  
                        ErrorMessage \= llmResponse.ErrorMessage  
                    };  
                }  
                  
                // Step 4: Validate the generated components  
                \_logger.LogInformation("Validating generated components");  
                var validationResult \= await \_validator.ValidateAsync(llmResponse, prompt, cancellationToken);  
                  
                return new ReportGenerationResult  
                {  
                    Success \= validationResult.IsValid,  
                    ErrorMessage \= validationResult.IsValid ? null : "Validation failed, see validation result for details",  
                    SqlQuery \= llmResponse.SqlQuery,  
                    ReactComponent \= llmResponse.ReactComponent,  
                    ValidationResult \= validationResult  
                };  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogError(ex, "Error generating report");  
                return new ReportGenerationResult  
                {  
                    Success \= false,  
                    ErrorMessage \= $"Report generation failed: {ex.Message}"  
                };  
            }  
        }

        private CompanyGuidelines GetDefaultGuidelines()  
        {  
            return new CompanyGuidelines  
            {  
                NamingConvention \= new NamingConvention  
                {  
                    Frontend \= "PascalCase for components, camelCase for props",  
                    Backend \= "C\# .NET, Service-Repository pattern",  
                    Database \= "tbl\_ prefix for tables"  
                },  
                ReportRules \= new ReportRules  
                {  
                    TimeFiltering \= "Every report must support date range filtering",  
                    Grouping \= "Reports must be grouped by Brand and Game when relevant",  
                    Roles \= new Dictionary\<string, string\>  
                    {  
                        { "Admin", "sees all data" },  
                        { "Affiliate", "sees only assigned brands" }  
                    }  
                },  
                Branding \= new Branding  
                {  
                    PrimaryColor \= "\#10B981",  
                    Font \= "Inter",  
                    LogoUrl \= "https://progressplay.com/assets/logo.svg",  
                    Vibe \= "clean, fast, pro-gaming"  
                }  
            };  
        }  
    }  
}

Finally, let's create the API controller:

// ProgressPlayReporting.Api/Controllers/ReportsController.cs  
using Microsoft.AspNetCore.Mvc;  
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Api.Controllers  
{  
    \[ApiController\]  
    \[Route("api/\[controller\]")\]  
    public class ReportsController : ControllerBase  
    {  
        private readonly IReportGenerationService \_reportService;  
        private readonly ILogger\<ReportsController\> \_logger;  
        private readonly string \_connectionString;

        public ReportsController(  
            IReportGenerationService reportService,  
            IConfiguration configuration,  
            ILogger\<ReportsController\> logger)  
        {  
            \_reportService \= reportService ?? throw new ArgumentNullException(nameof(reportService));  
            \_logger \= logger ?? throw new ArgumentNullException(nameof(logger));  
            \_connectionString \= configuration.GetConnectionString("DefaultConnection") ??   
                               throw new ArgumentException("Connection string 'DefaultConnection' not found in configuration");  
        }

        \[HttpPost("generate")\]  
        public async Task\<IActionResult\> GenerateReport(\[FromBody\] GenerateReportRequest request, CancellationToken cancellationToken)  
        {  
            if (string.IsNullOrWhiteSpace(request.Description))  
            {  
                return BadRequest("Report description is required");  
            }  
              
            try  
            {  
                \_logger.LogInformation("Received report generation request: {Description}", request.Description);  
                  
                var result \= await \_reportService.GenerateReportAsync(  
                    request.Description,   
                    \_connectionString,   
                    cancellationToken);  
                  
                if (\!result.Success)  
                {  
                    \_logger.LogWarning("Report generation failed: {Error}", result.ErrorMessage);  
                    return StatusCode(500, new {   
                        error \= result.ErrorMessage,   
                        validationResult \= result.ValidationResult   
                    });  
                }  
                  
                return Ok(result);  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogError(ex, "Error processing report generation request");  
                return StatusCode(500, new { error \= ex.Message });  
            }  
        }

        \[HttpGet("sample")\]  
        public IActionResult GetSampleReport()  
        {  
            // Return a predefined sample report for demonstration purposes  
            return Ok(new  
            {  
                success \= true,  
                sqlQuery \= @"  
SELECT   
    CasinoName AS Brand,   
    CONVERT(DATE, DatePlayed) AS Day,  
    SUM(AmountWagered \- AmountWon) AS NetRevenue  
FROM tbl\_Daily\_actions\_players  
WHERE DatePlayed BETWEEN @StartDate AND @EndDate  
GROUP BY CasinoName, CONVERT(DATE, DatePlayed)  
ORDER BY CasinoName, Day",  
                reactComponent \= @"  
import React, { useEffect, useState } from 'react';  
import { DateRangePicker } from '@/components/DateRangePicker';  
import { CSVLink } from 'react-csv';

export default function DailyNetRevenueReport() {  
  const \[data, setData\] \= useState(\[\]);  
  const \[dateRange, setDateRange\] \= useState({   
    from: new Date(new Date().setDate(new Date().getDate() \- 30)),   
    to: new Date()   
  });  
  const \[isLoading, setIsLoading\] \= useState(false);

  useEffect(() \=\> {  
    const fetchData \= async () \=\> {  
      setIsLoading(true);  
      try {  
        const response \= await fetch('/api/reports/data', {  
          method: 'POST',  
          headers: { 'Content-Type': 'application/json' },  
          body: JSON.stringify({   
            query: 'daily-net-revenue',  
            startDate: dateRange.from.toISOString(),  
            endDate: dateRange.to.toISOString()  
          })  
        });  
          
        if (\!response.ok) throw new Error('Failed to fetch data');  
          
        const result \= await response.json();  
        setData(result);  
      } catch (error) {  
        console.error('Error fetching report data:', error);  
      } finally {  
        setIsLoading(false);  
      }  
    };  
      
    fetchData();  
  }, \[dateRange\]);

  return (  
    \<div className='bg-white p-6 rounded-lg shadow'\>  
      \<div className='flex justify-between items-center mb-6'\>  
        \<h1 className='text-xl font-semibold text-gray-800'\>Daily Net Revenue by Brand\</h1\>  
        \<div className='flex items-center space-x-4'\>  
          \<DateRangePicker value={dateRange} onChange={setDateRange} /\>  
          \<CSVLink   
            data={data}   
            filename='daily\_net\_revenue.csv'  
            className='px-4 py-2 bg-green-500 text-white rounded-md hover:bg-green-600 transition'  
          \>  
            Export CSV  
          \</CSVLink\>  
        \</div\>  
      \</div\>  
        
      {isLoading ? (  
        \<div className='flex justify-center py-12'\>  
          \<div className='animate-spin rounded-full h-10 w-10 border-b-2 border-green-500'\>\</div\>  
        \</div\>  
      ) : (  
        \<\>  
          \<div className='overflow-x-auto'\>  
            \<table className='min-w-full divide-y divide-gray-200'\>  
              \<thead className='bg-gray-50'\>  
                \<tr\>  
                  \<th className='px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider'\>Brand\</th\>  
                  \<th className='px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider'\>Date\</th\>  
                  \<th className='px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider'\>Net Revenue\</th\>  
                \</tr\>  
              \</thead\>  
              \<tbody className='bg-white divide-y divide-gray-200'\>  
                {data.map((row, index) \=\> (  
                  \<tr key={index} className={index % 2 \=== 0 ? 'bg-white' : 'bg-gray-50'}\>  
                    \<td className='px-6 py-4 whitespace-nowrap text-sm text-gray-900'\>{row.brand}\</td\>  
                    \<td className='px-6 py-4 whitespace-nowrap text-sm text-gray-900'\>{new Date(row.day).toLocaleDateString()}\</td\>  
                    \<td className='px-6 py-4 whitespace-nowrap text-sm text-right font-medium'\>  
                      ${parseFloat(row.netRevenue).toFixed(2)}  
                    \</td\>  
                  \</tr\>  
                ))}  
              \</tbody\>  
              \<tfoot className='bg-gray-50'\>  
                \<tr\>  
                  \<td className='px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider'\>Total\</td\>  
                  \<td\>\</td\>  
                  \<td className='px-6 py-3 text-right text-sm font-bold text-gray-900'\>  
                    ${data.reduce((sum, row) \=\> sum \+ parseFloat(row.netRevenue), 0).toFixed(2)}  
                  \</td\>  
                \</tr\>  
              \</tfoot\>  
            \</table\>  
          \</div\>  
            
          {data.length \=== 0 && \!isLoading && (  
            \<div className='text-center py-12 text-gray-500'\>  
              No data available for the selected date range  
            \</div\>  
          )}  
        \</\>  
      )}  
    \</div\>  
  );  
}"  
            });  
        }  
    }

    public class GenerateReportRequest  
    {  
        public string Description { get; set; }  
    }  
}

## **5\. Program.cs Setup**

// ProgressPlayReporting.Api/Program.cs  
using ProgressPlayReporting.Core.Interfaces;  
using ProgressPlayReporting.Core.Services;  
using ProgressPlayReporting.SchemaExtractor;  
using ProgressPlayReporting.LlmIntegration;  
using ProgressPlayReporting.Validators;

var builder \= WebApplication.CreateBuilder(args);

// Add services to the container  
builder.Services.AddControllers();  
builder.Services.AddEndpointsApiExplorer();  
builder.Services.AddSwaggerGen();

// Add our POC services  
builder.Services.AddSchemaExtraction()  
                .AddLlmIntegration(builder.Configuration)  
                .AddReportValidation();

// Register report generation service  
builder.Services.AddScoped\<IReportGenerationService, ReportGenerationService\>();

// Configure CORS  
builder.Services.AddCors(options \=\>  
{  
    options.AddPolicy("AllowAll", policy \=\>  
    {  
        policy.AllowAnyOrigin()  
              .AllowAnyMethod()  
              .AllowAnyHeader();  
    });  
});

var app \= builder.Build();

// Configure the HTTP request pipeline  
if (app.Environment.IsDevelopment())  
{  
    app.UseSwagger();  
    app.UseSwaggerUI();  
}

app.UseHttpsRedirection();  
app.UseCors("AllowAll");  
app.UseAuthorization();  
app.MapControllers();

app.Run();

## **6\. Configuration**

// ProgressPlayReporting.Api/appsettings.json  
{  
  "Logging": {  
    "LogLevel": {  
      "Default": "Information",  
      "Microsoft.AspNetCore": "Warning"  
    }  
  },  
  "AllowedHosts": "\*",  
  "ConnectionStrings": {  
    "DefaultConnection": "Server=localhost;Database=DailyActionsDB;Trusted\_Connection=True;TrustServerCertificate=True;"  
  },  
  "Claude": {  
    "ApiKey": "YOUR\_CLAUDE\_API\_KEY",  
    "Model": "claude-3-sonnet-20240229",  
    "MaxTokens": 4000,  
    "Temperature": 0.2  
  }  
}

## **7\. Docker Support**

\# ProgressPlayReporting.POC/Dockerfile  
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base  
WORKDIR /app  
EXPOSE 80  
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build  
WORKDIR /src  
COPY \["ProgressPlayReporting.Api/ProgressPlayReporting.Api.csproj", "ProgressPlayReporting.Api/"\]  
COPY \["ProgressPlayReporting.Core/ProgressPlayReporting.Core.csproj", "ProgressPlayReporting.Core/"\]  
COPY \["ProgressPlayReporting.SchemaExtractor/ProgressPlayReporting.SchemaExtractor.csproj", "ProgressPlayReporting.SchemaExtractor/"\]  
COPY \["ProgressPlayReporting.LlmIntegration/ProgressPlayReporting.LlmIntegration.csproj", "ProgressPlayReporting.LlmIntegration/"\]  
COPY \["ProgressPlayReporting.Validators/ProgressPlayReporting.Validators.csproj", "ProgressPlayReporting.Validators/"\]  
RUN dotnet restore "ProgressPlayReporting.Api/ProgressPlayReporting.Api.csproj"  
COPY . .  
WORKDIR "/src/ProgressPlayReporting.Api"  
RUN dotnet build "ProgressPlayReporting.Api.csproj" \-c Release \-o /app/build

FROM build AS publish  
RUN dotnet publish "ProgressPlayReporting.Api.csproj" \-c Release \-o /app/publish

FROM base AS final  
WORKDIR /app  
COPY \--from=publish /app/publish .  
ENTRYPOINT \["dotnet", "ProgressPlayReporting.Api.dll"\]

\# ProgressPlayReporting.POC/docker-compose.yml  
version: '3.8'

services:  
  api:  
    build:  
      context: .  
      dockerfile: Dockerfile  
    ports:  
      \- "5000:80"  
    environment:  
      \- ASPNETCORE\_ENVIRONMENT=Development  
      \- ConnectionStrings\_\_DefaultConnection=Server=db;Database=DailyActionsDB;User=sa;Password=YourStrong\!Password;TrustServerCertificate=True;  
      \- Claude\_\_ApiKey=${CLAUDE\_API\_KEY}  
    depends\_on:  
      \- db

  db:  
    image: mcr.microsoft.com/mssql/server:2019-latest  
    environment:  
      SA\_PASSWORD: "YourStrong\!Password"  
      ACCEPT\_EULA: "Y"  
    ports:  
      \- "1433:1433"  
    volumes:  
      \- sqlvolume:/var/opt/mssql

volumes:  
  sqlvolume:

## **Summary**

This comprehensive Proof of Concept demonstrates:

1. **Schema Extraction Service**:

   * Connects to SQL Server databases  
   * Extracts detailed schema information about tables, columns, relationships  
   * Infers dimensions and facts for reporting  
   * Implements caching to improve performance  
2. **LLM Integration**:

   * Connects to Claude API for generating report components  
   * Builds structured prompts with database schema and company guidelines  
   * Implements circuit breaker pattern for reliability  
   * Parses LLM responses into discrete components  
3. **Validation Service**:

   * Validates SQL query syntax and best practices  
   * Checks React components for company guidelines compliance  
   * Provides detailed validation issues with severity levels  
   * Offers suggestions for fixing issues

The POC follows clean architecture principles with:

* Clear separation of concerns  
* Dependency injection for testability  
* Exception handling and logging  
* Caching for performance optimization  
* Docker support for easy deployment

This foundation can be expanded to include more advanced features like:

* Integration with frontend frameworks  
* User feedback loops for LLM improvement  
* Report versioning and history  
* Additional report types and validations

With this POC, you can begin generating AI-powered reports that adhere to your company guidelines and leverage your existing database structure.

