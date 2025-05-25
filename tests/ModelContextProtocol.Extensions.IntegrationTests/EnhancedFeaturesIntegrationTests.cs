using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.ErrorHandling;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;
using ModelContextProtocol.Extensions.Testing;
using ModelContextProtocol.Extensions.Performance;
using ModelContextProtocol.Extensions.Observability;
using ModelContextProtocol.Extensions.Diagnostics;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ModelContextProtocol.Extensions.IntegrationTests
{
    /// <summary>
    /// Integration tests for enhanced MCP features
    /// </summary>
    [Collection("MCP Integration Tests")]
    public class EnhancedFeaturesIntegrationTests : McpIntegrationTestBase
    {
        private readonly ITestOutputHelper _output;

        public EnhancedFeaturesIntegrationTests(McpTestFixture testFixture, ITestOutputHelper output)
            : base(testFixture)
        {
            _output = output;
        }

        [Fact]
        public async Task CertificateValidation_WithValidCertificate_ShouldSucceed()
        {
            await TestCertificateFactory.CreateValidServerCertificate()
                .Given("a valid server certificate")
                .When(cert =>
                {
                    var context = CertificateValidationContextBuilder.Create()
                        .WithSslErrors(System.Net.Security.SslPolicyErrors.None)
                        .AsServerCertificate()
                        .Build();

                    var validator = GetService<ICertificateValidationPipeline>();
                    return validator.ValidateAsync(cert, context);
                }, "validating the certificate")
                .Then(result =>
                {
                    Assert.True(result.IsValid);
                    Assert.Empty(result.Warnings);
                    _output.WriteLine($"Certificate validation successful: {result.IsValid}");
                }, "the validation should succeed");
        }

        [Fact]
        public async Task CertificateValidation_WithExpiredCertificate_ShouldFail()
        {
            await Task.FromResult(TestCertificateFactory.CreateExpiredCertificate())
                .Given("an expired certificate")
                .When(cert =>
                {
                    var context = CertificateValidationContextBuilder.Create()
                        .WithSslErrors(System.Net.Security.SslPolicyErrors.None)
                        .AsServerCertificate()
                        .Build();

                    var validator = GetService<ICertificateValidationPipeline>();
                    return validator.ValidateAsync(cert, context);
                }, "validating the expired certificate")
                .Then(result =>
                {
                    Assert.False(result.IsValid);
                    Assert.Contains("expired", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                    _output.WriteLine($"Certificate validation failed as expected: {result.ErrorMessage}");
                }, "the validation should fail with expiry error");
        }

        [Fact]
        public async Task ResultPattern_WithSuccessfulOperation_ShouldReturnSuccess()
        {
            var result = await RetryPolicies.ExecuteWithRetryAsync(async () =>
            {
                await Task.Delay(10);
                return "Success";
            });

            Assert.True(result.IsSuccess);
            Assert.Equal("Success", result.Value);
            _output.WriteLine($"Result pattern test successful: {result}");
        }

        [Fact]
        public async Task ResultPattern_WithFailingOperation_ShouldReturnFailure()
        {
            var result = await RetryPolicies.ExecuteWithRetryAsync<string>(async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Test failure");
            }, maxRetries: 2);

            Assert.True(result.IsFailure);
            Assert.Contains("Test failure", result.Error.Message);
            _output.WriteLine($"Result pattern failure test successful: {result}");
        }

        [Fact]
        public async Task PerformanceHarness_ShouldMeasureOperationPerformance()
        {
            var performanceResult = await PerformanceTestHarness.MeasureAsync(async () =>
            {
                await Task.Delay(50);
                return "test";
            }, iterations: 10, warmupIterations: 2);

            Assert.Equal(10, performanceResult.Iterations);
            Assert.True(performanceResult.AverageTime.TotalMilliseconds >= 40); // Should be around 50ms
            Assert.True(performanceResult.AverageTime.TotalMilliseconds <= 100); // Allow some variance

            _output.WriteLine($"Performance test results: {performanceResult}");
        }

        [Fact]
        public async Task ThroughputMeasurement_ShouldCalculateOperationsPerSecond()
        {
            var throughputResult = await PerformanceTestHarness.MeasureThroughputAsync(async () =>
            {
                await Task.Delay(10);
                return "test";
            }, TimeSpan.FromSeconds(1), maxConcurrency: 5);

            Assert.True(throughputResult.OperationsPerSecond > 0);
            Assert.True(throughputResult.CompletedOperations > 0);
            Assert.Equal(0, throughputResult.Errors);

            _output.WriteLine($"Throughput test results: {throughputResult}");
        }

        [Fact]
        public async Task MemoryMeasurement_ShouldTrackMemoryUsage()
        {
            var memoryResult = await PerformanceTestHarness.MeasureMemoryAsync(async () =>
            {
                // Allocate some memory
                var data = new byte[1024 * 1024]; // 1MB
                await Task.Delay(1);
                return data;
            }, iterations: 5);

            Assert.True(memoryResult.InitialMemory > 0);
            Assert.True(memoryResult.PeakMemory >= memoryResult.InitialMemory);

            _output.WriteLine($"Memory test results: {memoryResult}");
        }

        [Fact]
        public void StringUtilities_ShouldSanitizeConnectionString()
        {
            var connectionString = "Server=localhost;Database=test;User=admin;Password=secret123;";
            var sanitized = StringUtilitiesOptimized.SanitizeConnectionString(connectionString);

            Assert.Contains("Server=localhost", sanitized);
            Assert.Contains("Database=test", sanitized);
            Assert.Contains("User=admin", sanitized);
            Assert.DoesNotContain("secret123", sanitized);
            Assert.Contains("Password=***", sanitized);

            _output.WriteLine($"Original: {connectionString}");
            _output.WriteLine($"Sanitized: {sanitized}");
        }

        [Fact]
        public async Task ObjectPooling_ShouldReuseRequestMetrics()
        {
            var metric1 = DiagnosticMetricsPool.GetRequestMetric();
            metric1.RequestId = "test-1";
            metric1.Duration = TimeSpan.FromMilliseconds(100);

            DiagnosticMetricsPool.ReturnRequestMetric(metric1);

            var metric2 = DiagnosticMetricsPool.GetRequestMetric();

            // The pool should return a reset object
            Assert.NotEqual("test-1", metric2.RequestId);
            Assert.NotEqual(TimeSpan.FromMilliseconds(100), metric2.Duration);

            DiagnosticMetricsPool.ReturnRequestMetric(metric2);

            _output.WriteLine("Object pooling test completed successfully");
        }

        [Fact]
        public async Task EnhancedTelemetry_ShouldRecordMetrics()
        {
            var telemetry = GetService<EnhancedMcpTelemetry>();

            telemetry.RecordCertificateValidation("server", 50.5, true, "test.example.com");
            telemetry.RecordSecurityViolation("invalid_certificate", "test.example.com", "client-123");
            telemetry.RecordRequest("test.method", "test.endpoint", 25.0, true);
            telemetry.RecordHsmOperation("GetCertificate", "AzureKeyVault", 100.0, true, "test-key");

            // Telemetry recording is fire-and-forget, so we just verify no exceptions
            await Task.Delay(100);

            _output.WriteLine("Enhanced telemetry test completed successfully");
        }

        [Fact]
        public async Task BehaviorDrivenTest_ShouldSupportFluentSyntax()
        {
            await Task.FromResult("initial value")
                .Given("an initial value")
                .When(value => Task.FromResult(value.ToUpper()), "converting to uppercase")
                .And(value =>
                {
                    _output.WriteLine($"Intermediate value: {value}");
                    return Task.CompletedTask;
                }, "logging the intermediate value")
                .When(value => value + " PROCESSED", "adding processed suffix")
                .Then(result =>
                {
                    Assert.Equal("INITIAL VALUE PROCESSED", result);
                    _output.WriteLine($"Final result: {result}");
                }, "the result should be uppercase with suffix");
        }

        [Fact]
        public async Task ComprehensiveHealthCheck_ShouldValidateSystemHealth()
        {
            var healthCheck = GetService<ComprehensiveHealthCheck>();
            var context = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext();

            var result = await healthCheck.CheckHealthAsync(context);

            Assert.NotNull(result);
            Assert.NotNull(result.Data);

            _output.WriteLine($"Health check status: {result.Status}");
            _output.WriteLine($"Health check description: {result.Description}");

            foreach (var kvp in result.Data)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);

            // Add enhanced services for testing
            var configuration = TestFixture.Configuration;

            // Add enhanced telemetry
            services.AddSingleton<EnhancedMcpTelemetry>(provider =>
                new EnhancedMcpTelemetry(provider.GetRequiredService<ILogger<McpTelemetry>>()));

            // Add comprehensive health check
            services.AddSingleton<ComprehensiveHealthCheck>();
        }
    }
}
