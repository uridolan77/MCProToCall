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

MCP includes utilities to generate and manage certificates:

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

## Advanced Security Features

### Certificate Validation

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

### Certificate Revocation

```csharp
services.Configure<TlsOptions>(options => {
    options.CheckCertificateRevocation = true;
    options.RevocationCheckMode = RevocationCheckMode.OcspAndCrl;
    options.RevocationFailureMode = RevocationFailureMode.Deny;
    options.RevocationCachePath = "./certs/revocation";
    options.CrlUpdateIntervalHours = 24;
});
```

### Certificate Pinning

```csharp
services.Configure<TlsOptions>(options => {
    options.UseCertificatePinning = true;
    options.PinnedCertificates = new List<string> { "./certs/trusted.cer" };
    options.CertificatePinStoragePath = "./certs/pins";
    options.RequireExactCertificateMatch = true;
});
```

### Connection Rate Limiting

```csharp
services.Configure<TlsOptions>(options => {
    options.MaxConnectionsPerIpAddress = 50;
    options.ConnectionRateLimitingWindowSeconds = 60;
});
```

## Security Best Practices

1. Use strong certificates from trusted CAs in production
2. Enable mutual TLS authentication for sensitive APIs
3. Always enable certificate revocation checking in production
4. Use certificate pinning to prevent MITM attacks
5. Regularly rotate certificates before they expire
6. Implement connection rate limiting to prevent DoS attacks
7. Store private keys securely, consider using HSMs for production
8. Monitor TLS connections for suspicious activity
9. Keep TLS libraries updated with security patches
10. Only enable TLS 1.2 and 1.3 in production environments

## Troubleshooting

Common issues include:

- Certificate validation failures: Check expiration dates and trust chains
- Handshake failures: Verify protocol compatibility and cipher suites
- Connection timeouts: Check firewall settings and network connectivity
- Revocation checking failures: Ensure CRL endpoints are accessible
- Client certificate issues: Verify client certificates are signed by trusted CAs

Enable detailed TLS logging for troubleshooting:

```csharp
options.EnableDetailedTlsLogging = true;
```
