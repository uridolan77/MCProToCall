using System.Diagnostics;

namespace ModelContextProtocol.Server.Observability
{
    /// <summary>
    /// Interface for MCP telemetry operations
    /// </summary>
    public interface IMcpTelemetry
    {
        /// <summary>
        /// Starts a new activity for tracing
        /// </summary>
        /// <param name="name">Activity name</param>
        /// <param name="kind">Activity kind</param>
        /// <returns>The created activity</returns>
        Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal);

        /// <summary>
        /// Records a request received event
        /// </summary>
        /// <param name="method">Method name</param>
        void RecordRequestReceived(string method);

        /// <summary>
        /// Records a request completed event
        /// </summary>
        /// <param name="method">Method name</param>
        /// <param name="success">Whether the request was successful</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        void RecordRequestCompleted(string method, bool success, double durationMs);

        /// <summary>
        /// Records an error event
        /// </summary>
        /// <param name="method">Method name</param>
        /// <param name="errorType">Error type</param>
        void RecordError(string method, string errorType);

        /// <summary>
        /// Records a connection event
        /// </summary>
        /// <param name="eventType">Event type (connected, disconnected)</param>
        /// <param name="clientId">Client ID</param>
        void RecordConnectionEvent(string eventType, string clientId);

        /// <summary>
        /// Records a security event
        /// </summary>
        /// <param name="eventType">Event type</param>
        /// <param name="details">Event details</param>
        void RecordSecurityEvent(string eventType, string details);
    }
}
