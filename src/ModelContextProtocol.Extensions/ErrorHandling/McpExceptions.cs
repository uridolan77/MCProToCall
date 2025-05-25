using System;
using System.Collections.Generic;
using System.Security;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;

namespace ModelContextProtocol.Extensions.ErrorHandling
{
    /// <summary>
    /// Base exception for MCP-related errors
    /// </summary>
    public class McpException : Exception
    {
        /// <summary>
        /// Gets the error code
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets additional error data
        /// </summary>
        public Dictionary<string, object> ErrorData { get; }

        public McpException(string errorCode, string message, Dictionary<string, object> errorData = null)
            : base(message)
        {
            ErrorCode = errorCode;
            ErrorData = errorData ?? new Dictionary<string, object>();
        }

        public McpException(string errorCode, string message, Exception innerException, Dictionary<string, object> errorData = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            ErrorData = errorData ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Exception thrown when certificate validation fails
    /// </summary>
    public class CertificateValidationException : McpException
    {
        /// <summary>
        /// Gets the validation result
        /// </summary>
        public CertificateValidationResult ValidationResult { get; }

        public CertificateValidationException(string message, CertificateValidationResult result)
            : base("CERT_VALIDATION_FAILED", message)
        {
            ValidationResult = result;
            ErrorData["ValidationResult"] = result;
        }

        public CertificateValidationException(string message, CertificateValidationResult result, Exception innerException)
            : base("CERT_VALIDATION_FAILED", message, innerException)
        {
            ValidationResult = result;
            ErrorData["ValidationResult"] = result;
        }
    }

    /// <summary>
    /// Exception thrown when rate limiting is exceeded
    /// </summary>
    public class RateLimitExceededException : McpException
    {
        /// <summary>
        /// Gets the retry after duration
        /// </summary>
        public TimeSpan RetryAfter { get; }

        /// <summary>
        /// Gets the client identifier
        /// </summary>
        public string ClientId { get; }

        public RateLimitExceededException(string clientId, TimeSpan retryAfter)
            : base("RATE_LIMIT_EXCEEDED", $"Rate limit exceeded for client {clientId}. Retry after {retryAfter.TotalSeconds} seconds.")
        {
            ClientId = clientId;
            RetryAfter = retryAfter;
            ErrorData["ClientId"] = clientId;
            ErrorData["RetryAfterSeconds"] = retryAfter.TotalSeconds;
        }
    }

    /// <summary>
    /// Exception thrown when circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : McpException
    {
        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets the estimated recovery time
        /// </summary>
        public TimeSpan EstimatedRecoveryTime { get; }

        public CircuitBreakerOpenException(string serviceName, TimeSpan estimatedRecoveryTime)
            : base("CIRCUIT_BREAKER_OPEN", $"Circuit breaker is open for service {serviceName}. Estimated recovery time: {estimatedRecoveryTime.TotalSeconds} seconds.")
        {
            ServiceName = serviceName;
            EstimatedRecoveryTime = estimatedRecoveryTime;
            ErrorData["ServiceName"] = serviceName;
            ErrorData["EstimatedRecoveryTimeSeconds"] = estimatedRecoveryTime.TotalSeconds;
        }
    }

    /// <summary>
    /// Exception thrown when configuration validation fails
    /// </summary>
    public class ConfigurationValidationException : McpException
    {
        /// <summary>
        /// Gets the configuration section that failed validation
        /// </summary>
        public string ConfigurationSection { get; }

        /// <summary>
        /// Gets the validation errors
        /// </summary>
        public IReadOnlyList<string> ValidationErrors { get; }

        public ConfigurationValidationException(string configurationSection, IReadOnlyList<string> validationErrors)
            : base("CONFIG_VALIDATION_FAILED", $"Configuration validation failed for section '{configurationSection}': {string.Join("; ", validationErrors)}")
        {
            ConfigurationSection = configurationSection;
            ValidationErrors = validationErrors;
            ErrorData["ConfigurationSection"] = configurationSection;
            ErrorData["ValidationErrors"] = validationErrors;
        }
    }

    /// <summary>
    /// Exception thrown when HSM operations fail
    /// </summary>
    public class HsmOperationException : McpException
    {
        /// <summary>
        /// Gets the HSM provider type
        /// </summary>
        public string ProviderType { get; }

        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; }

        public HsmOperationException(string providerType, string operation, string message)
            : base("HSM_OPERATION_FAILED", $"HSM operation '{operation}' failed for provider '{providerType}': {message}")
        {
            ProviderType = providerType;
            Operation = operation;
            ErrorData["ProviderType"] = providerType;
            ErrorData["Operation"] = operation;
        }

        public HsmOperationException(string providerType, string operation, string message, Exception innerException)
            : base("HSM_OPERATION_FAILED", $"HSM operation '{operation}' failed for provider '{providerType}': {message}", innerException)
        {
            ProviderType = providerType;
            Operation = operation;
            ErrorData["ProviderType"] = providerType;
            ErrorData["Operation"] = operation;
        }
    }

