using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Exceptions;
using ModelContextProtocol.Core.Models.Mcp;
using System.Net.Sockets;

namespace ModelContextProtocol.Client
{
    /// <summary>
    /// MCP Client that communicates with MCP servers
    /// </summary>
    public class McpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly McpClientOptions _options;
        private readonly ILogger<McpClient> _logger;
        private readonly string _serverUrl;
        private bool _disposed;
        private int _requestId;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly System.Timers.Timer _rateLimitRefreshTimer;

        /// <summary>
        /// Initializes a new instance of the MCP Client
        /// </summary>
        /// <param name="options">Configuration options for the client</param>
        /// <param name="logger">Logger instance</param>
        public McpClient(McpClientOptions options, ILogger<McpClient> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure the server URL (HTTP or HTTPS)
            var protocol = options.UseTls ? "https" : "http";
            _serverUrl = $"{protocol}://{options.Host}:{options.Port}/";

            // Configure HTTP client with custom handler for TLS settings
            var handler = new HttpClientHandler();

            if (options.UseTls)
            {
                // Configure TLS certificate validation
                if (options.AllowUntrustedServerCertificate)
                {
                    _logger.LogWarning("Server certificate validation is disabled. This is not recommended for production use.");
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;
                }
                else if (options.ServerCertificateValidationCallback != null)
                {
                    // Convert RemoteCertificateValidationCallback to HttpClientHandler.ServerCertificateCustomValidationCallback
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                        options.ServerCertificateValidationCallback(message, cert, chain, errors);
                }
                else
                {
                    // Use our custom certificate validation
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                        ModelContextProtocol.Client.Security.TlsExtensions.ValidateServerCertificate(message, cert, chain, errors, _logger);
                }

                // Configure client certificate for mutual TLS
                if (options.ClientCertificate != null)
                {
                    handler.ClientCertificates.Add(options.ClientCertificate);
                    _logger.LogInformation("Using provided client certificate for mutual TLS");
                }
                else if (!string.IsNullOrEmpty(options.ClientCertificatePath))
                {
                    try
                    {
                        var clientCert = new X509Certificate2(
                            options.ClientCertificatePath,
                            options.ClientCertificatePassword);

                        handler.ClientCertificates.Add(clientCert);
                        _logger.LogInformation("Loaded client certificate from file for mutual TLS");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load client certificate from {Path}", options.ClientCertificatePath);
                        throw new McpException(McpException.ErrorCodes.InternalError, "Failed to load client certificate");
                    }
                }
            }

            _httpClient = new HttpClient(handler);

            // Set a default timeout of 30 seconds
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            if (!string.IsNullOrEmpty(options.AuthToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.AuthToken}");
            }

            // Configure rate limiting if enabled
            // For simplicity, we'll use a fixed rate limit of 60 requests per minute
            const int rateLimit = 60;
            _rateLimiter = new SemaphoreSlim(rateLimit);
            _rateLimitRefreshTimer = new System.Timers.Timer(60000); // 1 minute
            _rateLimitRefreshTimer.Elapsed += (sender, e) =>
            {
                int currentCount = _rateLimiter.CurrentCount;
                int toRelease = rateLimit - currentCount;
                if (toRelease > 0)
                {
                    _rateLimiter.Release(toRelease);
                }
            };
            _rateLimitRefreshTimer.Start();
        }

        /// <summary>
        /// Calls a method on the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected result type</typeparam>
        /// <param name="method">Method name</param>
        /// <param name="parameters">Method parameters</param>
        /// <returns>Method result</returns>
        public async Task<TResult> CallMethodAsync<TResult>(string method, object parameters = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(McpClient));

            if (string.IsNullOrEmpty(method))
                throw new ArgumentException("Method name cannot be null or empty", nameof(method));

            // Apply rate limiting if configured
            if (_rateLimiter != null)
            {
                await _rateLimiter.WaitAsync();
            }

            try
            {
                var requestId = Interlocked.Increment(ref _requestId).ToString();

                var request = new JsonRpcRequest
                {
                    Id = requestId,
                    Method = method,
                    Params = JsonDocument.Parse(JsonSerializer.Serialize(parameters ?? new { })).RootElement
                };

                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug("Sending request: {RequestJson}", requestJson);

                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                HttpResponseMessage response = null;
                string responseJson = null;

                // Simple implementation without retry logic
                try
                {
                    response = await _httpClient.PostAsync(_serverUrl, content);
                    response.EnsureSuccessStatusCode();
                    responseJson = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request failed");
                    throw;
                }

                _logger.LogDebug("Received response: {ResponseJson}", responseJson);

                // Check for error response
                var errorResponse = JsonSerializer.Deserialize<JsonRpcErrorResponse>(responseJson);
                if (errorResponse?.Error != null)
                {
                    throw new McpException(
                        errorResponse.Error.Code,
                        errorResponse.Error.Message,
                        null,
                        errorResponse.Error.Data);
                }

                // Parse success response
                var successResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);
                if (successResponse?.Result == null)
                {
                    throw new McpException(McpException.ErrorCodes.InternalError, "Invalid response format");
                }

                // Convert the result to the expected type
                return JsonSerializer.Deserialize<TResult>(successResponse.Result.ToString());
            }
            finally
            {
                // Release rate limiter permit
                if (_rateLimiter != null)
                {
                    _rateLimiter.Release();
                }
            }
        }

        /// <summary>
        /// Gets the capabilities of the MCP server
        /// </summary>
        /// <returns>Server capabilities</returns>
        public async Task<McpCapabilities> GetCapabilitiesAsync()
        {
            return await CallMethodAsync<McpCapabilities>("system.getCapabilities");
        }

        /// <summary>
        /// Retrieves a resource from the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected resource type</typeparam>
        /// <param name="resourceId">Resource identifier</param>
        /// <returns>Resource data</returns>
        public async Task<TResult> GetResourceAsync<TResult>(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));

            return await CallMethodAsync<TResult>("resources.get", new { resourceId });
        }

        /// <summary>
        /// Executes a tool on the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected result type</typeparam>
        /// <param name="toolId">Tool identifier</param>
        /// <param name="input">Tool input parameters</param>
        /// <returns>Tool execution result</returns>
        public async Task<TResult> ExecuteToolAsync<TResult>(string toolId, object input)
        {
            if (string.IsNullOrEmpty(toolId))
                throw new ArgumentException("Tool ID cannot be null or empty", nameof(toolId));

            return await CallMethodAsync<TResult>("tools.execute", new { toolId, input });
        }

        /// <summary>
        /// Refreshes the authorization token
        /// </summary>
        /// <returns>New authorization token</returns>
        public async Task<string> RefreshTokenAsync()
        {
            try
            {
                var response = await CallMethodAsync<AuthTokenResponse>("auth.refresh", new { });

                // Update the client with the new token
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {response.Token}");

                // Update the stored token
                _options.AuthToken = response.Token;

                return response.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh authorization token");
                throw new McpException(McpException.ErrorCodes.AuthenticationError, "Failed to refresh authorization token");
            }
        }

        /// <summary>
        /// Disposes the MCP client
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _httpClient.Dispose();
            _rateLimitRefreshTimer?.Stop();
            _rateLimitRefreshTimer?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Response from token refresh
    /// </summary>
    internal class AuthTokenResponse
    {
        /// <summary>
        /// New JWT token
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// New refresh token (if provided)
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Token expiration timestamp
        /// </summary>
        public long ExpiresAt { get; set; }
    }
}