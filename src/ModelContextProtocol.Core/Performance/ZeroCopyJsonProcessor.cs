using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Zero-copy JSON processor for high-performance message parsing
    /// </summary>
    public class ZeroCopyJsonProcessor : IDisposable
    {
        private readonly ILogger<ZeroCopyJsonProcessor> _logger;
        private readonly ArrayPool<byte> _arrayPool;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        public ZeroCopyJsonProcessor(ILogger<ZeroCopyJsonProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _arrayPool = ArrayPool<byte>.Shared;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultBufferSize = 16 * 1024, // 16KB buffer
                AllowTrailingCommas = false,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        /// <summary>
        /// Reads a JSON-RPC request from a stream without string allocations
        /// </summary>
        public async Task<JsonRpcRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] buffer = null;
            try
            {
                // Read the message length first (assuming length-prefixed format)
                var lengthBuffer = _arrayPool.Rent(4);
                try
                {
                    await stream.ReadExactlyAsync(lengthBuffer.AsMemory(0, 4), cancellationToken);
                    var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10MB limit
                    {
                        throw new InvalidOperationException($"Invalid message length: {messageLength}");
                    }

                    // Rent buffer for the actual message
                    buffer = _arrayPool.Rent(messageLength);
                    await stream.ReadExactlyAsync(buffer.AsMemory(0, messageLength), cancellationToken);

                    // Parse directly from UTF-8 bytes
                    return ParseRequest(buffer.AsSpan(0, messageLength));
                }
                finally
                {
                    _arrayPool.Return(lengthBuffer);
                }
            }
            finally
            {
                if (buffer != null)
                {
                    _arrayPool.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Parses a JSON-RPC request from UTF-8 bytes without string conversions
        /// </summary>
        public JsonRpcRequest ParseRequest(ReadOnlySpan<byte> utf8Json)
        {
            var reader = new Utf8JsonReader(utf8Json);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected JSON object");
            }

            string jsonrpc = null;
            string method = null;
            JsonElement? paramsElement = null;
            JsonElement? id = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToLowerInvariant())
                {
                    case "jsonrpc":
                        jsonrpc = reader.GetString();
                        break;
                    case "method":
                        method = reader.GetString();
                        break;
                    case "params":
                        paramsElement = JsonElement.ParseValue(ref reader);
                        break;
                    case "id":
                        id = JsonElement.ParseValue(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (string.IsNullOrEmpty(method))
            {
                throw new JsonException("Missing required 'method' property");
            }

            return new JsonRpcRequest
            {
                JsonRpc = jsonrpc ?? "2.0",
                Method = method,
                Params = paramsElement ?? default(JsonElement),
                Id = id?.GetRawText()
            };
        }

        /// <summary>
        /// Writes a JSON-RPC response to a stream with minimal allocations
        /// </summary>
        public async Task WriteResponseAsync(Stream stream, JsonRpcResponse response, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (response == null) throw new ArgumentNullException(nameof(response));

            byte[] buffer = null;
            try
            {
                // Estimate buffer size (can be adjusted based on typical response sizes)
                var estimatedSize = 1024;
                buffer = _arrayPool.Rent(estimatedSize);

                var bufferWriter = new ArrayBufferWriter<byte>(buffer);
                var writer = new Utf8JsonWriter(bufferWriter);

                writer.WriteStartObject();
                writer.WriteString("jsonrpc", response.JsonRpc ?? "2.0");

                if (response.Result != null)
                {
                    writer.WritePropertyName("result");
                    JsonSerializer.Serialize(writer, response.Result, _jsonOptions);
                }

                if (!string.IsNullOrEmpty(response.Id))
                {
                    writer.WritePropertyName("id");
                    writer.WriteStringValue(response.Id);
                }

                writer.WriteEndObject();
                await writer.FlushAsync(cancellationToken);

                var jsonBytes = bufferWriter.WrittenMemory;

                // Write length prefix
                var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
                await stream.WriteAsync(lengthBytes, cancellationToken);

                // Write JSON content
                await stream.WriteAsync(jsonBytes, cancellationToken);
            }
            finally
            {
                if (buffer != null)
                {
                    _arrayPool.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Validates JSON structure without full deserialization
        /// </summary>
        public bool IsValidJson(ReadOnlySpan<byte> utf8Json)
        {
            try
            {
                var reader = new Utf8JsonReader(utf8Json);
                while (reader.Read()) { } // Just validate structure
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts the method name from JSON without full parsing
        /// </summary>
        public string ExtractMethod(ReadOnlySpan<byte> utf8Json)
        {
            var reader = new Utf8JsonReader(utf8Json);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    if (string.Equals(propertyName, "method", StringComparison.OrdinalIgnoreCase))
                    {
                        return reader.GetString();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Array buffer writer for efficient JSON writing
    /// </summary>
    internal class ArrayBufferWriter<T> : IBufferWriter<T>
    {
        private T[] _buffer;
        private int _index;

        public ArrayBufferWriter(T[] initialBuffer)
        {
            _buffer = initialBuffer ?? throw new ArgumentNullException(nameof(initialBuffer));
            _index = 0;
        }

        public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);
        public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);
        public int WrittenCount => _index;

        public void Advance(int count)
        {
            if (count < 0 || _index + count > _buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            _index += count;
        }

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsMemory(_index);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsSpan(_index);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                sizeHint = 1;

            if (_index + sizeHint > _buffer.Length)
            {
                var newSize = Math.Max(_buffer.Length * 2, _index + sizeHint);
                var newBuffer = new T[newSize];
                Array.Copy(_buffer, newBuffer, _index);
                _buffer = newBuffer;
            }
        }
    }


}
