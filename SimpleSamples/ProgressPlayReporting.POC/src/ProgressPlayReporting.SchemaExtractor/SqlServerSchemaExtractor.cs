using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProgressPlayReporting.SchemaExtractor
{
    public class SqlServerSchemaExtractor : ISchemaExtractor
    {
        private readonly ILogger<SqlServerSchemaExtractor> _logger;

        public SqlServerSchemaExtractor(ILogger<SqlServerSchemaExtractor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DatabaseSchema> ExtractSchemaAsync(string connectionString)
        {
            _logger.LogInformation("Extracting database schema");
            
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var databaseName = connection.Database;
            _logger.LogInformation("Connected to database: {Database}", databaseName);
            
            var tableNames = await GetAllTableNamesAsync(connection);
            
            var schema = new DatabaseSchema 
            { 
                DatabaseName = databaseName 
            };
            
            foreach (var tableName in tableNames)
            {
                var tableSchema = await GetTableSchemaInternalAsync(connection, tableName);
                schema.Tables.Add(tableSchema);
            }
            
            // Infer dimensions and facts based on column naming conventions
            InferDimensionsAndFacts(schema);
            
            _logger.LogInformation("Schema extraction completed. Found {TableCount} tables", schema.Tables.Count);
            
            return schema;
        }

        public async Task<TableSchema> GetTableSchemaAsync(string connectionString, string tableName)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            return await GetTableSchemaInternalAsync(connection, tableName);
        }

        public async Task<IEnumerable<string>> GetAllTableNamesAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            return await GetAllTableNamesAsync(connection);
        }

        private async Task<IEnumerable<string>> GetAllTableNamesAsync(SqlConnection connection)
        {
            var tableNames = new List<string>();
            
            const string sql = @"
                SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' 
                ORDER BY TABLE_NAME";
            
            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }
            
            return tableNames;
        }

        private async Task<TableSchema> GetTableSchemaInternalAsync(SqlConnection connection, string tableName)
        {
            _logger.LogDebug("Extracting schema for table: {TableName}", tableName);
            
            var table = new TableSchema { TableName = tableName };
            
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
            const string sql = @"
                SELECT 
                    COLUMN_NAME, 
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var dataType = reader.GetString(1);
                
                // Append length/precision/scale to data type where applicable
                if (!reader.IsDBNull(3) && reader.GetInt32(3) != -1)
                {
                    dataType += $"({reader.GetInt32(3)})";
                }
                else if (!reader.IsDBNull(4))
                {
                    var precision = reader.GetInt32(4);
                    var scale = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                    dataType += $"({precision},{scale})";
                }
                
                var column = new ColumnSchema
                {
                    ColumnName = reader.GetString(0),
                    DataType = dataType,
                    IsNullable = reader.GetString(2) == "YES"
                };
                
                table.Columns.Add(column);
            }
        }

        private async Task GetPrimaryKeysAsync(SqlConnection connection, string tableName, TableSchema table)
        {
            const string sql = @"
                SELECT 
                    TC.CONSTRAINT_NAME, 
                    KCU.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU 
                    ON TC.CONSTRAINT_NAME = KCU.CONSTRAINT_NAME
                WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
                AND TC.TABLE_NAME = @TableName";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                var column = table.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                
                if (column != null)
                {
                    column.IsPrimaryKey = true;
                }
            }
        }

        private async Task GetForeignKeysAsync(SqlConnection connection, string tableName, TableSchema table)
        {
            const string sql = @"
                SELECT 
                    RC.CONSTRAINT_NAME,
                    KCU.COLUMN_NAME,
                    KCU2.TABLE_NAME AS REFERENCED_TABLE,
                    KCU2.COLUMN_NAME AS REFERENCED_COLUMN
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU 
                    ON RC.CONSTRAINT_NAME = KCU.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU2
                    ON RC.UNIQUE_CONSTRAINT_NAME = KCU2.CONSTRAINT_NAME
                WHERE KCU.TABLE_NAME = @TableName";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                var referencedTable = reader.GetString(2);
                var referencedColumn = reader.GetString(3);
                
                var column = table.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                if (column != null)
                {
                    column.IsForeignKey = true;
                    column.ReferencedTable = referencedTable;
                    column.ReferencedColumn = referencedColumn;
                }
                
                var fk = new ForeignKeySchema
                {
                    ConstraintName = reader.GetString(0),
                    ColumnName = columnName,
                    ReferencedTable = referencedTable,
                    ReferencedColumn = referencedColumn
                };
                
                table.ForeignKeys.Add(fk);
            }
        }

        private async Task GetIndexesAsync(SqlConnection connection, string tableName, TableSchema table)
        {
            const string sql = @"
                SELECT 
                    idx.name AS IndexName,
                    idx.is_unique AS IsUnique,
                    col.name AS ColumnName,
                    idxcol.key_ordinal AS KeyOrdinal
                FROM sys.indexes idx
                JOIN sys.tables t ON idx.object_id = t.object_id
                JOIN sys.index_columns idxcol ON idx.object_id = idxcol.object_id 
                    AND idx.index_id = idxcol.index_id
                JOIN sys.columns col ON idxcol.object_id = col.object_id 
                    AND idxcol.column_id = col.column_id
                WHERE t.name = @TableName
                AND idx.is_primary_key = 0
                ORDER BY idx.name, idxcol.key_ordinal";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            
            using var reader = await command.ExecuteReaderAsync();
            
            var currentIndexName = string.Empty;
            IndexSchema currentIndex = null;
            
            while (await reader.ReadAsync())
            {
                var indexName = reader.GetString(0);
                
                if (indexName != currentIndexName)
                {
                    currentIndexName = indexName;
                    
                    currentIndex = new IndexSchema
                    {
                        IndexName = indexName,
                        IsUnique = reader.GetBoolean(1)
                    };
                    
                    table.Indexes.Add(currentIndex);
                }
                
                currentIndex.Columns.Add(reader.GetString(2));
            }
        }

        private void InferDimensionsAndFacts(DatabaseSchema schema)
        {
            var dimensions = new HashSet<string>();
            var facts = new HashSet<string>();
            
            // Simple heuristics for inferring dimensions and facts
            foreach (var table in schema.Tables)
            {
                // Tables with "dim_" prefix or dimension in name are dimensions
                if (table.TableName.StartsWith("dim_", StringComparison.OrdinalIgnoreCase) ||
                    table.TableName.Contains("dimension", StringComparison.OrdinalIgnoreCase))
                {
                    dimensions.Add(table.TableName.Replace("dim_", "").Replace("dimension", ""));
                    continue;
                }
                
                // Tables with "fact_" prefix or fact in name are facts
                if (table.TableName.StartsWith("fact_", StringComparison.OrdinalIgnoreCase) ||
                    table.TableName.Contains("fact", StringComparison.OrdinalIgnoreCase))
                {
                    facts.Add(table.TableName.Replace("fact_", "").Replace("fact", ""));
                    continue;
                }
            }
            
            // Common dimension names for gaming industry
            var commonDimensions = new[] { "Player", "Brand", "Game", "Country", "Currency", "Platform" };
            foreach (var dim in commonDimensions)
            {
                foreach (var table in schema.Tables)
                {
                    if (table.TableName.Contains(dim, StringComparison.OrdinalIgnoreCase))
                    {
                        dimensions.Add(dim);
                        break;
                    }
                }
            }
            
            // Common facts for gaming industry
            var commonFacts = new[] { "Deposit", "Withdrawal", "Bet", "Win", "Wager", "Bonus", "Transaction" };
            foreach (var fact in commonFacts)
            {
                foreach (var table in schema.Tables)
                {
                    if (table.TableName.Contains(fact, StringComparison.OrdinalIgnoreCase))
                    {
                        facts.Add(fact);
                        break;
                    }
                }
            }
            
            schema.Dimensions = dimensions.ToList();
            schema.Facts = facts.ToList();
        }
    }
}
