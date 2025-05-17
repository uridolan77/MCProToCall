using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BasicClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Create configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Create service collection
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // Configure SimpleClient
            services.Configure<SimpleClientOptions>(configuration.GetSection("SimpleClient"));
            services.AddSingleton<SimpleClient>();

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get the client
            var client = serviceProvider.GetRequiredService<SimpleClient>();

            try
            {
                // Connect to the server
                await client.ConnectAsync();
                Console.WriteLine("Connected to server.");

                // Send a message
                await client.SendMessageAsync("Hello, server!");

                // Wait for a response
                var response = await client.ReceiveMessageAsync();
                Console.WriteLine($"Received: {response}");

                // Disconnect
                await client.DisconnectAsync();
                Console.WriteLine("Disconnected from server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
