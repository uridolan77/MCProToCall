// 1. Test Builders for Complex Objects
public class CertificateValidationContextBuilder
{
    private X509Chain _chain = new();
    private SslPolicyErrors _sslPolicyErrors = SslPolicyErrors.None;
    private string _remoteEndpoint = "test.example.com";
    private CertificateType _certificateType = CertificateType.Server;
    private TlsOptions _tlsOptions = new();
    
    public static CertificateValidationContextBuilder Create() => new();
    
    public CertificateValidationContextBuilder WithChain(X509Chain chain)
    {
        _chain = chain;
        return this;
    }
    
    public CertificateValidationContextBuilder WithSslErrors(SslPolicyErrors errors)
    {
        _sslPolicyErrors = errors;
        return this;
    }
    
    public CertificateValidationContextBuilder AsClientCertificate()
    {
        _certificateType = CertificateType.Client;
        return this;
    }
    
    public CertificateValidationContext Build() => new()
    {
        Chain = _chain,
        SslPolicyErrors = _sslPolicyErrors,
        RemoteEndpoint = _remoteEndpoint,
        CertificateType = _certificateType,
        TlsOptions = _tlsOptions
    };
}

// 2. Mock Certificate Factory
public static class TestCertificateFactory
{
    public static X509Certificate2 CreateValidServerCertificate(
        string subject = "CN=test.example.com",
        TimeSpan? validity = null)
    {
        validity ??= TimeSpan.FromDays(365);
        
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        // Add Server Authentication EKU
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
                
        var certificate = request.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.Add(validity.Value));
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string)null, X509KeyStorageFlags.Exportable);
    }
    
    public static X509Certificate2 CreateExpiredCertificate(string subject = "CN=expired.example.com")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        var notBefore = DateTime.UtcNow.AddDays(-30);
        var notAfter = DateTime.UtcNow.AddDays(-1);
        
        var certificate = request.CreateSelfSigned(notBefore, notAfter);
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
    }
}

// 3. Integration Test Base Class
public abstract class McpIntegrationTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<Program> Factory { get; private set; }
    protected HttpClient Client { get; private set; }
    protected IServiceScope Scope { get; private set; }
    
    public virtual async Task InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(ConfigureTestServices);
                builder.UseEnvironment("Testing");
            });
            
        Client = Factory.CreateClient();
        Scope = Factory.Services.CreateScope();
    }
    
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Remove real services and add test doubles
        services.RemoveAll<IAzureKeyVaultService>();
        services.AddSingleton<IAzureKeyVaultService, MockAzureKeyVaultService>();
        
        services.RemoveAll<IHardwareSecurityModule>();
        services.AddSingleton<IHardwareSecurityModule, MockHardwareSecurityModule>();
        
        // Add test-specific configuration
        services.Configure<TlsOptions>(options =>
        {
            options.AllowUntrustedCertificates = true;
            options.AllowSelfSignedCertificates = true;
        });
    }
    
    protected T GetService<T>() => Scope.ServiceProvider.GetRequiredService<T>();
    
    public virtual async Task DisposeAsync()
    {
        Scope?.Dispose();
        Client?.Dispose();
        await Factory?.DisposeAsync();
    }
}

// 4. Behavior-Driven Test Extensions
public static class BehaviorTestExtensions
{
    public static async Task<TResult> Given<TResult>(this Task<TResult> setup, string description)
    {
        Console.WriteLine($"Given: {description}");
        return await setup;
    }
    
    public static async Task<TResult> When<T, TResult>(this Task<T> given, Func<T, Task<TResult>> action, string description)
    {
        Console.WriteLine($"When: {description}");
        var input = await given;
        return await action(input);
    }
    
    public static async Task Then<T>(this Task<T> when, Action<T> assertion, string description)
    {
        Console.WriteLine($"Then: {description}");
        var result = await when;
        assertion(result);
    }
}

// 5. Sample Unit Test
[TestClass]
public class CertificateValidationPipelineTests : McpIntegrationTestBase
{
    [TestMethod]
    public async Task ValidateCertificate_WithValidServerCertificate_ShouldSucceed()
    {
        // Arrange
        var certificate = TestCertificateFactory.CreateValidServerCertificate();
        var context = CertificateValidationContextBuilder.Create()
            .WithSslErrors(SslPolicyErrors.None)
            .Build();
        
        var pipeline = GetService<ICertificateValidationPipeline>();
        
        // Act
        var result = await pipeline.ValidateAsync(certificate, context);
        
        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Warnings.Count);
    }
    
    [TestMethod]
    public async Task ValidateCertificate_WithExpiredCertificate_ShouldFail()
    {
        await TestCertificateFactory.CreateExpiredCertificate()
            .Given("an expired certificate")
            .When(cert => 
            {
                var context = CertificateValidationContextBuilder.Create().Build();
                var pipeline = GetService<ICertificateValidationPipeline>();
                return pipeline.ValidateAsync(cert, context);
            }, "validating the certificate")
            .Then(result =>
            {
                Assert.IsFalse(result.IsValid);
                Assert.IsTrue(result.ErrorMessage.Contains("expired"));
            }, "the validation should fail with expiry error");
    }
}

// 6. Performance Testing Utilities
public class PerformanceTestHarness
{
    public static async Task<PerformanceResult> MeasureAsync<T>(
        Func<Task<T>> operation,
        int iterations = 100,
        int warmupIterations = 10)
    {
        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            await operation();
        }
        
        var results = new List<TimeSpan>();
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            var iterationSw = Stopwatch.StartNew();
            await operation();
            iterationSw.Stop();
            results.Add(iterationSw.Elapsed);
        }
        
        sw.Stop();
        
        return new PerformanceResult
        {
            TotalTime = sw.Elapsed,
            Iterations = iterations,
            AverageTime = TimeSpan.FromTicks(results.Sum(r => r.Ticks) / results.Count),
            MedianTime = results.OrderBy(r => r.Ticks).Skip(results.Count / 2).First(),
            MinTime = results.Min(),
            MaxTime = results.Max()
        };
    }
}

public record PerformanceResult
{
    public TimeSpan TotalTime { get; init; }
    public int Iterations { get; init; }
    public TimeSpan AverageTime { get; init; }
    public TimeSpan MedianTime { get; init; }
    public TimeSpan MinTime { get; init; }
    public TimeSpan MaxTime { get; init; }
}