using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Diagnostics;
using ModelContextProtocol.Extensions.Security.Pipeline.Steps;

namespace ModelContextProtocol.Samples.EnhancedSample
{
    /// <summary>
    /// Enhanced MCP server example demonstrating all new features
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure comprehensive MCP server with all enhancements
            builder.Services.AddMcpServer(server => server
                // Configure TLS with comprehensive security
                .UseTls(tls => tls
                    .WithCertificate("./certs/server.pfx", "certificate-password")
                    .RequireClientCertificate()
                    .WithMinimumTlsVersion("1.3")
                    .EnableCertificatePinning(pinning => pinning
                        .PinCertificates(
                            "A1B2C3D4E5F6789012345678901234567890ABCD",
                            "B2C3D4E5F6789012345678901234567890ABCDE1")
                        .AllowSelfSignedIfPinned())
                    .UseValidationPipeline(pipeline => pipeline
                        .AddExpiryValidation()
                        .AddKeyUsageValidation()
                        .AddRevocationValidation()
                        .AddTransparencyValidation()
                        .AddPinningValidation())
                    .UseHsm(hsm => hsm
                        .UseAzureKeyVault("https://your-keyvault.vault.azure.net/")
                        .WithCertificate("server-certificate")
                        .WithSigningKey("signing-key")
                        .WithEncryptionKey("encryption-key")))

                // Configure authentication
                .UseAuthentication(auth => auth
                    .AddJwtBearer(jwt => jwt
                        .WithSecret("your-jwt-secret")
                        .WithIssuer("https://your-identity-provider.com")
                        .WithAudience("mcp-api"))
                    .AddApiKey(apiKey => apiKey
                        .WithHeaderName("X-API-Key")
                        .WithKeys("api-key-1", "api-key-2"))
                    .AddCertificate())

                // Configure rate limiting
                .UseRateLimiting(limits => limits
                    .WithAdaptivePolicy()
                    .WithMaxRequestsPerMinute(1000)
                    .WithMaxRequestsPerHour(50000)
                    .WithBurstAllowance(100))

                // Configure resilience patterns
                .UseResilience(resilience => resilience
                    .UseBulkhead(bulkhead => bulkhead
                        .WithMaxConcurrentExecutions(100)
                        .WithMaxQueueSize(1000)
                        .WithQueueTimeout(TimeSpan.FromSeconds(30)))
                    .UseHedging(hedging => hedging
                        .WithDelay(TimeSpan.FromMilliseconds(100))
                        .WithMaxHedgedRequests(2)
                        .ForOperations("tools/call", "resources/read", "prompts/get"))
                    .UseCircuitBreaker(cb => cb
                        .WithFailureThreshold(5)
                        .WithTimeout(TimeSpan.FromMinutes(1)))
                    .UseRetry(retry => retry
                        .WithMaxAttempts(3)
                        .WithExponentialBackoff(TimeSpan.FromSeconds(1))))

                // Configure protocol support
                .UseProtocols(protocols => protocols
                    .AddJsonRpc()
                    .AddMessagePack()
                    .AddGrpc()
                    .EnableNegotiation()
                    .WithDefaultProtocol("json-rpc"))

                // Add observability
                .AddHealthChecks()
                .AddMetrics()
                .AddTracing());

            // Add diagnostics service
            builder.Services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
            builder.Services.AddSingleton<DiagnosticMetricsCollector>();

            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("https://localhost:3000", "https://your-frontend-domain.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Add controllers for API endpoints
            builder.Services.AddControllers();

            // Configure logging
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors();

            // Add security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.Add("Content-Security-Policy", 
                    "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
                
                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            // Map health checks
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/health/ready");
            app.MapHealthChecks("/health/live");

            // Map diagnostics endpoint
            app.MapGet("/diagnostics", async (IDiagnosticsService diagnostics) =>
            {
                var report = await diagnostics.GenerateReportAsync();
                return Results.Ok(report);
            });

            // Map metrics endpoint
            app.MapGet("/metrics", (DiagnosticMetricsCollector collector) =>
            {
                var summary = collector.GetSummary();
                return Results.Ok(summary);
            });

            // Map controllers
            app.MapControllers();

            // Example MCP endpoints
            app.MapPost("/mcp/tools/call", async (ToolCallRequest request, IDiagnosticsService diagnostics) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    // Simulate tool execution
                    await Task.Delay(100);
                    
                    var response = new ToolCallResponse
                    {
                        ToolName = request.ToolName,
                        Result = $"Tool '{request.ToolName}' executed successfully",
                        ExecutionTime = stopwatch.Elapsed
                    };

                    diagnostics.RecordRequest(stopwatch.Elapsed, false);
                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    diagnostics.RecordRequest(stopwatch.Elapsed, true);
                    return Results.Problem($"Tool execution failed: {ex.Message}");
                }
            });

            app.MapGet("/mcp/resources/{resourceId}", async (string resourceId, IDiagnosticsService diagnostics) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    // Simulate resource retrieval
                    await Task.Delay(50);
                    
                    var resource = new ResourceResponse
                    {
                        ResourceId = resourceId,
                        Content = $"Content for resource '{resourceId}'",
                        ContentType = "text/plain",
                        LastModified = DateTime.UtcNow
                    };

                    diagnostics.RecordRequest(stopwatch.Elapsed, false);
                    return Results.Ok(resource);
                }
                catch (Exception ex)
                {
                    diagnostics.RecordRequest(stopwatch.Elapsed, true);
                    return Results.Problem($"Resource retrieval failed: {ex.Message}");
                }
            });

            app.MapGet("/mcp/prompts/{promptId}", async (string promptId, IDiagnosticsService diagnostics) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    // Simulate prompt retrieval
                    await Task.Delay(25);
                    
                    var prompt = new PromptResponse
                    {
                        PromptId = promptId,
                        Template = $"This is prompt template for '{promptId}'",
                        Parameters = new Dictionary<string, object>
                        {
                            ["param1"] = "value1",
                            ["param2"] = "value2"
                        }
                    };

                    diagnostics.RecordRequest(stopwatch.Elapsed, false);
                    return Results.Ok(prompt);
                }
                catch (Exception ex)
                {
                    diagnostics.RecordRequest(stopwatch.Elapsed, true);
                    return Results.Problem($"Prompt retrieval failed: {ex.Message}");
                }
            });

            app.Run();
        }
    }

    // Request/Response models
    public class ToolCallRequest
    {
        public string ToolName { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ToolCallResponse
    {
        public string ToolName { get; set; }
        public string Result { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class ResourceResponse
    {
        public string ResourceId { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class PromptResponse
    {
        public string PromptId { get; set; }
        public string Template { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
