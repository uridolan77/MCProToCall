# Secure TLS Implementation for Model Context Protocol (MCP)

This document outlines the security enhancements made to the Model Context Protocol implementation, including TLS configuration, certificate management, and mutual TLS authentication.

## TLS Security Features

The MCP implementation now includes the following TLS security features:

1. **Server-side TLS support** - Configure the server to use TLS 1.2/1.3 for secure communications
2. **Client-side TLS support** - Configure clients to validate server certificates
3. **Mutual TLS authentication** - Support for client certificates to authenticate clients to servers
4. **Certificate Validation** - Comprehensive certificate validation for both client and server certificates
5. **Certificate Revocation Checking** - Verify certificates against revocation lists (CRL) and OCSP
6. **Certificate Pinning** - Enhance security by pinning trusted certificates or public keys
7. **Connection Rate Limiting** - Prevent DoS attacks by limiting connection rates per client

## Server Configuration

### Basic TLS Configuration

To configure your server with TLS, update your `appsettings.json`:

```json
{
  "McpServer": {
    "Host": "127.0.0.1",
    "Port": 8443,
    "UseTls": true,
    "Tls": {
      "CertificatePath": "./server.pfx",
      "CertificatePassword": "your-secure-password",
      "RequireClientCertificate": false
    }
  }
}
```

### Advanced TLS Configuration

For production environments, consider this more secure configuration:

```json
{
  "McpServer": {
    "UseTls": true,
    "Tls": {
      "CertificatePath": "./server.pfx",
      "CertificatePassword": "your-secure-password",
      "RequireClientCertificate": true,
      "AllowedClientCertificateThumbprints": ["thumbprint1", "thumbprint2"],
      "CheckCertificateRevocation": true,
      "RevocationCheckMode": "OcspAndCrl",
      "RevocationFailureMode": "Deny",
      "RevocationCachePath": "./certs/revocation",
      "CrlUpdateIntervalHours": 24,
      "AllowUntrustedCertificates": false,
      "UseCertificatePinning": true,
      "PinnedCertificates": ["./certs/trusted_clients/client1.cer"],
      "CertificatePinStoragePath": "./certs/pins",
      "RequireExactCertificateMatch": true,
      "AllowOnPinningFailure": false,
      "MaxConnectionsPerIpAddress": 50,
      "ConnectionRateLimitingWindowSeconds": 60
    }
  }
}
```

### Setting Up the Server in Code

```csharp
public static IServiceCollection ConfigureSecureServer(this IServiceCollection services, IConfiguration configuration)
{
    // Add MCP Server with all security features
    services.AddMcpSecureServer(configuration);
    
    // Or configure security manually:
    services.AddMcpServer(configuration)
            .AddMcpSecurity(configuration)
            .AddMcpTls(configuration);
    
    return services;
}
```

## Client Configuration

### Basic TLS Client Configuration

To configure your client with TLS, update your `appsettings.json`:

```json
{
  "McpClient": {
    "Host": "127.0.0.1",
    "Port": 8443,
    "UseTls": true,
    "AllowUntrustedServerCertificate": false,
    "ClientCertificatePath": "./client.pfx",
    "ClientCertificatePassword": "your-secure-password"
  }
}
```

### Advanced TLS Client Configuration

For production environments:

```json
{
  "McpClient": {
    "UseTls": true,
    "AllowUntrustedServerCertificate": false,
    "ClientCertificatePath": "./client.pfx",
    "ClientCertificatePassword": "your-secure-password",
    "ServerCertificatePinPath": "./certs/server.cer",
    "EnableCertificatePinning": true,
    "EnableRevocationCheck": true,
    "EnableDetailedTlsLogging": false
  }
}
```

### Setting Up the Client in Code

```csharp
public static IServiceCollection ConfigureSecureClient(this IServiceCollection services, IConfiguration configuration)
{
    // Add MCP Client with all security features
    services.AddSecureMcpClient(configuration);
    
    // Register certificate validation services for client
    services.AddSingleton<ICertificateValidator, CertificateValidator>();
    services.AddSingleton<ICertificatePinningService, CertificatePinningService>();
    
    return services;
}
```

## Certificate Management

### Generating Development Certificates

MCP includes utilities to generate self-signed certificates for development:

