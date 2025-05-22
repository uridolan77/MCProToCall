# MCProToCall - Secure MCP Implementation

## Overview

MCProToCall is a robust and secure C# implementation of the Model Context Protocol (MCP), designed to facilitate seamless and secure integration between AI models and various application tools or data sources. This project includes both server and client implementations, adhering to JSON-RPC 2.0 standards and incorporating best security practices.

## Features

### Core Functionality
- **MCP Server**: A full-featured server that exposes capabilities to MCP clients, allowing for method registration and handling of requests.
- **MCP Client**: A client that communicates with MCP servers, enabling method calls and retrieval of server capabilities.
- **Extensible Architecture**: The project is designed with extensibility in mind, allowing for easy addition of new features and capabilities.

### Security Enhancements
- **TLS Support**: Secure communication with certificate-based encryption.
- **Authentication**: JWT-based authentication with secure token handling.
- **Authorization**: Role-based access control for MCP operations.
- **Input Validation**: Schema-based validation of MCP requests.
- **Rate Limiting**: Protection against abuse and DoS attacks.
- **Secure Logging**: Comprehensive, structured logging with sensitive data protection.

## Getting Started

### Prerequisites

- .NET SDK (version 6.0 or later)
- A code editor (e.g., Visual Studio, Visual Studio Code)

### Installation

1. Clone the repository:
   ```
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```
   cd MCProToCall
   ```
3. Restore the dependencies:
   ```
   dotnet restore
   ```

### Configuration

#### Server Configuration

Configure the MCP server in `appsettings.json`:

```json
{
  "McpServer": {
    "Host": "127.0.0.1",
    "Port": 8080,
    "UseTls": false,
    "CertificatePath": "",
    "CertificatePassword": "",
    "EnableAuthentication": true,
    "JwtAuth": {
      "SecretKey": "YourSecretKeyHere_MakeThisReallyLongAndSecureInProduction",
      "Issuer": "MCProToCall",
      "Audience": "MCProToCallClients",
      "AccessTokenExpirationMinutes": 15,
      "RefreshTokenExpirationDays": 7
    },
    "Validation": {
      "MaxRequestSize": 1048576,
      "StrictSchemaValidation": true
    },
    "RateLimit": {
      "Enabled": true,
      "RequestsPerMinute": 60,
      "RequestsPerDay": 1000
    }
  }
}
```

#### Client Configuration

Configure the MCP client in `appsettings.json`:

```json
{
  "McpClient": {
    "Host": "127.0.0.1",
    "Port": 8080,
    "Timeout": "00:00:30",
    "AuthToken": "",
    "UseTls": false,
    "EnableRetry": true,
    "MaxRetries": 3,
    "AutoRefreshToken": true
  }
}
```

### Running the Sample Applications

#### Basic MCP Server

1. Navigate to the `samples/BasicServer` directory:
   ```
   cd samples/BasicServer
   ```
2. Run the server:
   ```
   dotnet run
   ```

#### Basic MCP Client

1. Navigate to the `samples/BasicClient` directory:
   ```
   cd samples/BasicClient
   ```
2. Run the client:
   ```
   dotnet run
   ```

## Security Best Practices

### TLS Configuration

For production environments, always enable TLS:

```json
"UseTls": true,
"CertificatePath": "/path/to/certificate.pfx",
"CertificatePassword": "securePassword"
```

### JWT Authentication

1. Use a strong, random secret key (at least 32 characters)
2. Keep access token lifetimes short (15 minutes recommended)
3. Implement refresh token rotation
4. Store refresh tokens securely

### Authorization

Define granular permissions for your MCP methods:

```csharp
// Public method - no auth required
authMiddleware.RegisterPublicMethod("system.getCapabilities");

// User-level method
authMiddleware.RegisterMethodPermission("system.echo", new[] { "User" });

// Admin-only method
authMiddleware.RegisterMethodPermission("system.admin", new[] { "Admin" });
```

### Input Validation

Register JSON schema validation for MCP methods:

```csharp
var validator = serviceProvider.GetRequiredService<InputValidator>();
validator.RegisterMethodSchema("system.echo", @"{
  ""type"": ""string"",
  ""minLength"": 1,
  ""maxLength"": 1000
}");
```

## API Documentation

### Server API

```csharp
// Register a method
server.RegisterMethod("method.name", async parameters => {
    // Handle method
    return result;
});

// Register a public method (no auth required)
server.RegisterPublicMethod("public.method", async parameters => {
    // Handle method
    return result;
});

// Register a method with specific roles
server.RegisterSecuredMethod("admin.method", async parameters => {
    // Handle method
    return result;
}, new[] { "Admin" });
```

### Client API

```csharp
// Get server capabilities
var capabilities = await client.GetCapabilitiesAsync();

// Call a method
var result = await client.CallMethodAsync<ResultType>("method.name", parameters);

// Execute a tool
var toolResult = await client.ExecuteToolAsync<ResultType>("toolId", input);

// Get a resource
var resource = await client.GetResourceAsync<ResourceType>("resourceId");

// Render a prompt
var renderedPrompt = await client.RenderPromptAsync("promptId", variables);
```

## Architecture

The MCProToCall project is built with a modular architecture:

- **Core**: Core interfaces, models, and exceptions
- **Server**: The MCP server implementation
- **Client**: The MCP client implementation
- **Extensions**: Security, validation, and dependency injection extensions

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

1. Navigate to the `samples/BasicClient` directory:
   ```
   cd samples/BasicClient
   ```
2. Run the client:
   ```
   dotnet run
   ```

## Contributing

Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.