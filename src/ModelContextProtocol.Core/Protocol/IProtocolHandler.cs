using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Core.Protocol
{
    /// <summary>
    /// Interface for protocol handlers that can process different message formats
    /// </summary>
    public interface IProtocolHandler
    {
        /// <summary>
        /// The name of this protocol
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// The version of this protocol
        /// </summary>
        string ProtocolVersion { get; }

        /// <summary>
        /// Content type for this protocol
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Whether this protocol supports streaming
        /// </summary>
        bool SupportsStreaming { get; }

        /// <summary>
        /// Whether this protocol is binary
        /// </summary>
        bool IsBinary { get; }

        /// <summary>
        /// Reads a message from the stream
        /// </summary>
        Task<McpMessage> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a message to the stream
        /// </summary>
        Task WriteMessageAsync(Stream stream, McpMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if the stream contains a message in this protocol format
        /// </summary>
        Task<bool> CanHandleAsync(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the estimated message size for buffer allocation
        /// </summary>
        int GetEstimatedMessageSize(McpMessage message);
    }

    /// <summary>
    /// Base class for MCP messages
    /// </summary>
    public abstract class McpMessage
    {
        /// <summary>
        /// Message type
        /// </summary>
        public abstract string MessageType { get; }

        /// <summary>
        /// Message ID for correlation
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// JSON-RPC request message
    /// </summary>
    public class JsonRpcRequest : McpMessage
    {
        public override string MessageType => "request";
        
        public string JsonRpc { get; set; } = "2.0";
        public string Method { get; set; }
        public object Params { get; set; }
        public object Id { get; set; }
    }

    /// <summary>
    /// JSON-RPC response message
    /// </summary>
    public class JsonRpcResponse : McpMessage
    {
        public override string MessageType => "response";
        
        public string JsonRpc { get; set; } = "2.0";
        public object Result { get; set; }
        public JsonRpcError Error { get; set; }
        public object Id { get; set; }
    }

    /// <summary>
    /// JSON-RPC notification message
    /// </summary>
    public class JsonRpcNotification : McpMessage
    {
        public override string MessageType => "notification";
        
        public string JsonRpc { get; set; } = "2.0";
        public string Method { get; set; }
        public object Params { get; set; }
    }

    /// <summary>
    /// JSON-RPC error object
    /// </summary>
    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    /// <summary>
    /// Binary message for high-performance scenarios
    /// </summary>
    public class BinaryMessage : McpMessage
    {
        public override string MessageType => "binary";
        
        public byte[] Data { get; set; }
        public string Encoding { get; set; } = "msgpack";
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Streaming message for large data transfers
    /// </summary>
    public class StreamingMessage : McpMessage
    {
        public override string MessageType => "streaming";
        
        public string StreamId { get; set; }
        public long TotalSize { get; set; }
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public byte[] ChunkData { get; set; }
        public bool IsLastChunk { get; set; }
        public string ChecksumAlgorithm { get; set; } = "SHA256";
        public string ChunkChecksum { get; set; }
        public string TotalChecksum { get; set; }
    }

    /// <summary>
    /// Protocol negotiation message
    /// </summary>
    public class ProtocolNegotiationMessage : McpMessage
    {
        public override string MessageType => "negotiation";
        
        public string[] SupportedProtocols { get; set; }
        public string[] SupportedVersions { get; set; }
        public string PreferredProtocol { get; set; }
        public string PreferredVersion { get; set; }
        public Dictionary<string, object> Capabilities { get; set; } = new Dictionary<string, object>();
        public bool IsResponse { get; set; }
        public string SelectedProtocol { get; set; }
        public string SelectedVersion { get; set; }
    }
}
