# MCP Extensions Documentation

## Quick Start Guide

### 1. Basic Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add MCP Extensions with comprehensive configuration
builder.Services.AddMcpExtensions(builder.Configuration, options =>
{
    options.Security.EnableCertificateValidation = true;
    options.Security.EnableCertificatePinning = true;
    options.Resilience.EnableRateLimiting = true;
    options.Observability.EnableTelemetry = true;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.Run();
```

### 2. Configuration Examples

#### Production Configuration (appsettings.Production.json)
```json
{
  "McpExtensions": {
    "Security": {
      "EnableCertificateValidation": true,
      "EnableCertificatePinning": true,
      "TlsOptions": {
        "UseTls": true,
        "RequireClientCertificate": true,
        "AllowUntrustedCertificates": false,
        "MinimumTlsVersion": "1.3"
      }
    },
    "Resilience": {
      "EnableRateLimiting": true,
      "RateLimitingType": "Adaptive",
      "EnableCircuitBreaker": true
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server={azurevault:mcp-vault:db-server};Database={azurevault:mcp-vault:db-name};User ID={azurevault:mcp-vault:db-user};Password={azurevault:mcp-vault:db-password};"
  }
}
```

### 3. Certificate Management

```csharp
// Generate development certificates
var logger = serviceProvider.GetRequiredService<ILogger<TlsSetupTool>>();
var tool = new TlsSetupTool(logger);

tool.GenerateCertificates(
    outputDir: "./certs",
    serverName: "MCP-Server",
    clientNames: new[] { "Client1", "Client2" },
    password: "DevPassword123",
    validityDays: 365
);
```

### 4. Advanced Security Configuration

```csharp
// Certificate validation with custom pipeline
services.AddScoped<ICertificateValidationPipeline, CertificateValidationPipeline>();
services.AddScoped<ICertificateValidationStep, ExpiryValidationStep>();
services.AddScoped<ICertificateValidationStep, KeyUsageValidationStep>();
services.AddScoped<ICertificateValidationStep, RevocationValidationStep>();
services.AddScoped<ICertificateValidationStep, TransparencyValidationStep>();
services.AddScoped<ICertificateValidationStep, PinningValidationStep>();

// Configure certificate pinning
services.Configure<CertificatePinningOptions>(options =>
{
    options.Enabled = true;
    options.AutoPinFirstCertificate = false; // Security: Never enable in production
    options.PinnedCertificates = new List<string>
    {
        "1234567890ABCDEF...", // Production server cert thumbprint
        "FEDCBA0987654321..."  // Backup server cert thumbprint
    };
});
```

## Architecture Patterns

### 1. Result Pattern Usage

```csharp
public async Task<Result<Certificate>> GetCertificateAsync(string thumbprint)
{
    try
    {
        var certificate = await _certificateStore.GetAsync(thumbprint);
        return certificate != null 
            ? Result<Certificate>.Success(certificate)
            : Result<Certificate>.Failure("Certificate not found");
    }
    catch (Exception ex)
    {
        return Result<Certificate>.Failure(ex);
    }
}

// Usage
var result = await GetCertificateAsync("...");
if (result.IsSuccess)
{
    var certificate = result.Value;
    // Use certificate
}
else
{
    _logger.LogError("Failed to get certificate: {Error}", result.Error.Message);
}
```

### 2. Resilience Patterns

```csharp
// Rate limiting with adaptive behavior
var rateLimiter = serviceProvider.GetRequiredService<AdaptiveRateLimiter>();
var result = await rateLimiter.IsAllowedAsync(clientId);

if (!result.IsAllowed)
{
    return StatusCode(429, new { 
        error = "Rate limit exceeded",
        retryAfter = result.RetryAfter.TotalSeconds 
    });
}

// Circuit breaker for external dependencies
var circuitBreaker = serviceProvider.GetRequiredService<KeyVaultCircuitBreaker>();
var secret = await circuitBreaker.ExecuteAsync(
    () => keyVaultService.GetSecretAsync("my-secret"),
    () => fallbackSecretProvider.GetSecretAsync("my-secret")
);
```

## Monitoring & Observability

### 1. Custom Metrics

```csharp
public class CustomMetrics
{
    private readonly IMcpTelemetry _telemetry;
    
    public void RecordBusinessMetric(string operation, TimeSpan duration, bool success)
    {
        _telemetry.RecordRequestCompleted(operation, success, duration.TotalMilliseconds);
        _telemetry.AddCustomMetric($"business.{operation}.count", 1);
        _telemetry.AddCustomMetric($"business.{operation}.duration_ms", duration.TotalMilliseconds);
    }
}
```

### 2. Health Checks

```csharp
services.AddHealthChecks()
    .AddCheck<McpHealthCheck>("mcp_server")
    .AddCheck<ComprehensiveHealthCheck>("comprehensive")
    .AddCheck("database", () => 
    {
        // Custom health check logic
        return HealthCheckResult.Healthy("Database connection is healthy");
    });
```

## Performance Optimization

### 1. Memory Management

```csharp
// Use object pooling for frequently allocated objects
private static readonly ObjectPool<StringBuilder> StringBuilderPool = 
    new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy());

public string BuildLogMessage(params object[] args)
{
    var sb = StringBuilderPool.Get();
    try
    {
        // Build message...
        return sb.ToString();
    }
    finally
    {
        StringBuilderPool.Return(sb);
    }
}
```

### 2. Async Best Practices

```csharp
// Good: Proper async/await usage
public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
{
    var tasks = validationSteps.Select(step => 
        step.ValidateAsync(certificate, context, cancellationToken));
    
    var results = await Task.WhenAll(tasks);
    return CombineResults(results);
}

// Avoid: Blocking async calls
// var result = ValidateAsync().Result; // DON'T DO THIS
```

## Security Best Practices

### 1. Certificate Management

- Always validate certificate chains in production
- Use certificate pinning for critical connections
- Implement proper certificate rotation
- Monitor certificate expiration dates

### 2. Secret Management

- Use Azure Key Vault or similar HSM solutions
- Implement secret rotation
- Never log sensitive information
- Use connection string sanitization

### 3. Input Validation

- Validate all inputs at API boundaries
- Use schema validation for complex requests
- Implement rate limiting and request size limits
- Sanitize all output for logging

## Troubleshooting Guide

### Common Issues

1. **Certificate Validation Failures**
   - Check certificate expiration dates
   - Verify certificate chain completeness
   - Ensure proper certificate store permissions

2. **Rate Limiting Issues**
   - Monitor adaptive rate limiter statistics
   - Check client identification logic
   - Verify rate limit configuration

3. **Connection Issues**
   - Check TLS configuration
   - Verify network connectivity
   - Monitor connection pool statistics

### Logging and Diagnostics

```csharp
// Enable comprehensive logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddApplicationInsights();
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddFilter("Microsoft", LogLevel.Warning);
    builder.AddFilter("ModelContextProtocol", LogLevel.Debug);
});

// Structured logging examples
_logger.LogInformation("Certificate validation completed for {Thumbprint} in {Duration}ms", 
    certificate.Thumbprint, duration.TotalMilliseconds);
```

## Migration Guide

### From Basic MCP to Extensions

1. Update package references
2. Modify service registration
3. Update configuration files
4. Test security features
5. Enable monitoring and health checks

### Configuration Migration

```csharp
// Before
services.AddMcpServer(options => 
{
    options.Host = "localhost";
    options.Port = 8080;
});

// After
services.AddMcpExtensions(configuration, options =>
{
    options.Security.EnableCertificateValidation = true;
    options.Resilience.EnableRateLimiting = true;
    options.Observability.EnableTelemetry = true;
});
```