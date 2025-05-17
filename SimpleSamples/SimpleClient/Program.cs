using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleClient
{
    // These classes should match the server's schema classes
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
        static async Task Main(string[] args)
        {
            Console.WriteLine("Database Schema Client");
            Console.WriteLine("Connecting to server...");

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", 5000);
                    Console.WriteLine("Connected to server!");

                    NetworkStream stream = client.GetStream();

                    // Send request for database schema
                    string request = "get_schema";
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                    Console.WriteLine("Sent request: get_schema");

                    // Receive response
                    byte[] buffer = new byte[16384]; // Larger buffer for schema data
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine("\nReceived database schema:");

                    // Parse and display the schema
                    try
                    {
                        DatabaseSchema schema = JsonSerializer.Deserialize<DatabaseSchema>(response);

                        Console.WriteLine($"\nDatabase: {schema.DatabaseName}");
                        Console.WriteLine("\nTables:");

                        foreach (var table in schema.Tables)
                        {
                            Console.WriteLine($"\n  Table: {table.TableName}");
                            Console.WriteLine("  Columns:");

                            foreach (var column in table.Columns)
                            {
                                string columnInfo = $"    - {column.ColumnName} ({column.DataType})";

                                if (column.IsPrimaryKey)
                                    columnInfo += " [PK]";

                                if (column.IsForeignKey)
                                    columnInfo += $" [FK -> {column.ReferencedTable}.{column.ReferencedColumn}]";

                                if (!column.IsNullable)
                                    columnInfo += " [NOT NULL]";

                                Console.WriteLine(columnInfo);
                            }
                        }

                        // Save schema to file for Copilot to use
                        string schemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText("database_schema.json", schemaJson);
                        Console.WriteLine("\nSchema saved to database_schema.json for Copilot to use");
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing schema: {ex.Message}");
                        Console.WriteLine("Raw response:");
                        Console.WriteLine(response);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
