using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnhancedSample
{
    /// <summary>
    /// Enhanced example client program demonstrating advanced MCP features
    /// </summary>
    public class ClientExample
    {
        private static readonly Dictionary<string, Func<StreamingClient, Task>> _demoMethods = new()
        {
            { "basic", RunBasicDemoAsync },
            { "caching", RunCachingDemoAsync },
            { "streaming", RunStreamingDemoAsync },
            { "performance", RunPerformanceDemoAsync },
            { "resilience", RunResilienceDemoAsync },
            { "security", RunSecurityDemoAsync },
            { "observability", RunObservabilityDemoAsync },
            { "all", RunAllDemosAsync }
        };

        /// <summary>
        /// Runs the client example with specified demo type
        /// </summary>
        /// <param name="demoType">Type of demo to run (basic, caching, streaming, performance, resilience, security, observability, all)</param>
        public static async Task RunAsync(string demoType = "all")
        {
            Console.WriteLine($"Starting enhanced client example - Demo: {demoType}");
            Console.WriteLine(new string('=', 60));

            using var client = new EnhancedStreamingClient("ws://localhost:8080/ws");

            try
            {
                await client.ConnectAsync();
                Console.WriteLine("✅ Connected to server");

                // Get server capabilities and health
                await DisplayServerInfoAsync(client);

                // Run specified demo
                if (_demoMethods.TryGetValue(demoType.ToLower(), out var demoMethod))
                {
                    await demoMethod(client);
                }
                else
                {
                    Console.WriteLine($"❌ Unknown demo type: {demoType}");
                    Console.WriteLine($"Available demos: {string.Join(", ", _demoMethods.Keys)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine(new string('=', 60));
            Console.WriteLine("Client example completed");
        }

        private static async Task DisplayServerInfoAsync(StreamingClient client)
        {
            Console.WriteLine("\n📊 Server Information:");

            // Get server capabilities
            var capabilities = await client.CallMethodAsync<dynamic>("system.getCapabilities");
            Console.WriteLine($"   Version: {capabilities.version}");
            Console.WriteLine($"   Streaming: {capabilities.features?.streaming ?? false}");
            Console.WriteLine($"   Caching: {capabilities.features?.caching ?? false}");
            Console.WriteLine($"   Security: {capabilities.features?.security ?? false}");

            // Get server health
            try
            {
                var health = await client.CallMethodAsync<dynamic>("system.health");
                Console.WriteLine($"   Health: {health.status} ({health.uptime})");
                Console.WriteLine($"   Memory: {health.memory?.used ?? "N/A"} / {health.memory?.total ?? "N/A"}");
            }
            catch
            {
                Console.WriteLine("   Health: Not available");
            }
        }

        private static async Task RunBasicDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n🔧 Basic Operations Demo:");

            // Ping test
            var pingResult = await client.CallMethodAsync<dynamic>("system.ping");
            Console.WriteLine($"   Ping: {pingResult.pong} at {pingResult.timestamp}");

            // Echo test
            var echoResult = await client.CallMethodAsync<dynamic>("system.echo", new { message = "Hello MCP!" });
            Console.WriteLine($"   Echo: {echoResult.message}");
        }

        private static async Task RunCachingDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n💾 Caching Demo:");

            // Call cached method multiple times to demonstrate caching
            var cacheKey = "demo.cached";
            var measurements = new List<double>();

            for (int i = 0; i < 5; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await client.CallMethodAsync<dynamic>(cacheKey, new { iteration = i });
                stopwatch.Stop();

                measurements.Add(stopwatch.Elapsed.TotalMilliseconds);
                Console.WriteLine($"   Call {i + 1}: {result.message} ({stopwatch.Elapsed.TotalMilliseconds:F1}ms)");

                if (i == 0) await Task.Delay(100); // Small delay between first and subsequent calls
            }

            var firstCall = measurements[0];
            var avgSubsequent = measurements.Skip(1).Average();
            Console.WriteLine($"   Cache speedup: {firstCall / avgSubsequent:F1}x");

            // Cache invalidation test
            try
            {
                await client.CallMethodAsync<dynamic>("cache.invalidate", new { pattern = "demo.*" });
                Console.WriteLine("   ✅ Cache invalidated");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Cache invalidation not available");
            }
        }

        private static async Task RunStreamingDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n🌊 Streaming Demo:");

            // Basic streaming
            Console.WriteLine("   Starting basic stream...");
            var streamId = await client.CallStreamingMethodAsync("system.streamingPing", new { count = 5 });
            Console.WriteLine($"   Stream ID: {streamId}");

            await foreach (var item in client.ConsumeStreamAsync<dynamic>(streamId))
            {
                Console.WriteLine($"   📦 {item.message} at {item.timestamp}");
            }

            // LLM streaming simulation
            Console.WriteLine("\n   Starting LLM stream...");
            var llmStreamId = await client.CallStreamingMethodAsync("llm.generate", new
            {
                prompt = "Write a haiku about programming",
                maxTokens = 50
            });

            var tokens = new List<string>();
            await foreach (var item in client.ConsumeStreamAsync<dynamic>(llmStreamId))
            {
                Console.Write($"{item.token} ");
                tokens.Add(item.token?.ToString() ?? "");
            }
            Console.WriteLine($"\n   📝 Generated {tokens.Count} tokens");
        }

        private static async Task RunPerformanceDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n⚡ Performance Demo:");

            // Concurrent requests test
            const int concurrentRequests = 10;
            var stopwatch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(i => client.CallMethodAsync<dynamic>("system.ping", new { id = i }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"   {concurrentRequests} concurrent requests completed in {stopwatch.Elapsed.TotalMilliseconds:F1}ms");
            Console.WriteLine($"   Average: {stopwatch.Elapsed.TotalMilliseconds / concurrentRequests:F1}ms per request");
            Console.WriteLine($"   Throughput: {concurrentRequests / stopwatch.Elapsed.TotalSeconds:F1} requests/second");

            // Get performance metrics
            try
            {
                var metrics = await client.CallMethodAsync<dynamic>("system.metrics");
                Console.WriteLine($"   Server metrics: {metrics.activeConnections} connections, {metrics.requestsPerSecond:F1} req/s");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Performance metrics not available");
            }
        }

        private static async Task RunResilienceDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n🛡️ Resilience Demo:");

            // Test retry mechanism
            try
            {
                var result = await client.CallMethodAsync<dynamic>("test.flaky", new { failureRate = 0.7 });
                Console.WriteLine($"   ✅ Flaky method succeeded: {result.message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Flaky method failed: {ex.Message}");
            }

            // Test circuit breaker
            try
            {
                var result = await client.CallMethodAsync<dynamic>("test.circuitBreaker");
                Console.WriteLine($"   🔌 Circuit breaker status: {result.status}");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Circuit breaker test not available");
            }

            // Test timeout handling
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var result = await client.CallMethodAsync<dynamic>("test.slow", new { delayMs = 5000 });
                Console.WriteLine($"   ⏱️ Slow method completed: {result.message}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("   ⏱️ Slow method timed out (expected)");
            }
        }

        private static async Task RunSecurityDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n🔒 Security Demo:");

            // Test authentication
            try
            {
                var authResult = await client.CallMethodAsync<dynamic>("auth.login", new { username = "demo", password = "demo123" });
                Console.WriteLine($"   🔑 Authentication: {authResult.status}");

                if (authResult.token != null)
                {
                    Console.WriteLine($"   🎫 Token received: {authResult.token.ToString().Substring(0, 20)}...");
                }
            }
            catch
            {
                Console.WriteLine("   ⚠️ Authentication not available");
            }

            // Test authorization
            try
            {
                var protectedResult = await client.CallMethodAsync<dynamic>("protected.resource");
                Console.WriteLine($"   🛡️ Protected resource: {protectedResult.message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   🚫 Protected resource denied: {ex.Message}");
            }

            // Test input validation
            try
            {
                var validationResult = await client.CallMethodAsync<dynamic>("validate.input", new { data = "<script>alert('xss')</script>" });
                Console.WriteLine($"   ✅ Input validation: {validationResult.sanitized}");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Input validation not available");
            }
        }

        private static async Task RunObservabilityDemoAsync(StreamingClient client)
        {
            Console.WriteLine("\n📊 Observability Demo:");

            // Get telemetry data
            try
            {
                var telemetry = await client.CallMethodAsync<dynamic>("system.telemetry");
                Console.WriteLine($"   📈 Requests: {telemetry.totalRequests}");
                Console.WriteLine($"   ⏱️ Avg Response Time: {telemetry.averageResponseTime}ms");
                Console.WriteLine($"   ❌ Error Rate: {telemetry.errorRate:P2}");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Telemetry not available");
            }

            // Get health check details
            try
            {
                var healthDetails = await client.CallMethodAsync<dynamic>("system.healthDetails");
                Console.WriteLine($"   💚 Database: {healthDetails.database?.status ?? "Unknown"}");
                Console.WriteLine($"   💚 Cache: {healthDetails.cache?.status ?? "Unknown"}");
                Console.WriteLine($"   💚 External APIs: {healthDetails.externalApis?.status ?? "Unknown"}");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Detailed health checks not available");
            }

            // Test distributed tracing
            try
            {
                var traceResult = await client.CallMethodAsync<dynamic>("trace.test", new { operation = "demo" });
                Console.WriteLine($"   🔍 Trace ID: {traceResult.traceId}");
                Console.WriteLine($"   📊 Span Count: {traceResult.spanCount}");
            }
            catch
            {
                Console.WriteLine("   ⚠️ Distributed tracing not available");
            }
        }

        private static async Task RunAllDemosAsync(StreamingClient client)
        {
            var demos = new[] { "basic", "caching", "streaming", "performance", "resilience", "security", "observability" };

            foreach (var demo in demos)
            {
                if (_demoMethods.TryGetValue(demo, out var demoMethod))
                {
                    await demoMethod(client);
                    await Task.Delay(500); // Small delay between demos
                }
            }
        }
    }
}
