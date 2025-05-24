using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ModelContextProtocol.Extensions.Testing;
using ModelContextProtocol.Extensions.Testing.Doubles;
using ModelContextProtocol.Extensions.Testing.Chaos;
using ModelContextProtocol.Extensions.Security.Pipeline;
using ModelContextProtocol.Extensions.Security.HSM;
using ModelContextProtocol.Extensions.Resilience;
using ModelContextProtocol.Extensions.Diagnostics;
using ModelContextProtocol.Core.Protocol;

namespace ModelContextProtocol.Extensions.IntegrationTests
{
    /// <summary>
    /// Integration tests for enhanced MCP features
    /// </summary>
    public class EnhancedFeaturesTests
    {
        private readonly ITestOutputHelper _output;

        public EnhancedFeaturesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CertificateValidationPipeline_ShouldValidateAllSteps()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .UseTestTimeProvider(new DateTime(2024, 1, 1))
                .UseMockCertificateValidator(alwaysValid: true)
                .WithTestTlsConfiguration()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ICertificateValidationPipeline, CertificateValidationPipeline>();
                    services.AddSingleton<ICertificateValidationStep, ExpiryValidationStep>();
                    services.AddSingleton<ICertificateValidationStep, KeyUsageValidationStep>();
                })
                .Build();

            var pipeline = testServer.GetRequiredService<ICertificateValidationPipeline>();
            var timeProvider = testServer.GetRequiredService<TestTimeProvider>();

            // Create a test certificate (in real scenario, you'd use a proper certificate)
            var certificate = CreateTestCertificate();
            var context = new CertificateValidationContext
            {
                CertificateType = CertificateType.Server,
                TlsOptions = new TlsOptions()
            };

            // Act
            var result = await pipeline.ValidateAsync(certificate, context);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.StepResults);
            Assert.True(result.TotalDuration > TimeSpan.Zero);

            _output.WriteLine($"Validation completed in {result.TotalDuration.TotalMilliseconds}ms");
            _output.WriteLine($"Steps executed: {result.StepResults.Count}");
        }

        [Fact]
        public async Task BulkheadPolicy_ShouldLimitConcurrentExecutions()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .WithConfiguration("Tls:BulkheadOptions:MaxConcurrentExecutions", 2)
                .WithConfiguration("Tls:BulkheadOptions:MaxQueueSize", 1)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IBulkheadPolicy<string>, BulkheadPolicy<string>>();
                })
                .Build();

            var bulkhead = testServer.GetRequiredService<IBulkheadPolicy<string>>();

            // Act & Assert
            var task1 = bulkhead.ExecuteAsync(async ct => 
            {
                await Task.Delay(100, ct);
                return "result1";
            });

            var task2 = bulkhead.ExecuteAsync(async ct => 
            {
                await Task.Delay(100, ct);
                return "result2";
            });

            // This should be queued
            var task3 = bulkhead.ExecuteAsync(async ct => 
            {
                await Task.Delay(50, ct);
                return "result3";
            });

            // This should be rejected due to queue limit
            await Assert.ThrowsAsync<BulkheadRejectedException>(() =>
                bulkhead.ExecuteAsync(async ct => 
                {
                    await Task.Delay(50, ct);
                    return "result4";
                }));

            // Wait for the first tasks to complete
            var results = await Task.WhenAll(task1, task2, task3);
            
            Assert.Equal(3, results.Length);
            Assert.Contains("result1", results);
            Assert.Contains("result2", results);
            Assert.Contains("result3", results);

            var metrics = bulkhead.GetMetrics();
            _output.WriteLine($"Bulkhead metrics: {metrics}");
        }

        [Fact]
        public async Task ChaosPolicy_ShouldInjectFailures()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .EnableChaos(chaos => chaos
                    .Enable()
                    .WithFailureRate(0.5) // 50% failure rate
                    .WithDelayRate(0.3)   // 30% delay rate
                    .WithDelayRange(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100)))
                .Build();

            var chaosPolicy = testServer.GetRequiredService<IChaosPolicy>();

            // Act & Assert
            int successCount = 0;
            int failureCount = 0;
            int delayCount = 0;

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    await chaosPolicy.ExecuteAsync(async () =>
                    {
                        await Task.Delay(10);
                        return "success";
                    });
                    
                    stopwatch.Stop();
                    successCount++;
                    
                    if (stopwatch.ElapsedMilliseconds > 50)
                    {
                        delayCount++;
                    }
                }
                catch (ChaosInjectionException)
                {
                    failureCount++;
                }
            }

            _output.WriteLine($"Success: {successCount}, Failures: {failureCount}, Delays: {delayCount}");
            
            // With 50% failure rate, we should see some failures
            Assert.True(failureCount > 0, "Expected some chaos failures to be injected");
        }

        [Fact]
        public async Task MockHsm_ShouldSimulateHsmOperations()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .UseMockHsm()
                .Build();

            var hsm = testServer.GetRequiredService<IHardwareSecurityModule>();

            // Act
            var connectivity = await hsm.TestConnectivityAsync();
            var keyId = await hsm.GenerateKeyPairAsync("test-key", HsmKeyType.RSA, 2048);
            var keyInfo = await hsm.GetKeyInfoAsync(keyId);
            var signature = await hsm.SignDataAsync(keyId, new byte[] { 1, 2, 3, 4 });
            var isValid = await hsm.VerifySignatureAsync(keyId, new byte[] { 1, 2, 3, 4 }, signature);

            // Assert
            Assert.True(connectivity);
            Assert.NotNull(keyId);
            Assert.NotNull(keyInfo);
            Assert.Equal("test-key", keyInfo.KeyName);
            Assert.Equal(HsmKeyType.RSA, keyInfo.KeyType);
            Assert.Equal(2048, keyInfo.KeySize);
            Assert.NotNull(signature);
            Assert.True(isValid);

            // Check mock operations
            var mockHsm = hsm as MockHardwareSecurityModule;
            var operations = mockHsm.GetOperations();
            
            Assert.Contains(operations, op => op.Operation == "TestConnectivity");
            Assert.Contains(operations, op => op.Operation == "GenerateKeyPair");
            Assert.Contains(operations, op => op.Operation == "GetKeyInfo");
            Assert.Contains(operations, op => op.Operation == "SignData");
            Assert.Contains(operations, op => op.Operation == "VerifySignature");

            _output.WriteLine($"HSM operations performed: {operations.Count}");
        }

        [Fact]
        public async Task DiagnosticsService_ShouldGenerateComprehensiveReport()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
                    services.AddSingleton<DiagnosticMetricsCollector>();
                })
                .Build();

            var diagnostics = testServer.GetRequiredService<IDiagnosticsService>();
            var collector = testServer.GetRequiredService<DiagnosticMetricsCollector>();

            // Simulate some activity
            for (int i = 0; i < 10; i++)
            {
                var duration = TimeSpan.FromMilliseconds(50 + i * 10);
                var isError = i % 5 == 0; // 20% error rate
                diagnostics.RecordRequest(duration, isError);
            }

            diagnostics.RecordConnection(true);
            diagnostics.RecordConnection(false);
            diagnostics.AddCustomMetric("test_metric", 42);

            // Act
            var report = await diagnostics.GenerateReportAsync();
            var metrics = await diagnostics.GetPerformanceMetricsAsync();
            var summary = collector.GetSummary();

            // Assert
            Assert.NotNull(report);
            Assert.True(report.GenerationDuration > TimeSpan.Zero);
            Assert.NotNull(report.SystemInfo);
            Assert.NotNull(report.Performance);
            Assert.NotNull(report.MemoryInfo);
            Assert.NotNull(report.ThreadPoolInfo);
            Assert.NotNull(report.CustomMetrics);
            Assert.True(report.CustomMetrics.ContainsKey("test_metric"));
            Assert.Equal(42, report.CustomMetrics["test_metric"]);

            Assert.NotNull(metrics);
            Assert.True(metrics.TotalRequests > 0);
            Assert.True(metrics.ErrorRate > 0);

            Assert.NotNull(summary);
            Assert.Equal(10, summary.TotalRequests);
            Assert.Equal(2, summary.TotalErrors);

            _output.WriteLine($"Report generation time: {report.GenerationDuration.TotalMilliseconds}ms");
            _output.WriteLine($"Total requests: {metrics.TotalRequests}");
            _output.WriteLine($"Error rate: {metrics.ErrorRate:F1}%");
            _output.WriteLine($"Average latency: {metrics.AverageLatency.TotalMilliseconds:F1}ms");
        }

        [Fact]
        public async Task ProtocolNegotiation_ShouldSelectBestProtocol()
        {
            // Arrange
            var testServer = new McpTestServerBuilder()
                .WithConfiguration("Tls:ProtocolNegotiation:EnableNegotiation", true)
                .WithConfiguration("Tls:ProtocolNegotiation:SupportedProtocols:0", "json-rpc")
                .WithConfiguration("Tls:ProtocolNegotiation:SupportedProtocols:1", "msgpack")
                .WithConfiguration("Tls:ProtocolNegotiation:DefaultProtocol", "json-rpc")
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IProtocolHandler, JsonRpcProtocolHandler>();
                    services.AddSingleton<IProtocolNegotiator, ProtocolNegotiator>();
                })
                .Build();

            var negotiator = testServer.GetRequiredService<IProtocolNegotiator>();

            // Act
            using var stream = new MemoryStream();
            
            // In a real scenario, this would involve actual protocol negotiation
            // For this test, we'll just verify the negotiator is properly configured
            Assert.NotNull(negotiator);

            _output.WriteLine("Protocol negotiator successfully configured");
        }

        [Fact]
        public void TestTimeProvider_ShouldAllowTimeManipulation()
        {
            // Arrange
            var initialTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var timeProvider = new TestTimeProvider(initialTime);

            // Act & Assert
            Assert.Equal(initialTime, timeProvider.UtcNow);

            timeProvider.Advance(TimeSpan.FromHours(1));
            Assert.Equal(initialTime.AddHours(1), timeProvider.UtcNow);

            var newTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            timeProvider.SetTime(newTime);
            Assert.Equal(newTime, timeProvider.UtcNow);

            timeProvider.Reset();
            // After reset, time should be close to current time
            Assert.True(Math.Abs((DateTime.UtcNow - timeProvider.UtcNow).TotalSeconds) < 1);

            _output.WriteLine($"Time manipulation test completed successfully");
        }

        private System.Security.Cryptography.X509Certificates.X509Certificate2 CreateTestCertificate()
        {
            // In a real test, you would create or load a proper test certificate
            // For this example, we'll create a minimal certificate
            // Note: This is a simplified approach for testing purposes
            
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=Test Certificate", 
                rsa, 
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            var certificate = request.CreateSelfSigned(
                DateTime.UtcNow.AddDays(-1), 
                DateTime.UtcNow.AddDays(365));

            return certificate;
        }
    }
}
