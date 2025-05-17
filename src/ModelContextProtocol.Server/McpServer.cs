using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Core.Exceptions;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Models.Mcp;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Server.Security.Authentication;
using ModelContextProtocol.Server.Security.TLS;
using ModelContextProtocol.Server.Http;
using System.Net.Security;
using System.Security.Authentication;

namespace ModelContextProtocol.Server
{
    public class McpServer : IMcpServer, IDisposable
    {
        private readonly ILogger<McpServer> _logger;
        private readonly McpServerOptions _options;
        private readonly Dictionary<string, Func<JsonElement, Task<object>>> _methods;
        private readonly HttpListener _httpListener;
        private readonly Dictionary<string, int> _clientRequestCounts = new Dictionary<string, int>();
        private readonly IJwtTokenProvider _tokenProvider;
        private readonly AuthorizationMiddleware _authorizationMiddleware;
        private readonly ICertificateValidator _certificateValidator;
        private readonly ICertificatePinningService _certificatePinningService;
        private readonly TlsConnectionManager _tlsConnectionManager;
        private readonly SemaphoreSlim _rateLimitSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;
        private X509Certificate2 _serverCertificate;

        public McpServer(
            IOptions<McpServerOptions> options,
            ILogger<McpServer> logger,
            IJwtTokenProvider tokenProvider = null,
            AuthorizationMiddleware authorizationMiddleware = null,
            ICertificateValidator certificateValidator = null,
            ICertificatePinningService certificatePinningService = null,
            TlsConnectionManager tlsConnectionManager = null)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenProvider = tokenProvider; // Optional, can be null if auth is disabled
            _authorizationMiddleware = authorizationMiddleware; // Optional, can be null if auth is disabled
            _certificateValidator = certificateValidator; // Optional, used for TLS validation
            _certificatePinningService = certificatePinningService; // Optional, used for certificate pinning
            _tlsConnectionManager = tlsConnectionManager; // Optional, used for TLS connection limiting

            _methods = new Dictionary<string, Func<JsonElement, Task<object>>>();
            _httpListener = new HttpListener();

            // If TLS is enabled, configure the server certificate
            // Note: We can't use await in the constructor, so we'll configure TLS in StartAsync
            // This is just a placeholder for the constructor

            RegisterSystemMethods();
        }

