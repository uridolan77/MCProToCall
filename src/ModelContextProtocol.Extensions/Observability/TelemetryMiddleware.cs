using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Middleware for automatic telemetry collection
    /// </summary>
    public class TelemetryMiddleware
    {
        private readonly IMcpTelemetry _telemetry;
        private readonly ILogger<TelemetryMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryMiddleware"/> class
        /// </summary>
        /// <param name="telemetry">Telemetry service</param>
        /// <param name="logger">Logger</param>
        public TelemetryMiddleware(
            IMcpTelemetry telemetry,
            ILogger<TelemetryMiddleware> logger)
        {
            _telemetry = telemetry;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        /// <param name="request">JSON-RPC request</param>
        /// <param name="next">Next middleware in the pipeline</param>
        /// <returns>JSON-RPC response</returns>
        public async Task<JsonRpcResponse> InvokeAsync(
            JsonRpcRequest request,
            Func<JsonRpcRequest, Task<JsonRpcResponse>> next)
        {
            var stopwatch = Stopwatch.StartNew();

            using var activity = _telemetry.StartActivity($"mcp.request.{request.Method}");
            activity?.SetTag("mcp.method", request.Method);
            activity?.SetTag("mcp.request_id", request.Id);

            _telemetry.RecordRequestReceived(request.Method);

            try
            {
                var response = await next(request);

                stopwatch.Stop();

                bool isSuccess = true;

                // Check if the response is an error response
                if (response is JsonRpcErrorResponse)
                {
                    isSuccess = false;

                    // We can't directly cast, so we need to extract the error information differently
                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);
                    var errorInfo = System.Text.Json.JsonSerializer.Deserialize<JsonRpcErrorResponse>(jsonResponse);

                    if (errorInfo != null && errorInfo.Error != null)
                    {
                        activity?.SetTag("mcp.error_code", errorInfo.Error.Code);
                        activity?.SetTag("mcp.error_message", errorInfo.Error.Message);
                    }
                }

                _telemetry.RecordRequestCompleted(request.Method, isSuccess, stopwatch.ElapsedMilliseconds);
                activity?.SetTag("mcp.status", isSuccess ? "success" : "error");

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _telemetry.RecordRequestCompleted(request.Method, false, stopwatch.ElapsedMilliseconds);
                _telemetry.RecordError(request.Method, ex.GetType().Name);

                activity?.SetTag("error", true);
                activity?.SetTag("error.type", ex.GetType().FullName);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                throw;
            }
        }
    }
}
