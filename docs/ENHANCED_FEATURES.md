# Enhanced Features Guide

This document provides detailed information about the enhanced features added to the MCProToCall implementation.

## WebSocket Transport

The WebSocket transport provides real-time bidirectional communication with lower latency than HTTP.

### Key Components

- **WebSocketTransport**: Implements the `ITransport` interface for WebSocket communication
- **WebSocketMcpServer**: Handles WebSocket connections and routes messages to the MCP server

### Configuration

```json
"WebSocket": {
  "Enabled": true,
  "KeepAliveInterval": 30,
  "ReceiveBufferSize": 4096,
  "MaxMessageSize": 1048576
}
```

### Usage

Server-side:
```csharp
// In Program.cs
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
```

Client-side:
```csharp
using var client = new StreamingClient("ws://localhost:8080/ws");
await client.ConnectAsync();
```

## Streaming Support

The streaming support allows token-by-token streaming for LLM responses with backpressure and cancellation support.

### Key Components

- **StreamingResponseManager**: Manages streaming responses and handles backpressure
- **JsonRpcStreamNotification**: JSON-RPC notification for streaming data
- **StreamConsumer**: Client-side stream consumer

### Configuration

```json
"Streaming": {
  "MaxConcurrentStreams": 100,
  "StreamTimeout": 300,
  "ChunkSize": 4096,
  "EnableCompression": true
}
```

### Usage

Server-side:
```csharp
server.RegisterStreamingMethod("llm.generate", async (parameters, cancellationToken) =>
{
    var prompt = parameters.GetProperty("prompt").GetString();
    
    // Stream tokens
    foreach (var token in GenerateTokens(prompt))
    {
        if (cancellationToken.IsCancellationRequested)
            yield break;
            
        yield return new { token, timestamp = DateTime.UtcNow };
    }
});
```

Client-side:
```csharp
var streamId = await client.CallStreamingMethodAsync("llm.generate", new 
{ 
    prompt = "Tell me a story", 
    maxTokens = 100 
});

await foreach (var item in client.ConsumeStreamAsync<TokenResponse>(streamId))
{
    Console.Write(item.Token);
}
```

## OpenTelemetry Integration

The OpenTelemetry integration provides comprehensive observability with metrics, tracing, and structured logging.

### Key Components

- **McpTelemetry**: Implements the `IMcpTelemetry` interface for metrics and tracing
- **TelemetryMiddleware**: Middleware for automatic telemetry collection
- **McpHealthCheck**: Health check service for MCP

### Configuration

```json
"OpenTelemetry": {
  "Endpoint": "http://localhost:4317",
  "ServiceName": "MCP-Enhanced-Server",
  "EnableTracing": true,
  "EnableMetrics": true,
  "EnableLogging": true
}
```

### Usage

```csharp
// In Program.cs
services.AddMcpObservability(configuration, "MCP-Enhanced-Server");

// In your code
public class MyService
{
    private readonly IMcpTelemetry _telemetry;
    
    public MyService(IMcpTelemetry telemetry)
    {
        _telemetry = telemetry;
    }
    
    public async Task DoWorkAsync()
    {
        using var activity = _telemetry.StartActivity("my-operation");
        
        _telemetry.RecordRequestReceived("my-method");
        
        try
        {
            // Do work
            _telemetry.RecordRequestCompleted("my-method", true, 100);
        }
        catch (Exception ex)
        {
            _telemetry.RecordError("my-method", ex.GetType().Name);
            throw;
        }
    }
}
```

## Performance Optimizations

The performance optimizations include source generators for JSON serialization, object pooling, and buffer management.

### Key Components

- **McpJsonContext**: JSON serialization context for source generation
- **HighPerformanceJsonSerializer**: High-performance JSON serializer using source generators
- **McpObjectPoolProvider**: Object pool for reducing allocations
- **BufferPool**: Buffer pool for reducing byte array allocations
- **ResponseCache**: Response cache for frequently accessed resources
- **McpConnectionPool**: Connection pool for HTTP clients

### Configuration

```json
"Performance": {
  "UseSourceGenerators": true,
  "EnableObjectPooling": true,
  "EnableResponseCaching": true,
  "CacheDurationMinutes": 5,
  "ConnectionPoolSize": 50,
  "ConnectionLifetimeMinutes": 30,
  "BufferPoolMaxSize": 10485760
}
```

### Usage

```csharp
// Using high-performance serialization
var json = HighPerformanceJsonSerializer.Serialize(myObject);
var obj = HighPerformanceJsonSerializer.Deserialize<MyType>(json);

// Using object pooling
var poolProvider = new McpObjectPoolProvider();
var pool = poolProvider.GetPool<MyObject>();
var obj = pool.Get();
try
{
    // Use obj
}
finally
{
    pool.Return(obj);
}

// Using buffer pooling
var buffer = BufferPool.Rent(1024);
try
{
    // Use buffer
}
finally
{
    BufferPool.Return(buffer, clearArray: true);
}

// Using response caching
var cache = new ResponseCache(TimeSpan.FromMinutes(5));
var result = await cache.GetOrAddAsync(
    "my-key",
    async () => await FetchDataAsync(),
    TimeSpan.FromMinutes(10));
```

## Configuration Validation

The configuration validation provides a comprehensive validation system with clear error messages.

### Key Components

- **ValidationAttributes**: Custom validation attributes for configuration properties
- **ConfigurationValidator**: Configuration validator service
- **ValidatedMcpServerOptions**: Enhanced server options with validation
- **ValidatingOptionsSetup**: Options setup with validation
- **ConfigurationValidationExtensions**: Extension methods for configuration validation

### Usage

```csharp
// In Program.cs
services.AddValidatedOptions<ValidatedMcpServerOptions>(configuration, "McpServer");

// In your code
public class MyOptions
{
    [Required]
    [ValidHost]
    public string Host { get; set; }
    
    [ValidPort]
    public int Port { get; set; }
    
    [FileExists]
    public string CertificatePath { get; set; }
    
    [DirectoryExists(CreateIfMissing = true)]
    public string LogDirectory { get; set; }
}
```

## Enhanced Application Structure

The enhanced application structure provides a production-ready application example with all features integrated.

### Key Components

- **EnhancedMcpServer**: Enhanced MCP server with all improvements
- **McpEnhancedExtensions**: Extension methods for configuring enhanced MCP services
- **Program.cs**: ASP.NET Core application with all features integrated

### Usage

```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);

// Validate configuration at startup
StartupConfigurationValidator.ValidateConfiguration(
    builder.Configuration,
    builder.Logging.CreateLogger("Startup"));

// Add enhanced MCP server with all features
services.AddEnhancedMcpServer(builder.Configuration);

var app = builder.Build();

// Configure middleware pipeline
app.UseCors("McpCors");
app.MapHealthChecks("/health");
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

await app.RunAsync();
```
