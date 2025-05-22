using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Performance;
using ModelContextProtocol.Core.Streaming;
using ModelContextProtocol.Extensions.Configuration;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Server;
using ModelContextProtocol.Server.Transports;
using System;
using System.Threading.Tasks;

namespace ModelContextProtocol.EnhancedExample
{
    public class Program
    {
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
            // Add OpenTelemetry observability
            services.AddMcpObservability(configuration, "MCP-Enhanced-Server");

            // Add health checks
            services.AddHealthChecks()
                .AddCheck<McpHealthCheck>("mcp_server");

            // Add validated configuration
            services.AddValidatedOptions<ValidatedMcpServerOptions>(
                configuration, 
                "McpServer");

            // Add object pooling for performance
            services.AddSingleton<McpObjectPoolProvider>();
            services.AddSingleton<HighPerformanceRequestProcessor>();

            // Add response caching
            services.AddSingleton(new ResponseCache(TimeSpan.FromMinutes(5)));

            // Add connection pooling
            services.AddSingleton(new McpConnectionPool(TimeSpan.FromMinutes(30)));

            // Add streaming support
            services.AddSingleton<StreamingResponseManager>();

            // Add security services if TLS is enabled
            if (configuration.GetValue<bool>("McpServer:UseTls"))
            {
                services.AddMcpTlsMiddleware();
            }

            // Add the enhanced MCP server
            services.AddSingleton<IMcpServer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ValidatedMcpServerOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                var telemetry = sp.GetRequiredService<IMcpTelemetry>();

                // Create server with all enhancements
                var server = new EnhancedMcpServer(
                    options,
                    logger,
                    telemetry,
                    sp.GetService<StreamingResponseManager>(),
                    sp.GetService<ResponseCache>(),
                    sp.GetService<HighPerformanceRequestProcessor>());

                // Register methods
                RegisterMethods(server, sp);

                return server;
            });

            // Add WebSocket support
            services.AddSingleton<WebSocketMcpServer>();

            // Add SignalR for real-time communication
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });

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
            app.MapGet("/ws", async context =>
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

            // Map SignalR hub
            app.MapHub<McpHub>("/mcphub");

            // Start the MCP server
            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                var server = app.Services.GetRequiredService<IMcpServer>();
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                
                logger.LogInformation("Starting enhanced MCP server...");
                await server.StartAsync();
            });

            app.Lifetime.ApplicationStopping.Register(async () =>
            {
                var server = app.Services.GetRequiredService<IMcpServer>();
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                
                logger.LogInformation("Stopping enhanced MCP server...");
                await server.StopAsync();
            });
        }

        private static void RegisterMethods(IMcpServer server, IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();

            // Register standard methods
            server.RegisterMethod("system.ping", async _ => 
            {
                return await Task.FromResult(new { pong = DateTime.UtcNow });
            });

            // Register streaming method for LLM
            server.RegisterMethod("llm.generate", async parameters =>
            {
                var streamingManager = services.GetRequiredService<StreamingResponseManager>();
                var llmMethod = new LlmStreamingMethod(services.GetRequiredService<ILogger<LlmStreamingMethod>>());
                
                var dataStream = llmMethod.ExecuteStreamingAsync(parameters, CancellationToken.None);
                var streamId = await streamingManager.StartStreamAsync(
                    Guid.NewGuid().ToString(),
                    dataStream,
                    async notification =>
                    {
                        // Send notification through appropriate transport
                        logger.LogDebug("Streaming notification: {StreamId}", notification.Params.StreamId);
                    },
                    CancellationToken.None);

                return new { streamId, message = "Streaming started" };
            });

            // Register cached method
            server.RegisterMethod("resources.get", async parameters =>
            {
                var cache = services.GetRequiredService<ResponseCache>();
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
        }
    }

    /// <summary>
    /// Enhanced MCP Server with all improvements
    /// </summary>
    public class EnhancedMcpServer : McpServer
    {
        private readonly IMcpTelemetry _telemetry;
        private readonly StreamingResponseManager _streamingManager;
        private readonly ResponseCache _cache;
        private readonly HighPerformanceRequestProcessor _processor;

        public EnhancedMcpServer(
            McpServerOptions options,
            ILogger<McpServer> logger,
            IMcpTelemetry telemetry,
            StreamingResponseManager streamingManager,
            ResponseCache cache,
            HighPerformanceRequestProcessor processor)
            : base(options, logger)
        {
            _telemetry = telemetry;
            _streamingManager = streamingManager;
            _cache = cache;
            _processor = processor;
        }

        public override async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            // Add telemetry
            using var activity = _telemetry.StartActivity($"mcp.request.{request.Method}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Check cache for certain methods
                if (request.Method.StartsWith("resources."))
                {
                    var cacheKey = $"{request.Method}:{JsonSerializer.Serialize(request.Params)}";
                    var cached = await _cache.GetOrAddAsync(
                        cacheKey,
                        async () => await base.HandleRequestAsync(request),
                        TimeSpan.FromMinutes(5));
                    
                    if (cached != null)
                    {
                        _telemetry.RecordRequestCompleted(request.Method, true, stopwatch.ElapsedMilliseconds);
                        return (JsonRpcResponse)cached;
                    }
                }

                var response = await base.HandleRequestAsync(request);
                
                _telemetry.RecordRequestCompleted(
                    request.Method, 
                    response is not JsonRpcErrorResponse, 
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                _telemetry.RecordError(request.Method, ex.GetType().Name);
                throw;
            }
        }
    }

    /// <summary>
    /// SignalR hub for real-time MCP communication
    /// </summary>
    public class McpHub : Hub
    {
        private readonly IMcpServer _server;
        private readonly IMcpTelemetry _telemetry;
        private readonly ILogger<McpHub> _logger;

        public McpHub(IMcpServer server, IMcpTelemetry telemetry, ILogger<McpHub> logger)
        {
            _server = server;
            _telemetry = telemetry;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _telemetry.RecordConnectionEvent("connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _telemetry.RecordConnectionEvent("disconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<string> InvokeMethod(string method, object parameters)
        {
            var request = new JsonRpcRequest
            {
                Id = Guid.NewGuid().ToString(),
                Method = method,
                Params = JsonDocument.Parse(JsonSerializer.Serialize(parameters)).RootElement
            };

            var response = await _server.HandleRequestAsync(request);
            return JsonSerializer.Serialize(response);
        }

        public async Task SubscribeToStream(string streamId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"stream:{streamId}");
            _logger.LogInformation("Client {ConnectionId} subscribed to stream {StreamId}", 
                Context.ConnectionId, streamId);
        }

        public async Task UnsubscribeFromStream(string streamId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream:{streamId}");
            _logger.LogInformation("Client {ConnectionId} unsubscribed from stream {StreamId}", 
                Context.ConnectionId, streamId);
        }
    }
}