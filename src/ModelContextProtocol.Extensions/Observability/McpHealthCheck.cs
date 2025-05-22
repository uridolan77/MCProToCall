using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Health check service for MCP
    /// </summary>
    public class McpHealthCheck : IHealthCheck
    {
        private readonly IMcpServer _server;
        private readonly ILogger<McpHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpHealthCheck"/> class
        /// </summary>
        /// <param name="server">MCP server</param>
        /// <param name="logger">Logger</param>
        public McpHealthCheck(IMcpServer server, ILogger<McpHealthCheck> logger)
        {
            _server = server;
            _logger = logger;
        }

        /// <summary>
        /// Checks the health of the MCP server
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if server is responding
                var testRequest = new JsonRpcRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    Method = "system.ping",
                    Params = JsonDocument.Parse("{}").RootElement
                };

                var response = await _server.HandleRequestAsync(testRequest);
                
                if (response is JsonRpcErrorResponse)
                {
                    return HealthCheckResult.Degraded("Server is responding but returned an error");
                }

                return HealthCheckResult.Healthy("MCP server is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return HealthCheckResult.Unhealthy("MCP server is not responding", ex);
            }
        }
    }
}
