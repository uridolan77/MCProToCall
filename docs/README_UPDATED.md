# Model Context Protocol (MCP) Implementation in C#

This repository contains a robust C# implementation of the Model Context Protocol (MCP), designed for secure and reliable communication between clients and AI model servers.

## Features

- **Core JSON-RPC Implementation**: Based on the JSON-RPC 2.0 specification
- **Extensible Architecture**: Easily extend with new methods and capabilities
- **Dependency Injection**: Modern DI-based architecture for easy integration
- **Comprehensive Security Features**:
  - TLS/HTTPS support with client certificate authentication
  - JWT-based authentication
  - Role-based authorization
  - Input validation
  - Rate limiting

## Getting Started

### Server Setup

```csharp
// Configure and start MCP server
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Add secure MCP server with all security features
        services.AddMcpSecureServer(hostContext.Configuration);
    })
    .Build();

// Register methods
var server = host.Services.GetRequiredService<IMcpServer>();
((McpServer)server).RegisterMethod("example.method", async (parameters) =>
{
    return "Hello, MCP!";
});

// Start the server
await server.StartAsync();

Console.WriteLine("MCP Server is running. Press any key to exit...");
Console.ReadKey();

// Stop the server
((McpServer)server).Stop();
```

### Client Usage

```csharp
// Configure MCP client
var services = new ServiceCollection();
services.AddSecureMcpClient(configuration); // Adds client with TLS support
var serviceProvider = services.BuildServiceProvider();
var client = serviceProvider.GetRequiredService<IMcpClient>();

// Call method
var result = await client.CallMethodAsync<string>("example.method");
Console.WriteLine($"Result: {result}");
```

## Security Features

### TLS Configuration

The implementation provides comprehensive TLS support:

1. **Server-side TLS**: Configure the server to use TLS for all communications
2. **Client Certificate Authentication**: Validate client certificates for mutual TLS
3. **Certificate Management**: Utilities for working with certificates
4. **Connection Rate Limiting**: Limit connections per client to prevent DoS attacks

Configuration in `appsettings.json`:

```json
{
  "McpServer": {
    "UseTls": true,
    "Tls": {
      "CertificatePath": "./server.pfx",
      "CertificatePassword": "yourpassword",
      "RequireClientCertificate": true,
      "AllowedClientCertificateThumbprints": [
        "THUMBPRINT1", 
        "THUMBPRINT2"
      ],
      "CheckCertificateRevocation": true
    }
  }
}
```

### Certificate Generation

The library includes utilities for generating development certificates:

```csharp
// Generate certificates for development/testing
CertificateGenerator.GenerateDevelopmentCertificates("./certs", logger);
```

For production, use the TLS setup tool:

```powershell
TlsSetupTool.exe ./certs MyServer "Client1,Client2,Client3" --password="securePass" --validity=730
```

### Authentication & Authorization

The implementation includes JWT-based authentication and role-based authorization:

```csharp
// Register secured methods with role requirements
server.RegisterSecuredMethod("admin.method", handler, new[] { "Admin" });
server.RegisterMethodPermission("user.method", new[] { "User", "Admin" });
server.RegisterPublicMethod("public.method", handler); // No auth required
```

### Input Validation

Validate inputs against JSON schemas:

```csharp
var validator = serviceProvider.GetRequiredService<InputValidator>();
bool isValid = validator.ValidateInput(
    inputJson, 
    "{ \"type\": \"object\", \"properties\": { ... } }");
```

## More Information

For detailed TLS configuration, see [TLS_SECURITY.md](TLS_SECURITY.md).

## License

MIT
