using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Utilities
{
    /// <summary>
    /// Utility methods for HTTP operations
    /// </summary>
    public static class HttpUtilities
    {
        /// <summary>
        /// Determines if an HTTP status code represents a transient error
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <returns>True if the status code represents a transient error, false otherwise</returns>
        public static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            // 408 Request Timeout
            // 429 Too Many Requests
            // 5xx Server errors
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == (HttpStatusCode)429 ||
                   (int)statusCode >= 500 && (int)statusCode < 600;
        }

        /// <summary>
        /// Determines if an exception represents a transient error
        /// </summary>
        /// <param name="exception">The exception</param>
        /// <returns>True if the exception represents a transient error, false otherwise</returns>
        public static bool IsTransientException(Exception exception)
        {
            // Check for common transient exceptions
            if (exception is HttpRequestException ||
                exception is TimeoutException ||
                exception is TaskCanceledException ||
                exception is OperationCanceledException)
            {
                return true;
            }
            
            // Check for WebException with a transient status code
            if (exception is WebException webException && 
                webException.Response is HttpWebResponse response)
            {
                return IsTransientStatusCode(response.StatusCode);
            }
            
            // Check for inner exceptions
            if (exception.InnerException != null)
            {
                return IsTransientException(exception.InnerException);
            }
            
            return false;
        }

        /// <summary>
        /// Creates a sanitized version of an HTTP request for logging
        /// </summary>
        /// <param name="request">The HTTP request message</param>
        /// <returns>A sanitized string representation of the request</returns>
        public static string SanitizeHttpRequest(HttpRequestMessage request)
        {
            if (request == null)
            {
                return "null";
            }
            
            var sb = new StringBuilder();
            
            // Add method and URL
            sb.AppendLine($"{request.Method} {StringUtilities.SanitizeUrl(request.RequestUri?.ToString() ?? "null")}");
            
            // Add headers (excluding sensitive ones)
            sb.AppendLine("Headers:");
            foreach (var header in request.Headers)
            {
                // Skip sensitive headers
                if (IsSensitiveHeader(header.Key))
                {
                    sb.AppendLine($"  {header.Key}: *****");
                }
                else
                {
                    sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
            }
            
            // Add content headers if available
            if (request.Content != null)
            {
                sb.AppendLine("Content Headers:");
                foreach (var header in request.Content.Headers)
                {
                    sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                
                // Add content type and length
                sb.AppendLine($"Content Type: {request.Content.Headers.ContentType}");
                sb.AppendLine($"Content Length: {request.Content.Headers.ContentLength}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Creates a sanitized version of an HTTP response for logging
        /// </summary>
        /// <param name="response">The HTTP response message</param>
        /// <param name="includeContent">Whether to include the response content</param>
        /// <param name="maxContentLength">The maximum content length to include</param>
        /// <returns>A sanitized string representation of the response</returns>
        public static async Task<string> SanitizeHttpResponseAsync(
            HttpResponseMessage response, 
            bool includeContent = false, 
            int maxContentLength = 1000)
        {
            if (response == null)
            {
                return "null";
            }
            
            var sb = new StringBuilder();
            
            // Add status code
            sb.AppendLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
            
            // Add headers
            sb.AppendLine("Headers:");
            foreach (var header in response.Headers)
            {
                // Skip sensitive headers
                if (IsSensitiveHeader(header.Key))
                {
                    sb.AppendLine($"  {header.Key}: *****");
                }
                else
                {
                    sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
            }
            
            // Add content headers if available
            if (response.Content != null)
            {
                sb.AppendLine("Content Headers:");
                foreach (var header in response.Content.Headers)
                {
                    sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                
                // Add content if requested
                if (includeContent)
                {
                    try
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        sb.AppendLine("Content:");
                        sb.AppendLine(StringUtilities.Truncate(content, maxContentLength));
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Error reading content: {ex.Message}");
                    }
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Logs an HTTP request at the specified log level
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="request">The HTTP request message</param>
        /// <param name="logLevel">The log level</param>
        public static void LogHttpRequest(ILogger logger, HttpRequestMessage request, LogLevel logLevel = LogLevel.Debug)
        {
            logger.Log(logLevel, "HTTP Request:\n{Request}", SanitizeHttpRequest(request));
        }

        /// <summary>
        /// Logs an HTTP response at the specified log level
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="response">The HTTP response message</param>
        /// <param name="includeContent">Whether to include the response content</param>
        /// <param name="maxContentLength">The maximum content length to include</param>
        /// <param name="logLevel">The log level</param>
        public static async Task LogHttpResponseAsync(
            ILogger logger, 
            HttpResponseMessage response, 
            bool includeContent = false, 
            int maxContentLength = 1000, 
            LogLevel logLevel = LogLevel.Debug)
        {
            logger.Log(logLevel, "HTTP Response:\n{Response}", 
                await SanitizeHttpResponseAsync(response, includeContent, maxContentLength));
        }

        /// <summary>
        /// Determines if an HTTP header is sensitive
        /// </summary>
        /// <param name="headerName">The header name</param>
        /// <returns>True if the header is sensitive, false otherwise</returns>
        private static bool IsSensitiveHeader(string headerName)
        {
            var sensitiveHeaders = new[]
            {
                "Authorization",
                "X-API-Key",
                "API-Key",
                "X-Auth-Token",
                "Authentication",
                "Cookie",
                "Set-Cookie",
                "X-CSRF-Token",
                "X-XSRF-Token"
            };
            
            return sensitiveHeaders.Any(h => string.Equals(h, headerName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
