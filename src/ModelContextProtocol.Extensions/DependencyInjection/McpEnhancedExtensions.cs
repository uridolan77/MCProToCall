using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Performance;
using ModelContextProtocol.Core.Streaming;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring enhanced MCP services
    /// </summary>
    public static class McpEnhancedExtensions
    {
        /// <summary>
        /// Adds the enhanced MCP server with all features
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddEnhancedMcpServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add validated configuration
            services.AddValidatedOptions<ValidatedMcpServerOptions>(configuration, "McpServer");

            // Add OpenTelemetry observability
            services.AddMcpObservability(configuration, "MCP-Enhanced-Server");

            // Add object pooling for performance
            services.AddSingleton<McpObjectPoolProvider>();

            // Add response caching
            services.AddSingleton(new ResponseCache(TimeSpan.FromMinutes(5)));

            // Add connection pooling
            services.AddSingleton<McpConnectionPool>();

            // Add streaming support
            services.AddSingleton<StreamingResponseManager>();

            // Add security services if TLS is enabled
            if (configuration.GetValue<bool>("McpServer:UseTls"))
            {
                AddMcpTlsMiddleware(services);
            }

            // Add authentication if enabled
            if (configuration.GetValue<bool>("McpServer:EnableAuthentication"))
            {
                AddMcpAuthentication(services, configuration);
            }

            // Add the enhanced MCP server
            services.AddSingleton<IMcpServer>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ValidatedMcpServerOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                var telemetry = sp.GetRequiredService<IMcpTelemetry>();
                var streamingManager = sp.GetRequiredService<StreamingResponseManager>();
                var cache = sp.GetRequiredService<ResponseCache>();
                var telemetryMiddleware = sp.GetRequiredService<TelemetryMiddleware>();

                // Create enhanced server
                var server = new McpServer(
                    Microsoft.Extensions.Options.Options.Create(options),
                    logger);

                // Register enhanced methods
                RegisterEnhancedSystemMethods(server, cache, telemetry);

                return server;
            });

            // Add WebSocket support
            // WebSocketMcpServer will be registered by the Server project

            return services;
        }

        /// <summary>
        /// Adds MCP authentication services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration</param>
        /// <returns>Service collection for chaining</returns>
        private static IServiceCollection AddMcpAuthentication(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // Add JWT options
            services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

            return services;
        }

        /// <summary>
        /// Adds MCP TLS middleware
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        private static IServiceCollection AddMcpTlsMiddleware(
            IServiceCollection services)
        {
            // Configure TLS options
            services.Configure<McpServerOptions>(options =>
            {
                options.UseTls = true;
                options.CheckCertificateRevocation = true;
            });

            return services;
        }

        /// <summary>
        /// Registers enhanced system methods on the MCP server
        /// </summary>
        /// <param name="server">MCP server</param>
        /// <param name="cache">Response cache</param>
        /// <param name="telemetry">Telemetry service</param>
        private static void RegisterEnhancedSystemMethods(
            IMcpServer server,
            ResponseCache cache,
            IMcpTelemetry telemetry)
        {
            // Register a cached resource method
            server.RegisterMethod("resources.get", async parameters =>
            {
                var resourceId = parameters.GetProperty("id").GetString();

                return await cache.GetOrAddAsync(
                    $"resource:{resourceId}",
                    async () =>
                    {
                        // Simulate resource fetch
                        await Task.Delay(100);
                        return new
                        {
                            id = resourceId,
                            data = "Resource content",
                            timestamp = DateTime.UtcNow
                        };
                    },
                    TimeSpan.FromMinutes(10));
            });

            // Register a performance test method
            server.RegisterMethod("performance.test", async parameters =>
            {
                var iterations = 1000;
                if (parameters.TryGetProperty("iterations", out var iterationsElement))
                {
                    iterations = iterationsElement.GetInt32();
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Simulate work
                for (int i = 0; i < iterations; i++)
                {
                    // Do nothing, just measure serialization performance
                }

                stopwatch.Stop();

                // Record telemetry
                telemetry.RecordRequestCompleted(
                    "performance.test",
                    true,
                    stopwatch.ElapsedMilliseconds);

                return new
                {
                    iterations,
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow
                };
            });
        }
    }
}
