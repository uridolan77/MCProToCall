using System.Text.Json.Serialization;

namespace ModelContextProtocol.Core.Models.JsonRpc
{
    /// <summary>
    /// Base class for JSON-RPC 2.0 responses
    /// </summary>
    public abstract class JsonRpcResponseBase
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
    }

    /// <summary>
    /// JSON-RPC 2.0 success response
    /// </summary>
    public class JsonRpcResponse : JsonRpcResponseBase
    {
        /// <summary>
        /// Result of the method invocation
        /// </summary>
        [JsonPropertyName("result")]
        public object Result { get; set; }

        /// <summary>
        /// Creates a new success response
        /// </summary>
        public JsonRpcResponse() { }

        /// <summary>
        /// Creates a new success response with the specified ID and result
        /// </summary>
        public JsonRpcResponse(string id, object result)
        {
            Id = id;
            Result = result;
        }

        /// <summary>
        /// Creates a success response from a request
        /// </summary>
        public static JsonRpcResponse FromRequest(JsonRpcRequest request, object result)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
    }
}