```csharp
// Generate a server certificate
var generator = new CertificateGenerator();
var serverCert = generator.GenerateSelfSignedCertificate(
    "CN=MCP Server", 
    DateTime.Now, 
    DateTime.Now.AddYears(1));

// Export with private key
File.WriteAllBytes("server.pfx", serverCert.Export(X509ContentType.Pfx, "password"));

// Export public certificate for client pinning
File.WriteAllBytes("server.cer", serverCert.Export(X509ContentType.Cert));
```

### Loading Certificates

```csharp
// From file
var certificate = CertificateHelper.LoadCertificateFromFile("path/to/cert.pfx", "password");

// From certificate store
var certificate = CertificateHelper.LoadCertificateFromStore("thumbprint", StoreName.My, StoreLocation.LocalMachine);
```

## Certificate Validation

### Configure Certificate Validation

The `CertificateValidator` class provides comprehensive certificate validation:

```csharp
// Register the validator
services.AddSingleton<ICertificateValidator, CertificateValidator>();

// Configure options
services.Configure<TlsOptions>(options => {
    options.CheckCertificateRevocation = true;
    options.UseCertificatePinning = true;
    options.RequireExactCertificateMatch = true;
});
```

### Custom Certificate Validation

You can implement custom validation logic:

```csharp
options.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => {
    // Custom validation logic
    if (certificate.Subject.Contains("MyTrustedCA"))
        return true;
    
    return errors == SslPolicyErrors.None;
};
```

## Certificate Revocation

### Configure Revocation Checking

MCP supports multiple revocation checking modes:

```csharp
services.Configure<TlsOptions>(options => {
    options.CheckCertificateRevocation = true;
    options.RevocationCheckMode = RevocationCheckMode.OcspAndCrl;
    options.RevocationFailureMode = RevocationFailureMode.Deny;
    options.RevocationCachePath = "./certs/revocation";
    options.CrlUpdateIntervalHours = 24;
});
```

### Add to Revocation List

Manually add certificates to the revocation list:

```csharp
var revocationChecker = serviceProvider.GetRequiredService<ICertificateRevocationChecker>();
revocationChecker.AddToRevocationList(certificateToRevoke);
```

## Certificate Pinning

### Configure Certificate Pinning

Certificate pinning enhances security by restricting trusted certificates:

```csharp
services.Configure<TlsOptions>(options => {
    options.UseCertificatePinning = true;
    options.PinnedCertificates = new List<string> { "./certs/trusted.cer" };
    options.CertificatePinStoragePath = "./certs/pins";
    options.RequireExactCertificateMatch = true;
});
```

### Add Pinned Certificates

```csharp
var pinningService = serviceProvider.GetRequiredService<ICertificatePinningService>();
pinningService.AddCertificatePin(trustedCertificate, isPermanent: true);
```

## Connection Rate Limiting

### Configure Rate Limiting

Prevent denial-of-service attacks with connection rate limiting:

```csharp
services.Configure<TlsOptions>(options => {
    options.MaxConnectionsPerIpAddress = 50;
    options.ConnectionRateLimitingWindowSeconds = 60;
});
```

### Monitoring Connections

```csharp
var connectionManager = serviceProvider.GetRequiredService<TlsConnectionManager>();
int activeConnections = connectionManager.GetActiveConnectionCount(ipAddress);
```

## Security Best Practices

1. **Use Strong Certificates**: In production, use certificates from trusted CAs
2. **Enable Mutual TLS**: Require client certificates for sensitive APIs
3. **Check Revocation**: Always enable certificate revocation checks
4. **Use Certificate Pinning**: Pin certificates to prevent MITM attacks
5. **Rotate Certificates**: Regularly rotate certificates before expiration
6. **Use Rate Limiting**: Prevent DoS attacks with connection rate limits
7. **Secure Private Keys**: Store private keys securely, consider HSMs
8. **Audit Logs**: Monitor TLS connections for suspicious activity
9. **Stay Updated**: Regularly update TLS libraries for security patches
10. **Disable Old Protocols**: Only enable TLS 1.2 and 1.3 in production

## Troubleshooting

### Common Issues

1. **Certificate validation failures**: Check expiration dates and trust chains
2. **Handshake failures**: Verify protocol compatibility and cipher suites
3. **Connection timeouts**: Check firewall settings and network connectivity
4. **Revocation checking failures**: Ensure CRL endpoints are accessible
5. **Client certificate issues**: Verify client certificates are signed by trusted CAs

### Debugging TLS