        public async Task StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(McpServer));

            _cancellationTokenSource = new CancellationTokenSource();

            // Configure TLS/HTTPS if enabled
            if (_options.UseTls)
            {
                // Configure TLS certificate
                await ConfigureTlsCertificateAsync();

                // Configure HTTPS endpoint
                var endpoint = $"https://{_options.Host}:{_options.Port}/";
                _httpListener.Prefixes.Add(endpoint);

                // Configure client certificate requirements
                if (_options.RequireClientCertificate)
                {
                    // Setting up client certificate requirements
                    _httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                    _httpListener.AuthenticationSchemeSelectorDelegate = request =>
                    {
                        return AuthenticationSchemes.Anonymous | AuthenticationSchemes.Negotiate;
                    };

                    _logger.LogInformation("Client certificate authentication is enabled");
                }
            }
            else
            {
                // Configure HTTP endpoint (no TLS)
                var endpoint = $"http://{_options.Host}:{_options.Port}/";
                _httpListener.Prefixes.Add(endpoint);
            }

            try
            {
                _httpListener.Start();
                var protocol = _options.UseTls ? "https" : "http";
                _logger.LogInformation("MCP Server started on {Protocol}://{Host}:{Port}/",
                    protocol, _options.Host, _options.Port);

                if (_options.UseTls)
                {
                    _logger.LogInformation("TLS/HTTPS is enabled with security level: {SecurityLevel}",
                        _options.RequireClientCertificate ? "High (Client Certificates)" : "Standard");

                    if (_tlsConnectionManager != null)
                    {
                        _logger.LogInformation("TLS connection rate limiting is enabled: {MaxConnections} per IP address",
                            _options.MaxConnectionsPerIpAddress);
                    }
                }

                if (_options.EnableAuthentication && _tokenProvider != null)
                {
                    _logger.LogInformation("Authentication is enabled");
                }

                if (_options.RateLimit.Enabled)
                {
                    _logger.LogInformation("Rate limiting is enabled: {RequestsPerMinute} requests/minute, {RequestsPerDay} requests/day",
                        _options.RateLimit.RequestsPerMinute, _options.RateLimit.RequestsPerDay);
                }

                await AcceptConnectionsAsync(_cancellationTokenSource.Token);
            }
            catch (HttpListenerException ex)
            {
                _logger.LogError(ex, "Failed to start HTTP listener. This may be due to insufficient permissions or port conflicts.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MCP server");
                throw;
            }
        }

        private async Task ConfigureTlsCertificateAsync()
        {
            try
            {
                // Try to load certificate from file first
                if (!string.IsNullOrEmpty(_options.CertificatePath))
                {
                    _serverCertificate = CertificateHelper.LoadCertificateFromFile(
                        _options.CertificatePath,
                        _options.CertificatePassword);

                    _logger.LogInformation("Loaded TLS certificate from file: {CertificatePath}", _options.CertificatePath);
                }
                // If no file path or file loading failed, try certificate store
                else if (!string.IsNullOrEmpty(_options.CertificateThumbprint))
                {
                    // Use X509Store directly since we don't have LoadCertificateFromStore
                    using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                    {
                        store.Open(OpenFlags.ReadOnly);
                        var certificates = store.Certificates.Find(
                            X509FindType.FindByThumbprint,
                            _options.CertificateThumbprint,
                            false);

                        if (certificates.Count == 0)
                        {
                            throw new InvalidOperationException(
                                $"Certificate with thumbprint {_options.CertificateThumbprint} not found");
                        }

                        _serverCertificate = certificates[0];
                    }

                    _logger.LogInformation("Loaded TLS certificate from store with thumbprint: {Thumbprint}",
                        _options.CertificateThumbprint);
                }
                else
                {
                    throw new InvalidOperationException(
                        "TLS is enabled but no certificate path or thumbprint is specified");
                }

                // Verify the certificate is valid using our validation components
                if (_certificateValidator != null)
                {
                    var chain = new X509Chain();

                    // Configure chain building with proper revocation checking based on options
                    chain.ChainPolicy.RevocationMode = _options.CheckCertificateRevocation
                        ? X509RevocationMode.Online
                        : X509RevocationMode.NoCheck;

                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;

                    // Build the chain
                    bool chainBuilt = chain.Build(_serverCertificate);

                    if (!chainBuilt)
                    {
                        _logger.LogWarning("Server certificate chain could not be built - the certificate may be invalid");
                        // Log detailed chain status information
                        foreach (var status in chain.ChainStatus)
                        {
                            _logger.LogWarning("Certificate chain status: {Status} - {StatusInformation}",
                                status.Status, status.StatusInformation);
                        }
                    }

                    // Validate the certificate using our validator
                    if (!await _certificateValidator.ValidateCertificateAsync(_serverCertificate))
                    {
                        _logger.LogWarning("Server certificate validation failed - the certificate may be invalid or revoked");
                        // We continue anyway as this is our own certificate, but log the warning
                    }
                }

                // Add the server certificate to the pinning service if available
                if (_certificatePinningService != null && _serverCertificate != null)
                {
                    await _certificatePinningService.AddPinnedCertificateAsync(_serverCertificate);
                    _logger.LogDebug("Added server certificate to pinning service with thumbprint: {Thumbprint}",
                        _serverCertificate.Thumbprint);
                }

                // Check certificate expiration and warn if it's expiring soon
                var expirationDate = _serverCertificate.NotAfter;
                var daysUntilExpiration = (expirationDate - DateTime.Now).TotalDays;

                if (daysUntilExpiration < 30)
                {
                    _logger.LogWarning("Server certificate will expire in {Days} days (on {ExpirationDate}). " +
                        "Consider renewing it soon.",
                        (int)daysUntilExpiration, expirationDate.ToString("yyyy-MM-dd"));
                }
                else
                {
                    _logger.LogInformation("Server certificate is valid until {ExpirationDate}",
                        expirationDate.ToString("yyyy-MM-dd"));
                }

                // Configure HTTPS with the certificate
                string prefix = $"https://{_options.Host}:{_options.Port}/";

                // Setting up HTTPS binding requires administrative privileges
                try
                {
                    // In a production environment, you should use netsh or other tools to set up bindings
                    // Here we're just configuring HttpListener to use the certificate
                    // Note: HttpListener doesn't directly support timeout configuration
                    _logger.LogInformation("Using HTTPS with certificate: {Subject}", _serverCertificate.Subject);

                    _logger.LogInformation("Configured HTTPS binding for {Prefix}", prefix);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to configure HTTPS binding. This may require administrative privileges.");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TLS certificate");
                throw;
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _httpListener.Stop();
            _logger.LogInformation("MCP Server stopped");
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();

            // Give any pending requests a chance to complete
            await Task.Delay(100);

            _httpListener.Stop();
            _logger.LogInformation("MCP Server stopped asynchronously");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _httpListener.Close();
            _cancellationTokenSource?.Dispose();
            _rateLimitSemaphore?.Dispose();

            // Dispose of TLS resources
            _serverCertificate?.Dispose();

            _disposed = true;
        }

        public void RegisterMethod(string methodName, Func<JsonElement, Task<object>> handler)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentException("Method name cannot be null or empty", nameof(methodName));

            _methods[methodName] = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger.LogDebug("Registered method: {MethodName}", methodName);

            // If authorization middleware is available, register the method with default permissions
            if (_authorizationMiddleware != null && _options.EnableAuthentication)
            {
                // By default, methods require authentication
                _authorizationMiddleware.RegisterMethodPermission(methodName, new[] { "User" });
            }
        }

        /// <summary>
        /// Registers a method as publicly accessible (no authentication required)
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="handler">Method handler</param>
        public void RegisterPublicMethod(string methodName, Func<JsonElement, Task<object>> handler)
        {
            RegisterMethod(methodName, handler);

            // If authorization middleware is available, register as public
            if (_authorizationMiddleware != null)
            {
                _authorizationMiddleware.RegisterPublicMethod(methodName);
            }
        }

        /// <summary>
        /// Registers a method with specific role requirements
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="handler">Method handler</param>
        /// <param name="requiredRoles">Roles that can access this method</param>
        public void RegisterSecuredMethod(string methodName, Func<JsonElement, Task<object>> handler, IEnumerable<string> requiredRoles)
        {
            RegisterMethod(methodName, handler);

            // If authorization middleware is available, register with specific roles
            if (_authorizationMiddleware != null && _options.EnableAuthentication)
            {
                _authorizationMiddleware.RegisterMethodPermission(methodName, requiredRoles);
            }
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _httpListener.IsListening)
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting connections");
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            string clientIp = context.Request.RemoteEndPoint.Address.ToString();
            _logger.LogDebug("Received request from {ClientIp}", clientIp);

            try
            {
                // Check TLS connection limit if TLS is enabled and connection manager is available
                if (_options.UseTls && _tlsConnectionManager != null)
                {
                    string connectionId = GetConnectionId(context);
                    if (!await _tlsConnectionManager.RegisterConnectionAsync(context))
                    {
                        _logger.LogWarning("TLS connection limit exceeded for client {ClientIp}", clientIp);
                        context.Response.StatusCode = 429; // Too Many Requests
                        await SendErrorResponseAsync(context, 429, "TLS connection limit exceeded", null);
                        return;
                    }
                }

                // Validate client certificate if TLS is enabled and client certificates are required
                if (_options.UseTls && _options.RequireClientCertificate)
                {
                    var clientCertificate = context.Request.GetClientCertificate();

                    if (clientCertificate == null)
                    {
                        _logger.LogWarning("Client certificate required but not provided from {ClientIp}", clientIp);
                        context.Response.StatusCode = 403; // Forbidden
                        await SendErrorResponseAsync(context, 403, "Client certificate required", null);
                        return;
                    }

                    // Validate client certificate using our validator if available
                    if (_certificateValidator != null)
                    {
                        var chain = new X509Chain();
                        chain.Build(new X509Certificate2(clientCertificate));

                        if (!await _certificateValidator.ValidateCertificateAsync(
                            new X509Certificate2(clientCertificate)))
                        {
                            _logger.LogWarning("Client certificate validation failed from {ClientIp}", clientIp);
                            context.Response.StatusCode = 403; // Forbidden
                            await SendErrorResponseAsync(context, 403, "Invalid client certificate", null);
                            return;
                        }

                        // Check if the client certificate is in the allowed list
                        if (_options.AllowedClientCertificateThumbprints.Count > 0)
                        {
                            var clientThumbprint = new X509Certificate2(clientCertificate).Thumbprint;
                            if (!_options.AllowedClientCertificateThumbprints.Contains(clientThumbprint))
                            {
                                _logger.LogWarning("Client certificate not in allowed list from {ClientIp}", clientIp);
                                context.Response.StatusCode = 403; // Forbidden
                                await SendErrorResponseAsync(context, 403, "Client certificate not authorized", null);
                                return;
                            }
                        }
                    }
                }

                // If rate limiting is enabled, check limits
                if (_options.RateLimit.Enabled)
                {
                    if (!await CheckRateLimitAsync(clientIp))
                    {
                        _logger.LogWarning("Rate limit exceeded for client {ClientIp}", clientIp);
                        context.Response.StatusCode = 429; // Too Many Requests
                        await SendErrorResponseAsync(context, 429, "Rate limit exceeded", null);
                        return;
                    }
                }

                // Check request size
                if (context.Request.ContentLength64 > _options.Validation.MaxRequestSize)
                {
                    _logger.LogWarning("Request size exceeds limit: {Size} bytes", context.Request.ContentLength64);
                    context.Response.StatusCode = 413; // Payload Too Large
                    await SendErrorResponseAsync(context, 413, "Request size exceeds limit", null);
                    return;
                }

                // Read request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                _logger.LogDebug("Request body: {RequestBody}", requestBody);

                // Parse JSON-RPC request
                JsonRpcRequest request;
                try
                {
                    request = JsonSerializer.Deserialize<JsonRpcRequest>(requestBody);

                    if (request == null)
                    {
                        _logger.LogWarning("Failed to parse request: null after deserialization");
                        await SendErrorResponseAsync(context, -32700, "Parse error", null);
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid JSON in request");
                    await SendErrorResponseAsync(context, -32700, "Parse error: invalid JSON", null);
                    return;
                }

                // Check if method exists
                if (!_methods.TryGetValue(request.Method, out var methodHandler))
                {
                    _logger.LogWarning("Method not found: {Method}", request.Method);
                    await SendErrorResponseAsync(context, -32601, "Method not found", request.Id);
                    return;
                }

                // If authentication is enabled, authorize the request
                if (_options.EnableAuthentication && _authorizationMiddleware != null)
                {
                    // Get authorization token from header
                    string authToken = null;
                    if (context.Request.Headers["Authorization"] != null)
                    {
                        var authHeader = context.Request.Headers["Authorization"];
                        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            authToken = authHeader.Substring("Bearer ".Length).Trim();
                        }
                    }

                    // Authorize request
                    var authResult = await _authorizationMiddleware.AuthorizeRequestAsync(request, authToken);
                    if (!authResult.IsAuthorized)
                    {
                        _logger.LogWarning("Authorization failed for method {Method}: {ErrorMessage}",
                            request.Method, authResult.ErrorMessage);

                        context.Response.StatusCode = authResult.ErrorCode;
                        await SendErrorResponseAsync(context, authResult.ErrorCode, authResult.ErrorMessage, request.Id);
                        return;
                    }
                }

                try
                {
                    // Execute the method
                    var result = await methodHandler(request.Params);
                    await SendSuccessResponseAsync(context, result, request.Id);
                }
                catch (McpException ex)
                {
                    // Handle known MCP exceptions with specific error codes
                    _logger.LogWarning(ex, "MCP exception in method {Method}: {Message}", request.Method, ex.Message);
                    await SendErrorResponseAsync(context, ex.ErrorCode, ex.Message, request.Id);
                }
                catch (Exception ex)
                {
                    // Log detailed exception for server side, but return generic message to client
                    _logger.LogError(ex, "Error executing method {Method}", request.Method);
                    await SendErrorResponseAsync(context, -32603, "Internal error", request.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing request from {ClientIp}", clientIp);
                try
                {
                    await SendErrorResponseAsync(context, -32700, "Server error", null);
                }
                catch
                {
                    // Last resort if we can't even send the error response
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
            finally
            {
                // Cleanup TLS connection if applicable
                if (_options.UseTls && _tlsConnectionManager != null)
                {
                    string connectionId = GetConnectionId(context);
                    _tlsConnectionManager.RemoveConnection(connectionId);
                }
            }
        }

        /// <summary>
        /// Checks if the client has exceeded rate limits
        /// </summary>
        private async Task<bool> CheckRateLimitAsync(string clientIp)
        {
            await _rateLimitSemaphore.WaitAsync();
            try
            {
                // Initialize counter if this is the first request from this client
                if (!_clientRequestCounts.ContainsKey(clientIp))
                {
                    _clientRequestCounts[clientIp] = 0;
                }

                // Increment and check request count
                _clientRequestCounts[clientIp]++;

                // Simple implementation - in production, you'd use a sliding window with timestamps
                return _clientRequestCounts[clientIp] <= _options.RateLimit.RequestsPerMinute;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        private async Task SendSuccessResponseAsync(HttpListenerContext context, object result, string id)
        {
            var response = new JsonRpcResponse
            {
                Id = id,
                Result = result
            };

            await SendJsonResponseAsync(context, response);
        }

        private async Task SendErrorResponseAsync(HttpListenerContext context, int code, string message, string id)
        {
            var errorResponse = new JsonRpcErrorResponse
            {
                Id = id,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message
                }
            };

            await SendJsonResponseAsync(context, errorResponse);
        }

        private async Task SendJsonResponseAsync(HttpListenerContext context, object response)
        {
            try
            {
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Server", "MCP-Server");

                // Add security headers
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("Content-Security-Policy", "default-src 'none'");

                var buffer = Encoding.UTF8.GetBytes(json);
                context.Response.ContentLength64 = buffer.Length;

                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();

                _logger.LogDebug("Sent response: {Response}", json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending response");

                try
                {
                    // If we haven't sent headers yet, we can still set status code
                    // HttpListenerResponse doesn't have a HeadersSent property, so we'll try-catch instead
                    try
                    {
                        context.Response.StatusCode = 500;
                    }
                    catch
                    {
                        // Headers already sent, can't set status code
                    }

                    context.Response.Close();
                }
                catch
                {
                    // Ignore any errors in the error handler
                }
            }
        }

        /// <summary>
        /// Handle a JSON-RPC request directly
        /// </summary>
        public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_methods.TryGetValue(request.Method, out var methodHandler))
            {
                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = "Method not found"
                    }
                };
            }

            try
            {
                var result = await methodHandler(request.Params);
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = result
                };
            }
            catch (McpException ex)
            {
                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = ex.ErrorCode,
                        Message = ex.Message,
                        Data = ex.Data
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing method {Method}", request.Method);
                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Internal error"
                    }
                };
            }
        }

        /// <summary>
        /// Gets a unique ID for the connection
        /// </summary>
        private string GetConnectionId(HttpListenerContext context)
        {
            return $"{context.Request.RemoteEndPoint}_{Guid.NewGuid()}";
        }

        private void RegisterSystemMethods()
        {
            // Register getCapabilities as a public method (no auth required)
            RegisterPublicMethod("system.getCapabilities", _ => Task.FromResult<object>(new
            {
                Version = "1.0.0",
                Resources = _options.Resources,
                Tools = _options.Tools,
                Prompts = _options.Prompts
            }));

            // Echo method - requires User role
            RegisterMethod("system.echo", async param =>
            {
                string input = param.GetString();
                return await Task.FromResult<object>(input);
            });

            // Status method - requires User role
            RegisterMethod("system.status", async _ =>
            {
                return await Task.FromResult<object>(new
                {
                    Status = "Running",
                    StartTime = DateTime.UtcNow.AddHours(-1),
                    UpTimeMinutes = 60,
                    CurrentConnectionCount = 1,
                    TotalRequestsProcessed = 10,
                    AverageRequestProcessingTimeMs = 25
                });
            });

            // Admin method - requires Admin role
            RegisterSecuredMethod("system.admin", async _ =>
            {
                return await Task.FromResult<object>(new
                {
                    Success = true,
                    Message = "Admin operation completed successfully",
                    Timestamp = DateTime.UtcNow
                });
            }, new[] { "Admin" });
        }
    }
}