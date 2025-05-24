using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Protocol.Handlers
{
    /// <summary>
    /// Protocol handler for JSON-RPC messages
    /// </summary>
    public class JsonRpcProtocolHandler : IProtocolHandler
    {
        private readonly ILogger<JsonRpcProtocolHandler> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public string ProtocolName => "json-rpc";
        public string ProtocolVersion => "2.0";
        public string ContentType => "application/json";
        public bool SupportsStreaming => false;
        public bool IsBinary => false;

        public JsonRpcProtocolHandler(ILogger<JsonRpcProtocolHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<McpMessage> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            try
            {
                _logger.LogTrace("Reading JSON-RPC message from stream");

                // Read length prefix
                var lengthBuffer = new byte[4];
                await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
                var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Read JSON content
                var jsonBuffer = new byte[messageLength];
                await stream.ReadExactlyAsync(jsonBuffer, cancellationToken);

                // Parse JSON
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(jsonBuffer, _jsonOptions);

                _logger.LogTrace("Successfully read JSON-RPC message: {Method}", request.Method);
                return new JsonRpcRequestMessage { Request = request };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON-RPC message");
                throw new InvalidOperationException("Invalid JSON-RPC message format", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading JSON-RPC message");
                throw;
            }
        }

        public async Task WriteMessageAsync(Stream stream, McpMessage message, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (message == null) throw new ArgumentNullException(nameof(message));

            try
            {
                _logger.LogTrace("Writing JSON-RPC message to stream: {MessageType}", message.GetType().Name);

                object jsonObject = null;

                switch (message)
                {
                    case JsonRpcResponseMessage responseMsg:
                        jsonObject = responseMsg.Response;
                        break;
                    case JsonRpcRequestMessage requestMsg:
                        jsonObject = requestMsg.Request;
                        break;
                    default:
                        throw new ArgumentException($"Unsupported message type: {message.GetType().Name}");
                }

                // Serialize to JSON
                var json = JsonSerializer.Serialize(jsonObject, _jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(json);

                // Write length prefix
                var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
                await stream.WriteAsync(lengthBytes, cancellationToken);

                // Write JSON content
                await stream.WriteAsync(jsonBytes, cancellationToken);

                _logger.LogTrace("Successfully wrote JSON-RPC message");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing JSON-RPC message");
                throw;
            }
        }

        public async Task<bool> CanHandleAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                return false;

            try
            {
                var originalPosition = stream.Position;

                // Read a small buffer to check for JSON structure
                var buffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                // Reset stream position
                stream.Position = originalPosition;

                if (bytesRead == 0)
                    return false;

                // Simple check for JSON-RPC structure
                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return text.Contains("\"jsonrpc\"") && (text.Contains("\"method\"") || text.Contains("\"result\"") || text.Contains("\"error\""));
            }
            catch
            {
                return false;
            }
        }

        public int GetEstimatedMessageSize(McpMessage message)
        {
            // Simple estimation - 1KB default
            return 1024;
        }
    }

    /// <summary>
    /// Wrapper for JSON-RPC request messages
    /// </summary>
    public class JsonRpcRequestMessage : McpMessage
    {
        public override string MessageType => "request";
        public JsonRpcRequest Request { get; set; }
    }

    /// <summary>
    /// Wrapper for JSON-RPC response messages
    /// </summary>
    public class JsonRpcResponseMessage : McpMessage
    {
        public override string MessageType => "response";
        public JsonRpcResponse Response { get; set; }
    }
}
