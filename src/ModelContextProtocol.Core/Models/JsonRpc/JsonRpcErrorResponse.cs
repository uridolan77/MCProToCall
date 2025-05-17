using System.Text.Json.Serialization;

namespace ModelContextProtocol.Core.Models.JsonRpc
{
    /// <summary>
    /// JSON-RPC 2.0 error response
    /// </summary>
    public class JsonRpcErrorResponse
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
        /// Error information
        /// </summary>
        [JsonPropertyName("error")]
        public JsonRpcError Error { get; set; }
    }
}
