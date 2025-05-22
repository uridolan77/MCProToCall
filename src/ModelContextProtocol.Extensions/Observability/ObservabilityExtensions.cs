using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Extension methods for configuring OpenTelemetry
    /// </summary>
    public static class ObservabilityExtensions
    {
        /// <summary>
        /// Adds MCP observability services to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="serviceName">Service name</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddMcpObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            string serviceName = "MCP-Service")
        {
            var otlpEndpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

            // Configure OpenTelemetry
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: serviceName)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("service.version", "1.0.0"),
                        new KeyValuePair<string, object>("deployment.environment",
                            configuration["Environment"] ?? "production")
                    }))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(McpTelemetry.ActivitySourceName)
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(McpTelemetry.MeterName)
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    }));

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddOpenTelemetry(logging =>
                {
                    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName));
                    logging.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                });
            });

            // Register telemetry service
            services.AddSingleton<IMcpTelemetry, McpTelemetry>();
            services.AddSingleton<TelemetryMiddleware>();

            // Add health checks
            services.AddHealthChecks()
                .AddCheck<McpHealthCheck>("mcp_server");

            return services;
        }
    }
}
