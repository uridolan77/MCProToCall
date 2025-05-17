using System.Text.Json.Serialization;

namespace ModelContextProtocol.Core.Models.JsonRpc
{
    /// <summary>
    /// JSON-RPC 2.0 success response
    /// </summary>
    public class JsonRpcResponse
    {
        /// <summary>
        /// Protocol version (must be "2.0")
        /// </summary>
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Response identifier (matching the request)
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Result of the method invocation
        /// </summary>
        [JsonPropertyName("result")]
        public object Result { get; set; }
    }
}