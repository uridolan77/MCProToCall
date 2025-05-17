using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MssqlSchemaServer
{
    // Simple class to represent database schema information
    public class DatabaseSchema
    {
        public string DatabaseName { get; set; }
        public List<TableSchema> Tables { get; set; } = new List<TableSchema>();
    }

    public class TableSchema
    {
        public string TableName { get; set; }
        public List<ColumnSchema> Columns { get; set; } = new List<ColumnSchema>();
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

    class Program
    {
        // Connection string with hardcoded credentials for demo purposes
        private const string ConnectionString =
            "data source=185.64.56.157;initial catalog=DailyActionsDB;persist security info=True;" +
            "user id=ReportsUser;password=Pp@123456;";

        private static IServiceProvider _serviceProvider;
        private static ILogger<Program> _logger;

        static async Task Main(string[] args)
        {
            // Set up dependency injection
            _serviceProvider = ConfigureServices();
            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();

            _logger.LogInformation("Starting database schema server...");

            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
            listener.Start();

            _logger.LogInformation("Server started on 127.0.0.1:5000");
            _logger.LogInformation("Waiting for client connections...");

            while (true)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _logger.LogInformation("Client connected!");

                    // Handle client in a separate task
                    _ = HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                }
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    _logger.LogInformation("Received request: {Request}", request);

                    // Process the request
                    string response;
                    if (request.Contains("get_schema"))
                    {
                        try
                        {
                            // Get the actual database schema
                            var schema = await GetDatabaseSchemaAsync();
                            response = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
                            _logger.LogInformation("Database schema retrieved successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error retrieving database schema");
                            response = $"Error retrieving database schema: {ex.Message}";
                        }
                    }
                    else
                    {
                        response = "Unknown request. Available commands: get_schema";
                    }

                    // Send the response
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                    _logger.LogInformation("Response sent to client");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client");
            }
        }

        static async Task<DatabaseSchema> GetDatabaseSchemaAsync()
        {
            _logger.LogInformation("Getting database schema");

            // Create the database schema object
            var schema = new DatabaseSchema();

            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    // Get the database name
                    schema.DatabaseName = connection.Database;
                    _logger.LogInformation("Connected to database: {DatabaseName}", schema.DatabaseName);

                    // Get all tables
                    var tables = await GetTablesAsync(connection);
                    schema.Tables = tables;

                    _logger.LogInformation("Retrieved schema for {TableCount} tables", tables.Count);
                }

                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database schema");
                throw;
            }
        }

        static async Task<List<TableSchema>> GetTablesAsync(SqlConnection connection)
        {
            var tables = new List<TableSchema>();

            // Query to get all user tables
            string tableQuery = @"
                SELECT
                    t.name AS TableName
                FROM
                    sys.tables t
                ORDER BY
                    t.name";

            using (var command = new SqlCommand(tableQuery, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string tableName = reader["TableName"].ToString();
                        tables.Add(new TableSchema { TableName = tableName });
                    }
                }
            }

            // For each table, get its columns
            foreach (var table in tables)
            {
                table.Columns = await GetColumnsAsync(connection, table.TableName);
                _logger.LogInformation("Retrieved {ColumnCount} columns for table {TableName}", table.Columns.Count, table.TableName);
            }

            return tables;
        }

        static async Task<List<ColumnSchema>> GetColumnsAsync(SqlConnection connection, string tableName)
        {
            var columns = new List<ColumnSchema>();

            // Query to get all columns for a table, including primary key and foreign key information
            string columnQuery = @"
                SELECT
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.max_length,
                    c.precision,
                    c.scale,
                    c.is_nullable,
                    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                    CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
                    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
                    COL_NAME(fk.referenced_object_id, fk.referenced_column_id) AS ReferencedColumn
                FROM
                    sys.columns c
                INNER JOIN
                    sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN
                    sys.index_columns pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id AND pk.index_id = 1
                LEFT JOIN
                    sys.foreign_key_columns fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
                WHERE
                    c.object_id = OBJECT_ID(@TableName)
                ORDER BY
                    c.column_id";

            using (var command = new SqlCommand(columnQuery, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string columnName = reader["ColumnName"].ToString();
                        string dataType = reader["DataType"].ToString();
                        int maxLength = Convert.ToInt32(reader["max_length"]);
                        byte precision = Convert.ToByte(reader["precision"]);
                        byte scale = Convert.ToByte(reader["scale"]);
                        bool isNullable = Convert.ToBoolean(reader["is_nullable"]);
                        bool isPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]);
                        bool isForeignKey = Convert.ToBoolean(reader["IsForeignKey"]);

                        // Format the data type with precision/scale/length as needed
                        string formattedDataType = FormatDataType(dataType, maxLength, precision, scale);

                        var column = new ColumnSchema
                        {
                            ColumnName = columnName,
                            DataType = formattedDataType,
                            IsNullable = isNullable,
                            IsPrimaryKey = isPrimaryKey,
                            IsForeignKey = isForeignKey
                        };

                        if (isForeignKey)
                        {
                            column.ReferencedTable = reader["ReferencedTable"] as string;
                            column.ReferencedColumn = reader["ReferencedColumn"] as string;
                        }

                        columns.Add(column);
                    }
                }
            }

            return columns;
        }

        static string FormatDataType(string dataType, int maxLength, byte precision, byte scale)
        {
            switch (dataType.ToLower())
            {
                case "nvarchar":
                case "nchar":
                    return maxLength == -1 ? $"{dataType}(max)" : $"{dataType}({maxLength / 2})";

                case "varchar":
                case "char":
                    return maxLength == -1 ? $"{dataType}(max)" : $"{dataType}({maxLength})";

                case "decimal":
                case "numeric":
                    return $"{dataType}({precision},{scale})";

                default:
                    return dataType;
            }
        }

        static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            return services.BuildServiceProvider();
        }
    }
}
