using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MessagePack;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// High-performance JSON processor using Span-based operations
    /// </summary>
    public class OptimizedJsonProcessor
    {
        private readonly ILogger<OptimizedJsonProcessor> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public OptimizedJsonProcessor(ILogger<OptimizedJsonProcessor> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Deserializes JSON from UTF-8 bytes using Span operations
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="utf8Json">UTF-8 JSON bytes</param>
        /// <returns>Deserialized object</returns>
        public T DeserializeFromUtf8<T>(ReadOnlySpan<byte> utf8Json)
        {
            try
            {
                var reader = new Utf8JsonReader(utf8Json);
                return JsonSerializer.Deserialize<T>(ref reader, _jsonOptions)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON to {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Serializes an object to UTF-8 JSON using a buffer writer
        /// </summary>
        /// <typeparam name="T">Type to serialize</typeparam>
        /// <param name="value">Value to serialize</param>
        /// <param name="writer">Buffer writer to write to</param>
        public void SerializeToUtf8<T>(T value, IBufferWriter<byte> writer)
        {
            try
            {
                using var jsonWriter = new Utf8JsonWriter(writer);
                JsonSerializer.Serialize(jsonWriter, value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize {Type} to JSON", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Serializes an object to UTF-8 JSON and returns the bytes
        /// </summary>
        /// <typeparam name="T">Type to serialize</typeparam>
        /// <param name="value">Value to serialize</param>
        /// <returns>UTF-8 JSON bytes</returns>
        public byte[] SerializeToUtf8Bytes<T>(T value)
        {
            try
            {
                return JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize {Type} to UTF-8 bytes", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Deserializes JSON from a stream asynchronously
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="stream">Stream containing JSON</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized object</returns>
        public async ValueTask<T> DeserializeFromStreamAsync<T>(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Deserialization returned null");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON stream to {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Serializes an object to a stream asynchronously
        /// </summary>
        /// <typeparam name="T">Type to serialize</typeparam>
        /// <param name="stream">Stream to write to</param>
        /// <param name="value">Value to serialize</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async ValueTask SerializeToStreamAsync<T>(
            Stream stream,
            T value,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize {Type} to stream", typeof(T).Name);
                throw;
            }
        }
    }

    /// <summary>
    /// MessagePack processor for binary serialization
    /// </summary>
    public class MessagePackProcessor
    {
        private readonly ILogger<MessagePackProcessor> _logger;
        private readonly MessagePackSerializerOptions _options;

        public MessagePackProcessor(ILogger<MessagePackProcessor> logger)
        {
            _logger = logger;
            _options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray);
        }

        /// <summary>
        /// Serializes an object to MessagePack format
        /// </summary>
        /// <typeparam name="T">Type to serialize</typeparam>
        /// <param name="value">Value to serialize</param>
        /// <returns>MessagePack bytes</returns>
        public byte[] Serialize<T>(T value)
        {
            try
            {
                return MessagePackSerializer.Serialize(value, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize {Type} to MessagePack", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Deserializes MessagePack data to an object
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="data">MessagePack data</param>
        /// <returns>Deserialized object</returns>
        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            try
            {
                var sequence = new ReadOnlySequence<byte>(data.ToArray());
                return MessagePackSerializer.Deserialize<T>(sequence, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize MessagePack to {Type}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Serializes an object to MessagePack format using a buffer writer
        /// </summary>
        /// <typeparam name="T">Type to serialize</typeparam>
        /// <param name="writer">Buffer writer to write to</param>
        /// <param name="value">Value to serialize</param>
        public void Serialize<T>(IBufferWriter<byte> writer, T value)
        {
            try
            {
                MessagePackSerializer.Serialize(writer, value, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize {Type} to MessagePack buffer", typeof(T).Name);
                throw;
            }
        }
    }
}
