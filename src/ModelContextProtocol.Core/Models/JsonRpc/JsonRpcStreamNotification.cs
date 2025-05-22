using System.Text.Json.Serialization;

namespace ModelContextProtocol.Core.Models.JsonRpc
{
    /// <summary>
    /// Represents a streaming JSON-RPC notification for partial results
    /// </summary>
    public class JsonRpcStreamNotification
    {
        /// <summary>
        /// Gets or sets the JSON-RPC version
        /// </summary>
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the method name
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = "stream.data";

        /// <summary>
        /// Gets or sets the parameters
        /// </summary>
        [JsonPropertyName("params")]
        public StreamParams Params { get; set; }
    }

    /// <summary>
    /// Parameters for a streaming notification
    /// </summary>
    public class StreamParams
    {
        /// <summary>
        /// Gets or sets the stream ID
        /// </summary>
        [JsonPropertyName("streamId")]
        public string StreamId { get; set; }

        /// <summary>
        /// Gets or sets the sequence number
        /// </summary>
        [JsonPropertyName("sequenceNumber")]
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the data
        /// </summary>
        [JsonPropertyName("data")]
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the last chunk
        /// </summary>
        [JsonPropertyName("isComplete")]
        public bool IsComplete { get; set; }
    }
}
