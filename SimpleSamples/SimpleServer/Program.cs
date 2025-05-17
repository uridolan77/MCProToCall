using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PPrePorter.Core.Interfaces;
using PPrePorter.Core.Services;

namespace SimpleServer
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
        // Connection string template with Azure Key Vault placeholders
        private const string ConnectionStringTemplate =
            "data source=185.64.56.157;initial catalog=DailyActionsDB;persist security info=True;" +
            "user id={azurevault:progressplaymcp-kv:DailyActionsDB--Username};" +
            "password={azurevault:progressplaymcp-kv:DailyActionsDB--Password};";

        private static IServiceProvider _serviceProvider;
        private static ILogger<Program> _logger;
        private static DatabaseSchemaService _schemaService;

        static async Task Main(string[] args)
        {
            // Set up dependency injection
            _serviceProvider = ConfigureServices();
            _logger = _serviceProvider.GetRequiredService<ILogger<Program>>();

            // Get the schema service
            _schemaService = _serviceProvider.GetRequiredService<DatabaseSchemaService>();

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
                            var schema = await _schemaService.GetDatabaseSchemaAsync();
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

        static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add services
            services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
            services.AddSingleton<IConnectionStringCacheService, ConnectionStringCacheService>();
            services.AddSingleton<IConnectionStringResolverService, ConnectionStringResolverService>();
            services.AddSingleton<IAzureKeyVaultConnectionStringResolver, AzureKeyVaultConnectionStringResolver>();

            // Add the database schema service
            services.AddSingleton(provider => new DatabaseSchemaService(
                provider.GetRequiredService<ILogger<DatabaseSchemaService>>(),
                provider.GetRequiredService<IConnectionStringResolverService>(),
                ConnectionStringTemplate
            ));

            return services.BuildServiceProvider();
        }
    }
}
