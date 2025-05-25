using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Protocol
{
    /// <summary>
    /// Manages protocol version negotiation and compatibility
    /// </summary>
    public interface IProtocolVersionManager
    {
        /// <summary>
        /// Negotiates the best protocol version between client and server
        /// </summary>
        /// <param name="clientVersions">Versions supported by the client</param>
        /// <returns>Negotiated protocol version</returns>
        Task<ProtocolVersion> NegotiateVersionAsync(string[] clientVersions);

        /// <summary>
        /// Registers a protocol handler for a specific version
        /// </summary>
        /// <param name="version">Protocol version</param>
        /// <param name="handler">Protocol handler</param>
        void RegisterVersionHandler(string version, IProtocolHandler handler);

        /// <summary>
        /// Checks if a version is compatible with the current server
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>True if compatible</returns>
        Task<bool> IsVersionCompatibleAsync(string version);

        /// <summary>
        /// Gets all supported protocol versions
        /// </summary>
        /// <returns>Supported versions</returns>
        Task<ProtocolVersion[]> GetSupportedVersionsAsync();

        /// <summary>
        /// Gets the default protocol version
        /// </summary>
        /// <returns>Default version</returns>
        Task<ProtocolVersion> GetDefaultVersionAsync();

        /// <summary>
        /// Migrates data between protocol versions
        /// </summary>
        /// <param name="data">Data to migrate</param>
        /// <param name="fromVersion">Source version</param>
        /// <param name="toVersion">Target version</param>
        /// <returns>Migrated data</returns>
        Task<object> MigrateDataAsync(object data, string fromVersion, string toVersion);
    }

    /// <summary>
    /// Handles protocol-specific operations
    /// </summary>
    public interface IProtocolHandler
    {
        /// <summary>
        /// Gets the protocol version this handler supports
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the protocol capabilities
        /// </summary>
        ProtocolCapabilities Capabilities { get; }

        /// <summary>
        /// Serializes a message for this protocol version
        /// </summary>
        /// <param name="message">Message to serialize</param>
        /// <returns>Serialized message</returns>
        Task<byte[]> SerializeMessageAsync(object message);

        /// <summary>
        /// Deserializes a message from this protocol version
        /// </summary>
        /// <param name="data">Serialized data</param>
        /// <param name="messageType">Expected message type</param>
        /// <returns>Deserialized message</returns>
        Task<T> DeserializeMessageAsync<T>(byte[] data, Type messageType = null);

        /// <summary>
        /// Validates a message for this protocol version
        /// </summary>
        /// <param name="message">Message to validate</param>
        /// <returns>Validation result</returns>
        Task<ProtocolValidationResult> ValidateMessageAsync(object message);

        /// <summary>
        /// Transforms a message from another protocol version
        /// </summary>
        /// <param name="message">Message to transform</param>
        /// <param name="sourceVersion">Source protocol version</param>
        /// <returns>Transformed message</returns>
        Task<object> TransformMessageAsync(object message, string sourceVersion);
    }

    /// <summary>
    /// Adaptive protocol handler that selects optimal protocol based on conditions
    /// </summary>
    public interface IAdaptiveProtocolHandler
    {
        /// <summary>
        /// Selects the optimal protocol handler based on network conditions
        /// </summary>
        /// <param name="conditions">Current network conditions</param>
        /// <returns>Optimal protocol handler</returns>
        Task<IProtocolHandler> SelectOptimalProtocolAsync(NetworkConditions conditions);

        /// <summary>
        /// Monitors network conditions and adapts protocol accordingly
        /// </summary>
        /// <param name="onProtocolChanged">Callback when protocol changes</param>
        Task StartAdaptiveMonitoringAsync(Func<IProtocolHandler, Task> onProtocolChanged);

        /// <summary>
        /// Stops adaptive monitoring
        /// </summary>
        Task StopAdaptiveMonitoringAsync();
    }

    /// <summary>
    /// Represents a protocol version with semantic versioning
    /// </summary>
    public class ProtocolVersion : IComparable<ProtocolVersion>
    {
        /// <summary>
        /// Gets or sets the version string (e.g., "1.2.3")
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the major version number
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Gets or sets the minor version number
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Gets or sets the patch version number
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the pre-release identifier
        /// </summary>
        public string PreRelease { get; set; }

        /// <summary>
        /// Gets or sets the build metadata
        /// </summary>
        public string BuildMetadata { get; set; }

        /// <summary>
        /// Gets or sets the protocol capabilities
        /// </summary>
        public ProtocolCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Gets or sets when this version was released
        /// </summary>
        public DateTime? ReleasedAt { get; set; }

        /// <summary>
        /// Gets or sets whether this version is deprecated
        /// </summary>
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// Gets or sets the deprecation message
        /// </summary>
        public string DeprecationMessage { get; set; }

        /// <summary>
        /// Parses a version string into a ProtocolVersion
        /// </summary>
        /// <param name="versionString">Version string to parse</param>
        /// <returns>Parsed protocol version</returns>
        public static ProtocolVersion Parse(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                throw new ArgumentException("Version string cannot be null or empty", nameof(versionString));

            var parts = versionString.Split('.');
            if (parts.Length < 3)
                throw new ArgumentException("Version string must have at least major.minor.patch format", nameof(versionString));

            return new ProtocolVersion
            {
                Version = versionString,
                Major = int.Parse(parts[0]),
                Minor = int.Parse(parts[1]),
                Patch = int.Parse(parts[2])
            };
        }

        /// <summary>
        /// Compares this version with another version
        /// </summary>
        public int CompareTo(ProtocolVersion other)
        {
            if (other == null) return 1;

            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;

            return Patch.CompareTo(other.Patch);
        }

        /// <summary>
        /// Checks if this version is compatible with another version
        /// </summary>
        /// <param name="other">Version to check compatibility with</param>
        /// <returns>True if compatible</returns>
        public bool IsCompatibleWith(ProtocolVersion other)
        {
            if (other == null) return false;

            // Same major version is generally compatible
            if (Major == other.Major)
            {
                // Newer minor versions are backward compatible
                return Minor >= other.Minor;
            }

            return false;
        }

        public override string ToString() => Version;
    }

    /// <summary>
    /// Protocol capabilities and features
    /// </summary>
    public class ProtocolCapabilities
    {
        /// <summary>
        /// Gets or sets whether streaming is supported
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets whether compression is supported
        /// </summary>
        public bool SupportsCompression { get; set; }

        /// <summary>
        /// Gets or sets whether encryption is supported
        /// </summary>
        public bool SupportsEncryption { get; set; }

        /// <summary>
        /// Gets or sets whether binary data is supported
        /// </summary>
        public bool SupportsBinaryData { get; set; }

        /// <summary>
        /// Gets or sets whether batch operations are supported
        /// </summary>
        public bool SupportsBatchOperations { get; set; }

        /// <summary>
        /// Gets or sets the maximum message size
        /// </summary>
        public long MaxMessageSize { get; set; } = 1024 * 1024; // 1MB default

        /// <summary>
        /// Gets or sets supported content types
        /// </summary>
        public string[] SupportedContentTypes { get; set; } = { "application/json" };

        /// <summary>
        /// Gets or sets supported compression algorithms
        /// </summary>
        public string[] SupportedCompressionAlgorithms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets supported encryption algorithms
        /// </summary>
        public string[] SupportedEncryptionAlgorithms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets custom capabilities
        /// </summary>
        public Dictionary<string, object> CustomCapabilities { get; set; } = new();
    }

    /// <summary>
    /// Network conditions for adaptive protocol selection
    /// </summary>
    public class NetworkConditions
    {
        /// <summary>
        /// Gets or sets the network latency in milliseconds
        /// </summary>
        public double LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the available bandwidth in bytes per second
        /// </summary>
        public long BandwidthBytesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the packet loss rate (0-1)
        /// </summary>
        public double PacketLossRate { get; set; }

        /// <summary>
        /// Gets or sets the connection stability score (0-1)
        /// </summary>
        public double StabilityScore { get; set; }

        /// <summary>
        /// Gets or sets whether the connection is metered
        /// </summary>
        public bool IsMeteredConnection { get; set; }

        /// <summary>
        /// Gets or sets the connection type
        /// </summary>
        public ConnectionType ConnectionType { get; set; }

        /// <summary>
        /// Gets or sets when these conditions were measured
        /// </summary>
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of network connections
    /// </summary>
    public enum ConnectionType
    {
        Unknown,
        Ethernet,
        WiFi,
        Cellular,
        Satellite,
        VPN
    }

    /// <summary>
    /// Result of protocol message validation
    /// </summary>
    public class ProtocolValidationResult
    {
        /// <summary>
        /// Gets or sets whether the message is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets the validation score (0-1)
        /// </summary>
        public double ValidationScore { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets additional validation metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
