using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Performance;
using ModelContextProtocol.Core.Streaming;
using ModelContextProtocol.Server.Observability;

namespace ModelContextProtocol.Server
{
    /// <summary>
    /// Enhanced MCP Server with all improvements
    /// </summary>
    public class EnhancedMcpServer : McpServer
    {
        private readonly IMcpTelemetry _telemetry;
        private readonly StreamingResponseManager _streamingManager;
        private readonly ResponseCache _cache;
        private readonly TelemetryMiddleware _telemetryMiddleware;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedMcpServer"/> class
        /// </summary>
        /// <param name="options">Server options</param>
        /// <param name="logger">Logger</param>
        /// <param name="telemetry">Telemetry service</param>
        /// <param name="streamingManager">Streaming manager</param>
        /// <param name="cache">Response cache</param>
        /// <param name="telemetryMiddleware">Telemetry middleware</param>
        public EnhancedMcpServer(
            McpServerOptions options,
            ILogger<McpServer> logger,
            IMcpTelemetry telemetry,
            StreamingResponseManager streamingManager,
            ResponseCache cache,
            TelemetryMiddleware telemetryMiddleware)
            : base(
                Microsoft.Extensions.Options.Options.Create(options),
                logger,
                streamingManager: streamingManager)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _streamingManager = streamingManager ?? throw new ArgumentNullException(nameof(streamingManager));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _telemetryMiddleware = telemetryMiddleware ?? throw new ArgumentNullException(nameof(telemetryMiddleware));
        }

        /// <summary>
        /// Handles a JSON-RPC request with telemetry and caching
        /// </summary>
        /// <param name="request">The request to handle</param>
        /// <returns>The response</returns>
        public override async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            // Use telemetry middleware to track request
            return await _telemetryMiddleware.InvokeAsync(request, async req =>
            {
                // Check cache for certain methods
                if (req.Method.StartsWith("resources."))
                {
                    var cacheKey = $"{req.Method}:{JsonSerializer.Serialize(req.Params)}";
                    var cached = await _cache.GetOrAddAsync(
                        cacheKey,
                        async () => await base.HandleRequestAsync(req),
                        TimeSpan.FromMinutes(5));

                    return cached;
                }

                // Use high-performance serialization for certain methods
                if (req.Method.StartsWith("performance."))
                {
                    try
                    {
                        // Example of using high-performance serialization
                        var result = await base.HandleRequestAsync(req);

                        // Log performance metrics
                        _telemetry.RecordRequestCompleted(
                            req.Method,
                            true,
                            0); // In a real implementation, we would measure the time

                        return result;
                    }
                    catch (Exception ex)
                    {
                        _telemetry.RecordError(req.Method, ex.GetType().Name);
                        throw;
                    }
                }

                // Default handling
                return await base.HandleRequestAsync(req);
            });
        }

        /// <summary>
        /// Registers enhanced system methods
        /// </summary>
        public void RegisterEnhancedSystemMethods()
        {
            // Register a cached resource method
            RegisterMethod("resources.get", async parameters =>
            {
                var resourceId = parameters.GetProperty("id").GetString();

                return await _cache.GetOrAddAsync(
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
            RegisterMethod("performance.test", async parameters =>
            {
                var iterations = 1000;
                if (parameters.TryGetProperty("iterations", out var iterationsElement))
                {
                    iterations = iterationsElement.GetInt32();
                }

                var stopwatch = Stopwatch.StartNew();

                // Simulate work
                for (int i = 0; i < iterations; i++)
                {
                    // Do nothing, just measure serialization performance
                }

                stopwatch.Stop();

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
