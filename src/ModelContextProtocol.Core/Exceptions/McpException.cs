using System;
using System.Collections.Generic;

namespace ModelContextProtocol.Core.Exceptions
{
    /// <summary>
    /// Exception for MCP-specific errors that will be translated to JSON-RPC error responses
    /// </summary>
    public class McpException : Exception
    {
        /// <summary>
        /// JSON-RPC error code
        /// </summary>
        public int ErrorCode { get; }
        
        /// <summary>
        /// Optional additional error data
        /// </summary>
        public new object Data { get; }
        
        /// <summary>
        /// Creates a new MCP exception
        /// </summary>
        /// <param name="errorCode">JSON-RPC error code</param>
        /// <param name="message">Error message</param>
        /// <param name="data">Optional error data</param>
        public McpException(int errorCode, string message, object data = null) 
            : base(message)
        {
            ErrorCode = errorCode;
            Data = data;
        }
        
        /// <summary>
        /// Creates a new MCP exception with the inner exception
        /// </summary>
        public McpException(int errorCode, string message, Exception innerException, object data = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Data = data;
        }
        
        /// <summary>
        /// Standard JSON-RPC error codes
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>
            /// Invalid JSON was received by the server
            /// </summary>
            public const int ParseError = -32700;
            
            /// <summary>
            /// The JSON sent is not a valid Request object
            /// </summary>
            public const int InvalidRequest = -32600;
            
            /// <summary>
            /// The method does not exist / is not available
            /// </summary>
            public const int MethodNotFound = -32601;
            
            /// <summary>
            /// Invalid method parameter(s)
            /// </summary>
            public const int InvalidParams = -32602;
            
            /// <summary>
            /// Internal JSON-RPC error
            /// </summary>
            public const int InternalError = -32603;
            
            /// <summary>
            /// Server error, -32000 to -32099 are reserved for implementation-defined server-errors
            /// </summary>
            public const int ServerError = -32000;
            
            // MCP-specific error codes (should be outside reserved JSON-RPC range)
            
            /// <summary>
            /// Authentication error
            /// </summary>
            public const int AuthenticationError = -33001;
            
            /// <summary>
            /// Authorization error
            /// </summary>
            public const int AuthorizationError = -33002;
            
            /// <summary>
            /// Rate limit exceeded
            /// </summary>
            public const int RateLimitExceeded = -33003;
            
            /// <summary>
            /// Resource not found
            /// </summary>
            public const int ResourceNotFound = -33004;
            
            /// <summary>
            /// Tool execution error
            /// </summary>
            public const int ToolExecutionError = -33005;
            
            /// <summary>
            /// Prompt rendering error
            /// </summary>
            public const int PromptRenderingError = -33006;
        }
    }
}