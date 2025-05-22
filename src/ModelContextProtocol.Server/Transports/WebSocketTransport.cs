using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Server.Transports
{
    /// <summary>
    /// WebSocket transport implementation
    /// </summary>
    public class WebSocketTransport : ITransport
    {
        private readonly WebSocket _webSocket;
        private readonly ILogger<WebSocketTransport> _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ArraySegment<byte> _receiveBuffer;
        private const int BufferSize = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketTransport"/> class
        /// </summary>
        /// <param name="webSocket">WebSocket instance</param>
        /// <param name="logger">Logger</param>
        public WebSocketTransport(WebSocket webSocket, ILogger<WebSocketTransport> logger)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _receiveBuffer = new ArraySegment<byte>(new byte[BufferSize]);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task SendNotificationAsync(object notification, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(notification);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);

                _logger.LogDebug("Sent WebSocket notification: {Notification}", json);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <inheritdoc/>
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
}
