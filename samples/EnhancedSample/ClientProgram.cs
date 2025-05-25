using System;
using System.Threading.Tasks;

namespace EnhancedSample
{
    /// <summary>
    /// Client program entry point for Enhanced MCP Sample
    /// </summary>
    public class ClientProgram
    {
        /// <summary>
        /// Main entry point for client demo
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Enhanced MCP Extensions Client Demo");
            Console.WriteLine("======================================");
            Console.WriteLine();

            // Parse command line arguments
            var demoType = args.Length > 0 ? args[0] : "all";
            
            // Display available demo types
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                DisplayHelp();
                if (args.Length > 0) return;
                
                Console.Write("Enter demo type (or press Enter for 'all'): ");
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                {
                    demoType = input.Trim();
                }
            }

            try
            {
                Console.WriteLine($"🎯 Running demo: {demoType}");
                Console.WriteLine();
                
                await ClientExample.RunAsync(demoType);
                
                Console.WriteLine();
                Console.WriteLine("✅ Demo completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ Demo failed with error:");
                Console.WriteLine($"   {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                
                // Show stack trace in debug mode
                if (args.Length > 0 && (args[^1] == "--debug" || args[^1] == "-d"))
                {
                    Console.WriteLine();
                    Console.WriteLine("Stack trace:");
                    Console.WriteLine(ex.StackTrace);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Displays help information
        /// </summary>
        private static void DisplayHelp()
        {
            Console.WriteLine("Enhanced MCP Extensions Client Demo");
            Console.WriteLine();
            Console.WriteLine("Usage: ClientProgram [demo-type] [options]");
            Console.WriteLine();
            Console.WriteLine("Demo Types:");
            Console.WriteLine("  basic         - Basic MCP operations (ping, echo)");
            Console.WriteLine("  caching       - Cache warming and optimization demo");
            Console.WriteLine("  streaming     - Stream processing and real-time data");
            Console.WriteLine("  performance   - Performance testing and metrics");
            Console.WriteLine("  resilience    - Resilience patterns (retry, circuit breaker)");
            Console.WriteLine("  security      - Security features (auth, validation)");
            Console.WriteLine("  observability - Advanced observability and anomaly detection");
            Console.WriteLine("  all           - Run all demos (default)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --debug, -d   - Show detailed error information");
            Console.WriteLine("  --help, -h    - Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run --project ClientProgram caching");
            Console.WriteLine("  dotnet run --project ClientProgram performance --debug");
            Console.WriteLine("  dotnet run --project ClientProgram all");
            Console.WriteLine();
            Console.WriteLine("Prerequisites:");
            Console.WriteLine("  1. Start the Enhanced MCP Server first:");
            Console.WriteLine("     dotnet run --project EnhancedSample");
            Console.WriteLine("  2. Ensure the server is running on ws://localhost:8080/ws");
            Console.WriteLine();
            Console.WriteLine("Features Demonstrated:");
            Console.WriteLine("  🔧 Basic Operations    - Ping, echo, server capabilities");
            Console.WriteLine("  💾 Advanced Caching    - Cache warming, analytics, optimization");
            Console.WriteLine("  🌊 Stream Processing   - Real-time data streams, LLM generation");
            Console.WriteLine("  ⚡ Performance         - Concurrent requests, metrics, throughput");
            Console.WriteLine("  🛡️ Resilience         - Retry policies, circuit breakers, timeouts");
            Console.WriteLine("  🔒 Security           - Authentication, authorization, validation");
            Console.WriteLine("  📊 Observability      - Telemetry, health checks, distributed tracing");
            Console.WriteLine();
        }
    }
}
