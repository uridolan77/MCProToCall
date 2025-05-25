// 1. Enhanced Configuration with Environment-Specific Validation
public class EnvironmentAwareConfigurationValidator : IValidateOptions<TlsOptions>
{
    private readonly IHostEnvironment _environment;
    
    public EnvironmentAwareConfigurationValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }
    
    public ValidateOptionsResult Validate(string name, TlsOptions options)
    {
        var failures = new List<string>();
        
        // Environment-specific validation rules
        if (_environment.IsProduction())
        {
            if (options.AllowUntrustedCertificates)
                failures.Add("Untrusted certificates must not be allowed in production");
                
            if (options.AllowSelfSignedCertificates)
                failures.Add("Self-signed certificates must not be allowed in production");
                
            if (!options.UseTls)
                failures.Add("TLS must be enabled in production");
        }
        
        if (_environment.IsDevelopment())
        {
            // Development-specific warnings (logged but not failed)
            if (!options.AllowUntrustedCertificates)
                Console.WriteLine("Warning: Consider allowing untrusted certificates in development");
        }
        
        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

// 2. Advanced Health Checks with Dependency Mapping
public class ComprehensiveHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComprehensiveHealthCheck> _logger;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var healthData = new Dictionary<string, object>();
        var checks = new[]
        {
            ("KeyVault", CheckKeyVaultHealth),
            ("Certificate", CheckCertificateHealth),
            ("RateLimit", CheckRateLimitHealth),
            ("WebSocket", CheckWebSocketHealth)
        };
        
        var overallHealthy = true;
        var warnings = new List<string>();
        
        foreach (var (name, check) in checks)
        {
            try
            {
                var (isHealthy, data, warning) = await check(cancellationToken);
                healthData[name] = data;
                
                if (!isHealthy) overallHealthy = false;
                if (!string.IsNullOrEmpty(warning)) warnings.Add($"{name}: {warning}");
            }
            catch (Exception ex)
            {
                healthData[name] = $"Error: {ex.Message}";
                overallHealthy = false;
            }
        }
        
        var status = overallHealthy ? HealthStatus.Healthy : 
                    warnings.Count > 0 ? HealthStatus.Degraded : HealthStatus.Unhealthy;
                    
        return new HealthCheckResult(status, data: healthData);
    }
    
    private async Task<(bool isHealthy, object data, string warning)> CheckKeyVaultHealth(CancellationToken cancellationToken)
    {
        var keyVaultService = _serviceProvider.GetService<IAzureKeyVaultService>();
        if (keyVaultService == null) 
            return (true, "Not configured", null);
        
        try
        {
            // Test connectivity with a known test secret
            await keyVaultService.GetSecretAsync("health-check-test");
            return (true, "Connected", null);
        }
        catch (SecretNotFoundException)
        {
            return (true, "Connected (test secret not found)", "Test secret not configured");
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}", null);
        }
    }
    
    // Additional health check methods...
}

// 3. Custom Metrics with OpenTelemetry
public class EnhancedMcpTelemetry : McpTelemetry
{
    private readonly Histogram<double> _certificateValidationDuration;
    private readonly Counter<long> _securityViolations;
    private readonly UpDownCounter<long> _activeConnections;
    
    public EnhancedMcpTelemetry(ILogger<McpTelemetry> logger) : base(logger)
    {
        var meter = new Meter(MeterName, "1.0.0");
        
        _certificateValidationDuration = meter.CreateHistogram<double>(
            "mcp.certificate.validation.duration",
            unit: "ms",
            description: "Time taken to validate certificates");
            
        _securityViolations = meter.CreateCounter<long>(
            "mcp.security.violations.total",
            description: "Total number of security violations detected");
            
        _activeConnections = meter.CreateUpDownCounter<long>(
            "mcp.connections.active.detailed",
            description: "Active connections by type and endpoint");
    }
    
