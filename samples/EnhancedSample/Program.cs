using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Streaming;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ModelContextProtocol.Server.Transports;

namespace EnhancedSample
{
    /// <summary>
    /// Sample application demonstrating enhanced MCP features
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure enhanced logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            // Validate configuration at startup
            try
            {
                StartupConfigurationValidator.ValidateConfiguration(
                    builder.Configuration,
                    builder.Logging.CreateLogger("Startup"));
            }
            catch (ConfigurationException ex)
            {
                Console.WriteLine($"Configuration error: {ex.Message}");
                return;
            }

            // Configure services
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Configure middleware pipeline
            ConfigureMiddleware(app);

            // Start the application
            await app.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add enhanced MCP server with all features
            services.AddEnhancedMcpServer(configuration);

            // Add CORS for browser-based clients
            services.AddCors(options =>
            {
                options.AddPolicy("McpCors", builder =>
                {
                    builder
                        .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>())
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            // Enable CORS
            app.UseCors("McpCors");

            // Add observability endpoints
            app.MapHealthChecks("/health");
            app.MapGet("/metrics", async context =>
            {
                // Export Prometheus metrics
                await context.Response.WriteAsync("# Metrics endpoint");
            });

            // Map WebSocket endpoint
            app.UseWebSockets();
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var wsServer = context.RequestServices.GetRequiredService<WebSocketMcpServer>();
                    await wsServer.HandleWebSocketAsync(webSocket, context.RequestAborted);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });

            // Start the MCP server
            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                var server = app.Services.GetRequiredService<IMcpServer>();
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                
                logger.LogInformation("Starting enhanced MCP server...");
                await server.StartAsync();

                // Register sample methods
                RegisterSampleMethods(server, app.Services);
            });

            app.Lifetime.ApplicationStopping.Register(async () =>
            {
                var server = app.Services.GetRequiredService<IMcpServer>();
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                
                logger.LogInformation("Stopping enhanced MCP server...");
                await server.StopAsync();
            });
        }

        private static void RegisterSampleMethods(IMcpServer server, IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            var streamingManager = services.GetRequiredService<StreamingResponseManager>();

            // Register LLM streaming method
            server.RegisterStreamingMethod("llm.generate", async (parameters, cancellationToken) =>
            {
                var prompt = parameters.GetProperty("prompt").GetString();
                var maxTokens = 100;
                
                if (parameters.TryGetProperty("maxTokens", out var maxTokensElement))
                {
                    maxTokens = maxTokensElement.GetInt32();
                }

                logger.LogInformation("Generating text for prompt: {Prompt}", 
                    prompt?.Substring(0, Math.Min(50, prompt.Length)) + "...");

                // Simulate LLM token generation
                var tokens = new[] 
                { 
                    "The", "quick", "brown", "fox", "jumps", 
                    "over", "the", "lazy", "dog", ".", 
                    "This", "is", "a", "sample", "text", 
                    "generated", "by", "the", "enhanced", "MCP", 
                    "server", "with", "streaming", "support", "."
                };

                foreach (var token in tokens)
                {
                    if (cancellationToken.IsCancellationRequested)
                        yield break;

                    // Simulate processing time
                    await Task.Delay(100, cancellationToken);

                    yield return new
                    {
                        token = token,
                        timestamp = DateTime.UtcNow,
                        confidence = 0.95
                    };
                }
            });

            // Register a method that demonstrates caching
            server.RegisterMethod("demo.cached", async parameters =>
            {
                // Simulate slow operation
                await Task.Delay(1000);
                
                return new
                {
                    message = "This response is cached for 5 minutes",
                    timestamp = DateTime.UtcNow
                };
            });

            logger.LogInformation("Registered sample methods");
        }
    }
}
