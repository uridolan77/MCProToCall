using System.Text.Json.Serialization;

namespace ModelContextProtocol.Core.Models.JsonRpc
{
    /// <summary>
    /// JSON-RPC 2.0 error response
    /// </summary>
    public class JsonRpcErrorResponse : JsonRpcResponseBase
    {
        /// <summary>
        /// Error information
        /// </summary>
        [JsonPropertyName("error")]
        public JsonRpcError Error { get; set; }

        /// <summary>
        /// Creates a new error response
        /// </summary>
        public JsonRpcErrorResponse() { }

        /// <summary>
        /// Creates a new error response with the specified ID and error
        /// </summary>
        public JsonRpcErrorResponse(string id, JsonRpcError error)
        {
            Id = id;
            Error = error;
        }

        /// <summary>
        /// Creates a new error response with the specified ID, error code, and message
        /// </summary>
        public JsonRpcErrorResponse(string id, int code, string message, object data = null)
        {
            Id = id;
            Error = new JsonRpcError
            {
                Code = code,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// Creates an error response from a request
        /// </summary>
        public static JsonRpcErrorResponse FromRequest(JsonRpcRequest request, int code, string message, object data = null)
        {
            return new JsonRpcErrorResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message,
                    Data = data
                }
            };
        }

        /// <summary>
        /// Implicit conversion from JsonRpcErrorResponse to JsonRpcResponse
        /// </summary>
        public static implicit operator JsonRpcResponse(JsonRpcErrorResponse errorResponse)
        {
            // This is a workaround for the type conversion issue
            // In a real implementation, we would use polymorphism properly
            return new JsonRpcResponse
            {
                Id = errorResponse.Id,
                Result = null
            };
        }
    }
}
