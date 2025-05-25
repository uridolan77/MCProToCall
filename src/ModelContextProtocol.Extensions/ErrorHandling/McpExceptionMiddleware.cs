using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Observability;
using System;
using System.Net.Http;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.ErrorHandling
{
    /// <summary>
    /// Global exception handler middleware for MCP applications
    /// </summary>
    public class McpExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<McpExceptionMiddleware> _logger;
        private readonly IMcpTelemetry _telemetry;

        public McpExceptionMiddleware(
            RequestDelegate next,
            ILogger<McpExceptionMiddleware> logger,
            IMcpTelemetry telemetry)
        {
            _next = next;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogError(exception, "Unhandled exception {ErrorId}: {Message}", errorId, exception.Message);
            _telemetry.RecordError("global_exception", exception.GetType().Name);

            var response = CreateErrorResponse(exception, errorId);
            var statusCode = GetStatusCode(exception);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }

        private ErrorResponse CreateErrorResponse(Exception exception, string errorId)
        {
            return exception switch
            {
                McpException mcpEx => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = mcpEx.ErrorCode,
                    Message = mcpEx.Message,
                    Data = mcpEx.ErrorData,
                    Timestamp = DateTime.UtcNow
                },
                SecurityException secEx => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "SECURITY_ERROR",
                    Message = "A security error occurred",
                    Timestamp = DateTime.UtcNow
                },
                UnauthorizedAccessException => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "UNAUTHORIZED",
                    Message = "Access denied",
                    Timestamp = DateTime.UtcNow
                },
                ArgumentException argEx => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "INVALID_ARGUMENT",
                    Message = argEx.Message,
                    Data = new { ParameterName = argEx.ParamName },
                    Timestamp = DateTime.UtcNow
                },
                TimeoutException => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "TIMEOUT",
                    Message = "The operation timed out",
                    Timestamp = DateTime.UtcNow
                },
                TaskCanceledException => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "OPERATION_CANCELLED",
                    Message = "The operation was cancelled",
                    Timestamp = DateTime.UtcNow
                },
                _ => new ErrorResponse
                {
                    ErrorId = errorId,
                    ErrorCode = "INTERNAL_ERROR",
                    Message = "An internal error occurred",
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        private static int GetStatusCode(Exception exception) => exception switch
        {
            SecurityException => 403,
            UnauthorizedAccessException => 401,
            ArgumentNullException => 400,
            ArgumentException => 400,
            TimeoutException => 408,
            TaskCanceledException => 408,
            RateLimitExceededException => 429,
            CircuitBreakerOpenException => 503,
            ConfigurationValidationException => 500,
            HsmOperationException => 500,
            WebSocketOperationException => 400,
            ProtocolNegotiationException => 400,
            ResourceOperationException => 404,
            McpException mcpEx when mcpEx.ErrorCode.StartsWith("TRANSIENT_") => 503,
            McpException => 400,
            _ => 500
        };
    }

    /// <summary>
    /// Standard error response format
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Unique identifier for this error instance
        /// </summary>
        public string ErrorId { get; set; }

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Additional error data
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional trace identifier for correlation
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Optional details for debugging (only in development)
        /// </summary>
        public object Details { get; set; }
    }

    /// <summary>
    /// Extension methods for adding exception handling middleware
    /// </summary>
    public static class McpExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Adds the MCP exception handling middleware to the pipeline
        /// </summary>
        public static IApplicationBuilder UseMcpExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<McpExceptionMiddleware>();
        }
    }

    /// <summary>
    /// Retry policies with exponential backoff
    /// </summary>
    public static class RetryPolicies
    {
        /// <summary>
        /// Executes an operation with retry logic and exponential backoff
        /// </summary>
        public static async Task<Result<T>> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            TimeSpan baseDelay = default,
            CancellationToken cancellationToken = default)
        {
            baseDelay = baseDelay == default ? TimeSpan.FromSeconds(1) : baseDelay;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();
                    return Result<T>.Success(result);
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    return Result<T>.Failure(ex);
                }
            }

            return Result<T>.Failure("Maximum retry attempts exceeded");
        }

        /// <summary>
        /// Executes an operation with retry logic (non-generic version)
        /// </summary>
        public static async Task<Result> ExecuteWithRetryAsync(
            Func<Task> operation,
            int maxRetries = 3,
            TimeSpan baseDelay = default,
            CancellationToken cancellationToken = default)
        {
            baseDelay = baseDelay == default ? TimeSpan.FromSeconds(1) : baseDelay;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return Result.Success();
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    return Result.Failure(ex);
                }
            }

            return Result.Failure("Maximum retry attempts exceeded");
        }

        /// <summary>
        /// Determines if an exception is transient and should be retried
        /// </summary>
        private static bool IsTransientException(Exception ex) =>
            ex is TimeoutException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is System.Net.Sockets.SocketException ||
            (ex is McpException mcpEx && mcpEx.ErrorCode.StartsWith("TRANSIENT_")) ||
            (ex is HsmOperationException hsmEx && IsTransientHsmError(hsmEx));

        private static bool IsTransientHsmError(HsmOperationException hsmEx)
        {
            // Define which HSM errors are considered transient
            return hsmEx.Operation switch
            {
                "GetCertificate" => true,
                "SignData" => true,
                "VerifySignature" => false, // Verification failures are usually not transient
                _ => false
            };
        }
    }
}