    /// <summary>
    /// Exception thrown when WebSocket operations fail
    /// </summary>
    public class WebSocketOperationException : McpException
    {
        /// <summary>
        /// Gets the WebSocket state when the error occurred
        /// </summary>
        public System.Net.WebSockets.WebSocketState WebSocketState { get; }

        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; }

        public WebSocketOperationException(string operation, System.Net.WebSockets.WebSocketState webSocketState, string message)
            : base("WEBSOCKET_OPERATION_FAILED", $"WebSocket operation '{operation}' failed in state '{webSocketState}': {message}")
        {
            Operation = operation;
            WebSocketState = webSocketState;
            ErrorData["Operation"] = operation;
            ErrorData["WebSocketState"] = webSocketState.ToString();
        }

        public WebSocketOperationException(string operation, System.Net.WebSockets.WebSocketState webSocketState, string message, Exception innerException)
            : base("WEBSOCKET_OPERATION_FAILED", $"WebSocket operation '{operation}' failed in state '{webSocketState}': {message}", innerException)
        {
            Operation = operation;
            WebSocketState = webSocketState;
            ErrorData["Operation"] = operation;
            ErrorData["WebSocketState"] = webSocketState.ToString();
        }
    }

    /// <summary>
    /// Exception thrown when protocol negotiation fails
    /// </summary>
    public class ProtocolNegotiationException : McpException
    {
        /// <summary>
        /// Gets the requested protocol
        /// </summary>
        public string RequestedProtocol { get; }

        /// <summary>
        /// Gets the supported protocols
        /// </summary>
        public IReadOnlyList<string> SupportedProtocols { get; }

        public ProtocolNegotiationException(string requestedProtocol, IReadOnlyList<string> supportedProtocols)
            : base("PROTOCOL_NEGOTIATION_FAILED", $"Protocol negotiation failed. Requested: '{requestedProtocol}', Supported: [{string.Join(", ", supportedProtocols)}]")
        {
            RequestedProtocol = requestedProtocol;
            SupportedProtocols = supportedProtocols;
            ErrorData["RequestedProtocol"] = requestedProtocol;
            ErrorData["SupportedProtocols"] = supportedProtocols;
        }
    }

    /// <summary>
    /// Exception thrown when resource operations fail
    /// </summary>
    public class ResourceOperationException : McpException
    {
        /// <summary>
        /// Gets the resource identifier
        /// </summary>
        public string ResourceId { get; }

        /// <summary>
        /// Gets the operation that failed
        /// </summary>
        public string Operation { get; }

        public ResourceOperationException(string resourceId, string operation, string message)
            : base("RESOURCE_OPERATION_FAILED", $"Resource operation '{operation}' failed for resource '{resourceId}': {message}")
        {
            ResourceId = resourceId;
            Operation = operation;
            ErrorData["ResourceId"] = resourceId;
            ErrorData["Operation"] = operation;
        }

        public ResourceOperationException(string resourceId, string operation, string message, Exception innerException)
            : base("RESOURCE_OPERATION_FAILED", $"Resource operation '{operation}' failed for resource '{resourceId}': {message}", innerException)
        {
            ResourceId = resourceId;
            Operation = operation;
            ErrorData["ResourceId"] = resourceId;
            ErrorData["Operation"] = operation;
        }
    }
}
