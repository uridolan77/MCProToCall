using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// High-performance JSON serializer using source generators
    /// </summary>
    public static class HighPerformanceJsonSerializer
    {
        private static readonly JsonSerializerOptions _options = McpJsonContext.DefaultOptions;

        /// <summary>
        /// Serializes an object to JSON
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="value">Object to serialize</param>
        /// <returns>JSON string</returns>
        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, typeof(T), _options);
        }

        /// <summary>
        /// Deserializes JSON to an object
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="json">JSON string</param>
        /// <returns>Deserialized object</returns>
        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        /// <summary>
        /// Deserializes UTF-8 encoded JSON to an object
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="utf8Json">UTF-8 encoded JSON</param>
        /// <returns>Deserialized object</returns>
        public static T Deserialize<T>(ReadOnlySpan<byte> utf8Json)
        {
            return JsonSerializer.Deserialize<T>(utf8Json, _options);
        }

        /// <summary>
        /// Asynchronously serializes an object to JSON and writes it to a stream
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="stream">Target stream</param>
        /// <param name="value">Object to serialize</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task SerializeAsync<T>(Stream stream, T value)
        {
            await JsonSerializer.SerializeAsync(stream, value, typeof(T), _options);
        }

        /// <summary>
        /// Asynchronously deserializes JSON from a stream
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="stream">Source stream</param>
        /// <returns>Deserialized object</returns>
        public static async ValueTask<T> DeserializeAsync<T>(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, _options);
        }
    }
}