Enable detailed TLS logging:

```csharp
options.EnableDetailedTlsLogging = true;
```

## Appendix: TLS Configuration Reference

### TlsOptions Properties

| Property | Type | Description |
|----------|------|-------------|
| CertificatePath | string | Path to the server certificate file |
| CertificatePassword | string | Password for the server certificate |
| CertificateThumbprint | string | Thumbprint of the certificate in the store |
| RequireClientCertificate | bool | Whether to require client certificates |
| AllowedClientCertificateThumbprints | List<string> | List of allowed client thumbprints |
| CheckCertificateRevocation | bool | Whether to check certificate revocation |
| RevocationCheckMode | enum | Mode for revocation checking |
| RevocationFailureMode | enum | How to handle revocation check failures |
| RevocationCachePath | string | Path to store revocation cache |
| CrlUpdateIntervalHours | int | How often to update CRL cache |
| AllowUntrustedCertificates | bool | Allow untrusted certs (dev only) |
| UseCertificatePinning | bool | Enable certificate pinning |
| PinnedCertificates | List<string> | List of pinned certificate paths |
| CertificatePinStoragePath | string | Path to store pin information |
| RequireExactCertificateMatch | bool | Require exact cert matches for pinning |
| AllowOnPinningFailure | bool | How to handle pinning failures |
| MaxConnectionsPerIpAddress | int | Max connections per client IP |
| ConnectionRateLimitingWindowSeconds | int | Time window for rate limiting |
4. **Certificate validation** - Robust certificate validation with customizable validation rules
5. **Certificate management** - Utilities for loading certificates from files or certificate stores
6. **Development tools** - Helper utilities for generating self-signed certificates for development

## Server Configuration

### Basic TLS Configuration

To enable TLS on the server, update your `appsettings.json`:

```json
{
  "McpServer": {
    "UseTls": true,
    "Tls": {
      "CertificatePath": "./server.pfx",
      "CertificatePassword": "yourpassword",
      "RequireClientCertificate": false,
      "CheckCertificateRevocation": true
    }
  }
}
```

### Mutual TLS Configuration

To require client certificates for mutual TLS:

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

### Server Registration

Register the secure server with all security features in one go:

```csharp
services.AddMcpSecureServer(configuration);
```

## Client Configuration

### Basic TLS Configuration

To enable TLS on the client, update your `appsettings.json`:

```json
{
  "McpClient": {
    "UseTls": true,
    "AllowUntrustedServerCertificate": false
  }
}
```

### Mutual TLS Configuration

To use client certificates for mutual TLS:

```json
{
  "McpClient": {
    "UseTls": true,
    "ClientCertificatePath": "./client.pfx",
    "ClientCertificatePassword": "yourpassword"
  }
}
```

### Client Registration

Register the secure client with all security features:

```csharp
services.AddSecureMcpClient(configuration);
```

## Certificate Generation for Development

Use the provided certificate generator utility to create self-signed certificates for development:

```csharp
using ModelContextProtocol.Extensions.Utilities;
using Microsoft.Extensions.Logging;

// Set up logger
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<Program>();

// Generate certificates
CertificateGenerator.GenerateDevelopmentCertificates("./certs", logger);
```

This will generate:
- A server certificate: `./certs/server.pfx`
- A client certificate: `./certs/client.pfx`
- Both with password: `password`

## Best Practices

1. **Use TLS 1.2 or 1.3** - Avoid older TLS/SSL protocols which have known vulnerabilities
2. **Validate certificates properly** - Always validate server certificates in production
3. **Use strong certificates** - For production, use certificates from trusted certificate authorities
4. **Use secure certificate storage** - Store certificate passwords securely, not in plaintext
5. **Implement certificate pinning** - For high-security scenarios, implement certificate pinning
6. **Rotate certificates** - Regularly rotate certificates and have a certificate renewal process
7. **Monitor certificate expiration** - Set up alerts for certificate expiration

## Implementation Details

The TLS implementation includes:

- `TlsExtensions.cs` - Core TLS configuration and validation methods
- `CertificateHelper.cs` - Certificate loading and management utilities
- `CertificateGenerator.cs` - Development certificate generation tool
- Dependency injection extensions for easily configuring secure servers and clients

For comprehensive security, TLS should be used in conjunction with other security features like:
- JWT-based authentication
- Role-based authorization
- Input validation
- Rate limiting
