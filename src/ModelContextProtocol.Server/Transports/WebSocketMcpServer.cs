using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Streaming;

namespace ModelContextProtocol.Server.Transports
{
    /// <summary>
    /// WebSocket-enabled MCP Server
    /// </summary>
    public class WebSocketMcpServer
    {
        private readonly IMcpServer _mcpServer;
        private readonly ILogger<WebSocketMcpServer> _logger;
        private readonly StreamingResponseManager _streamingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketMcpServer"/> class
        /// </summary>
        /// <param name="mcpServer">MCP server instance</param>
        /// <param name="streamingManager">Streaming manager</param>
        /// <param name="logger">Logger</param>
        public WebSocketMcpServer(
            IMcpServer mcpServer,
            StreamingResponseManager streamingManager,
            ILogger<WebSocketMcpServer> logger)
        {
            _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
            _streamingManager = streamingManager ?? throw new ArgumentNullException(nameof(streamingManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles a WebSocket connection
        /// </summary>
        /// <param name="webSocket">WebSocket instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var transportLogger = new Logger<WebSocketTransport>(
                _logger.GetType().GetProperty("LoggerFactory").GetValue(_logger) as ILoggerFactory);
            var transport = new WebSocketTransport(webSocket, transportLogger);

            try
            {
                _logger.LogInformation("WebSocket connection established");

                while (!cancellationToken.IsCancellationRequested &&
                       webSocket.State == WebSocketState.Open)
                {
                    var request = await transport.ReceiveRequestAsync(cancellationToken);
                    if (request == null)
                        break;

                    // Check if this is a streaming method request
                    if (request.Method.StartsWith("stream."))
                    {
                        await HandleStreamingRequestAsync(request, transport, cancellationToken);
                    }
                    else
                    {
                        var response = await _mcpServer.HandleRequestAsync(request);
                        await transport.SendResponseAsync(response, cancellationToken);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogInformation(ex, "WebSocket connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection");
            }
            finally
            {
                await transport.CloseAsync();
                _logger.LogInformation("WebSocket connection closed");
            }
        }

        private async Task HandleStreamingRequestAsync(
            JsonRpcRequest request,
            ITransport transport,
            CancellationToken cancellationToken)
        {
            try
            {
                // For stream.cancel requests
                if (request.Method == "stream.cancel")
                {
                    var streamId = request.Params.GetProperty("streamId").GetString();
                    await _streamingManager.CancelStreamAsync(streamId);

                    await transport.SendResponseAsync(new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = new { success = true, message = "Stream cancelled" }
                    }, cancellationToken);

                    return;
                }

                // For regular method requests that need streaming responses
                var response = await _mcpServer.HandleRequestAsync(request);
                await transport.SendResponseAsync(response, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling streaming request");

                await transport.SendResponseAsync(new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Internal error: " + ex.Message
                    }
                }, cancellationToken);
            }
        }
    }
}
