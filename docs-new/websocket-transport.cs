using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Interfaces;

namespace ModelContextProtocol.Server.Transports
{
    public interface ITransport
    {
        Task<JsonRpcRequest> ReceiveRequestAsync(CancellationToken cancellationToken);
        Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken);
        Task CloseAsync();
    }

    public class WebSocketTransport : ITransport
    {
        private readonly WebSocket _webSocket;
        private readonly ILogger<WebSocketTransport> _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ArraySegment<byte> _receiveBuffer;
        private const int BufferSize = 4096;

        public WebSocketTransport(WebSocket webSocket, ILogger<WebSocketTransport> logger)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _receiveBuffer = new ArraySegment<byte>(new byte[BufferSize]);
        }

        public async Task<JsonRpcRequest> ReceiveRequestAsync(CancellationToken cancellationToken)
        {
            var messageBuilder = new StringBuilder();
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(_receiveBuffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageChunk = Encoding.UTF8.GetString(_receiveBuffer.Array, 0, result.Count);
                    messageBuilder.Append(messageChunk);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Client requested close", 
                        cancellationToken);
                    return null;
                }
            } while (!result.EndOfMessage);

            var message = messageBuilder.ToString();
            _logger.LogDebug("Received WebSocket message: {Message}", message);

            try
            {
                return JsonSerializer.Deserialize<JsonRpcRequest>(message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON-RPC request");
                throw;
            }
        }

        public async Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(response);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);

                _logger.LogDebug("Sent WebSocket response: {Response}", json);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task CloseAsync()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server closing connection",
                    CancellationToken.None);
            }
        }
    }

    // WebSocket-enabled MCP Server
    public class WebSocketMcpServer : IMcpServer
    {
        private readonly ILogger<WebSocketMcpServer> _logger;
        private readonly Dictionary<string, Func<JsonElement, Task<object>>> _methods;
        private readonly McpServerOptions _options;

        public WebSocketMcpServer(
            McpServerOptions options,
            ILogger<WebSocketMcpServer> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _methods = new Dictionary<string, Func<JsonElement, Task<object>>>();
        }

        public async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var transport = new WebSocketTransport(webSocket, _logger);

            try
            {
                while (!cancellationToken.IsCancellationRequested && 
                       webSocket.State == WebSocketState.Open)
                {
                    var request = await transport.ReceiveRequestAsync(cancellationToken);
                    if (request == null)
                        break;

                    var response = await HandleRequestAsync(request);
                    await transport.SendResponseAsync(response, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                await transport.CloseAsync();
            }
        }

        public void RegisterMethod(string methodName, Func<JsonElement, Task<object>> handler)
        {
            _methods[methodName] = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger.LogDebug("Registered WebSocket method: {MethodName}", methodName);
        }

        public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            if (!_methods.TryGetValue(request.Method, out var handler))
            {
                return new JsonRpcErrorResponse(
                    request.Id, 
                    -32601, 
                    "Method not found");
            }

            try
            {
                var result = await handler(request.Params);
                return new JsonRpcResponse(request.Id, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing method {Method}", request.Method);
                return new JsonRpcErrorResponse(
                    request.Id,
                    -32603,
                    "Internal error");
            }
        }

        // Other IMcpServer members...
        public Task StartAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
    }
}