    public void RecordCertificateValidation(string validationType, double durationMs, bool successful)
    {
        _certificateValidationDuration.Record(durationMs,
            new KeyValuePair<string, object>("validation_type", validationType),
            new KeyValuePair<string, object>("result", successful ? "success" : "failure"));
    }
    
    public void RecordSecurityViolation(string violationType, string endpoint)
    {
        _securityViolations.Add(1,
            new KeyValuePair<string, object>("violation_type", violationType),
            new KeyValuePair<string, object>("endpoint", endpoint));
    }
}

// 4. Configuration Hot Reload with Validation
public class ValidatedConfigurationReloader<T> : IOptionsMonitor<T> where T : class, IValidatableObject, new()
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ValidatedConfigurationReloader<T>> _logger;
    private readonly string _sectionName;
    private T _currentValue;
    private readonly List<IDisposable> _changeTokenRegistrations = new();
    
    public T CurrentValue => _currentValue ??= LoadAndValidate();
    
    public T Get(string name) => CurrentValue;
    
    public IDisposable OnChange(Action<T, string> listener)
    {
        var registration = ChangeToken.OnChange(_configuration.GetReloadToken, () =>
        {
            try
            {
                var newValue = LoadAndValidate();
                var oldValue = _currentValue;
                _currentValue = newValue;
                listener(newValue, null);
                
                _logger.LogInformation("Configuration reloaded successfully for {Type}", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload configuration for {Type}", typeof(T).Name);
            }
        });
        
        _changeTokenRegistrations.Add(registration);
        return registration;
    }
    
    private T LoadAndValidate()
    {
        var instance = new T();
        _configuration.GetSection(_sectionName).Bind(instance);
        
        var validationContext = new ValidationContext(instance);
        var validationResults = instance.Validate(validationContext).ToList();
        
        if (validationResults.Count > 0)
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Configuration validation failed for {typeof(T).Name}: {errors}");
        }
        
        return instance;
    }
}

// 5. Distributed Configuration with Consensus
public class DistributedConfigurationProvider : IConfigurationProvider
{
    private readonly Dictionary<string, string> _data = new();
    private readonly ILogger<DistributedConfigurationProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string[] _configurationEndpoints;
    
    public void Load()
    {
        // Load configuration from multiple sources and achieve consensus
        var configurations = LoadFromMultipleSources();
        var consensusConfig = AchieveConsensus(configurations);
        
        foreach (var kvp in consensusConfig)
        {
            _data[kvp.Key] = kvp.Value;
        }
    }
    
    private Dictionary<string, string> AchieveConsensus(List<Dictionary<string, string>> configurations)
    {
        var result = new Dictionary<string, string>();
        
        // Simple majority consensus for each configuration key
        var allKeys = configurations.SelectMany(c => c.Keys).Distinct();
        
        foreach (var key in allKeys)
        {
            var values = configurations
                .Where(c => c.ContainsKey(key))
                .Select(c => c[key])
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .First();
                
            result[key] = values.Key;
        }
        
        return result;
    }
    
    public bool TryGet(string key, out string value) => _data.TryGetValue(key, out value);
    public void Set(string key, string value) => _data[key] = value;
    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath) 
        => _data.Keys.Where(k => k.StartsWith(parentPath));
}

// 6. Configuration Schema Registry
public static class ConfigurationSchemaRegistry
{
    private static readonly ConcurrentDictionary<Type, JsonSchema> _schemas = new();
    
    public static void RegisterSchema<T>(JsonSchema schema)
    {
        _schemas[typeof(T)] = schema;
    }
    
    public static JsonSchema GetSchema<T>() => _schemas.GetValueOrDefault(typeof(T));
    
    public static ValidationResult ValidateConfiguration<T>(T configuration)
    {
        var schema = GetSchema<T>();
        if (schema == null) return ValidationResult.Success();
        
        var json = JsonSerializer.Serialize(configuration);
        var document = JsonDocument.Parse(json);
        
        var validationResults = schema.Validate(document.RootElement);
        // Convert JsonSchema validation results to our ValidationResult format
        
        return ValidationResult.Success(); // Simplified for brevity
    }
}