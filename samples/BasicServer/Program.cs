using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BasicServer
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

            // Configure SimpleServer
            services.Configure<SimpleServerOptions>(configuration.GetSection("SimpleServer"));
            services.AddSingleton<SimpleServer>();

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get the server
            var server = serviceProvider.GetRequiredService<SimpleServer>();

            // Start the server
            Console.WriteLine("Starting server...");

            // Start server in a separate task so we can handle console input
            var serverTask = server.StartAsync();

            Console.WriteLine("Server started. Press any key to stop...");
            Console.ReadKey();

            // Stop the server
            await server.StopAsync();
        }
    }
}
