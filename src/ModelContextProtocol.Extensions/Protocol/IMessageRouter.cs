using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Protocol
{
    /// <summary>
    /// Routes messages based on configurable rules and patterns
    /// </summary>
    public interface IMessageRouter
    {
        /// <summary>
        /// Routes a message to appropriate handlers
        /// </summary>
        /// <param name="message">Message to route</param>
        /// <param name="context">Routing context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Routing result</returns>
        Task<MessageRoutingResult> RouteMessageAsync(McpMessage message, RoutingContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a message route
        /// </summary>
        /// <param name="route">Route configuration</param>
        void RegisterRoute(MessageRoute route);

        /// <summary>
        /// Unregisters a message route
        /// </summary>
        /// <param name="routeId">Route ID to remove</param>
        /// <returns>True if route was removed</returns>
        bool UnregisterRoute(string routeId);

        /// <summary>
        /// Gets routing statistics
        /// </summary>
        /// <returns>Routing statistics</returns>
        Task<RoutingStatistics> GetRoutingStatsAsync();

        /// <summary>
        /// Gets all registered routes
        /// </summary>
        /// <returns>Registered routes</returns>
        Task<MessageRoute[]> GetRoutesAsync();

        /// <summary>
        /// Tests a route against a message without executing it
        /// </summary>
        /// <param name="message">Message to test</param>
        /// <param name="routeId">Route ID to test</param>
        /// <returns>Test result</returns>
        Task<RouteTestResult> TestRouteAsync(McpMessage message, string routeId);
    }

    /// <summary>
    /// Transforms messages between different formats and versions
    /// </summary>
    public interface IMessageTransformationPipeline
    {
        /// <summary>
        /// Transforms input through a chain of transformers
        /// </summary>
        /// <typeparam name="T">Output type</typeparam>
        /// <param name="input">Input data</param>
        /// <param name="transformationChain">Chain of transformation steps</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transformed output</returns>
        Task<T> TransformAsync<T>(object input, string transformationChain, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a message transformer
        /// </summary>
        /// <param name="name">Transformer name</param>
        /// <param name="transformer">Transformer implementation</param>
        void RegisterTransformer(string name, IMessageTransformer transformer);

        /// <summary>
        /// Gets available transformers
        /// </summary>
        /// <returns>Available transformer names</returns>
        Task<string[]> GetAvailableTransformersAsync();

        /// <summary>
        /// Validates a transformation chain
        /// </summary>
        /// <param name="transformationChain">Chain to validate</param>
        /// <returns>Validation result</returns>
        Task<TransformationValidationResult> ValidateChainAsync(string transformationChain);
    }

    /// <summary>
    /// Transforms messages between formats
    /// </summary>
    public interface IMessageTransformer
    {
        /// <summary>
        /// Gets the transformer name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets supported input types
        /// </summary>
        Type[] SupportedInputTypes { get; }

        /// <summary>
        /// Gets supported output types
        /// </summary>
        Type[] SupportedOutputTypes { get; }

        /// <summary>
        /// Transforms input to output
        /// </summary>
        /// <param name="input">Input data</param>
        /// <param name="outputType">Desired output type</param>
        /// <param name="options">Transformation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transformed output</returns>
        Task<object> TransformAsync(object input, Type outputType, TransformationOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if transformation is supported
        /// </summary>
        /// <param name="inputType">Input type</param>
        /// <param name="outputType">Output type</param>
        /// <returns>True if supported</returns>
        bool CanTransform(Type inputType, Type outputType);
    }

    /// <summary>
    /// Represents a message in the MCP system
    /// </summary>
    public class McpMessage
    {
        /// <summary>
        /// Gets or sets the message ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the message type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the message method/operation
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the message payload
        /// </summary>
        public object Payload { get; set; }

        /// <summary>
        /// Gets or sets message headers
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Gets or sets message metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when the message was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the message priority
        /// </summary>
        public MessagePriority Priority { get; set; } = MessagePriority.Normal;

        /// <summary>
        /// Gets or sets the message source
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the message destination
        /// </summary>
        public string Destination { get; set; }
    }

    /// <summary>
    /// Message priority levels
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Context for message routing
    /// </summary>
    public class RoutingContext
    {
        /// <summary>
        /// Gets or sets the client/session ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the user ID
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets routing tags
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets routing properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// Gets or sets the routing timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the trace ID for distributed tracing
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Gets or sets the span ID for distributed tracing
        /// </summary>
        public string SpanId { get; set; }
    }

    /// <summary>
    /// Configuration for a message route
    /// </summary>
    public class MessageRoute
    {
        /// <summary>
        /// Gets or sets the route ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the route name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the route description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the route pattern (method pattern, type pattern, etc.)
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Gets or sets the route condition
        /// </summary>
        public Func<McpMessage, RoutingContext, bool> Condition { get; set; }

        /// <summary>
        /// Gets or sets the route handler
        /// </summary>
        public Func<McpMessage, RoutingContext, CancellationToken, Task<object>> Handler { get; set; }

        /// <summary>
        /// Gets or sets the route priority (higher numbers = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether this route is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets route tags for categorization
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets route metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when this route was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when this route was last modified
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// Gets or sets the transformation chain to apply before routing
        /// </summary>
        public string TransformationChain { get; set; }

        /// <summary>
        /// Gets or sets the timeout for this route
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Result of message routing
    /// </summary>
    public class MessageRoutingResult
    {
        /// <summary>
        /// Gets or sets whether routing was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the matched route
        /// </summary>
        public MessageRoute MatchedRoute { get; set; }

        /// <summary>
        /// Gets or sets the routing result
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// Gets or sets routing errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets routing warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets routing execution time
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets routing metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Routing statistics
    /// </summary>
    public class RoutingStatistics
    {
        /// <summary>
        /// Gets or sets total messages routed
        /// </summary>
        public long TotalMessagesRouted { get; set; }

        /// <summary>
        /// Gets or sets successful routings
        /// </summary>
        public long SuccessfulRoutings { get; set; }

        /// <summary>
        /// Gets or sets failed routings
        /// </summary>
        public long FailedRoutings { get; set; }

        /// <summary>
        /// Gets or sets average routing time
        /// </summary>
        public TimeSpan AverageRoutingTime { get; set; }

        /// <summary>
        /// Gets or sets route-specific statistics
        /// </summary>
        public Dictionary<string, RouteStatistics> RouteStats { get; set; } = new();

        /// <summary>
        /// Gets or sets when statistics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics for a specific route
    /// </summary>
    public class RouteStatistics
    {
        /// <summary>
        /// Gets or sets the route ID
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Gets or sets messages handled by this route
        /// </summary>
        public long MessagesHandled { get; set; }

        /// <summary>
        /// Gets or sets successful executions
        /// </summary>
        public long SuccessfulExecutions { get; set; }

        /// <summary>
        /// Gets or sets failed executions
        /// </summary>
        public long FailedExecutions { get; set; }

        /// <summary>
        /// Gets or sets average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets last execution time
        /// </summary>
        public DateTime? LastExecutedAt { get; set; }
    }

    /// <summary>
    /// Result of testing a route
    /// </summary>
    public class RouteTestResult
    {
        /// <summary>
        /// Gets or sets whether the route matches
        /// </summary>
        public bool Matches { get; set; }

        /// <summary>
        /// Gets or sets the match score (0-1)
        /// </summary>
        public double MatchScore { get; set; }

        /// <summary>
        /// Gets or sets test details
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Gets or sets test metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Options for message transformation
    /// </summary>
    public class TransformationOptions
    {
        /// <summary>
        /// Gets or sets transformation parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to validate input
        /// </summary>
        public bool ValidateInput { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate output
        /// </summary>
        public bool ValidateOutput { get; set; } = true;

        /// <summary>
        /// Gets or sets transformation timeout
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Result of transformation chain validation
    /// </summary>
    public class TransformationValidationResult
    {
        /// <summary>
        /// Gets or sets whether the chain is valid
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
        /// Gets or sets the parsed transformation steps
        /// </summary>
        public string[] TransformationSteps { get; set; } = Array.Empty<string>();
    }
}
