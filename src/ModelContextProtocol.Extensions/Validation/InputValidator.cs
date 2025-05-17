using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Schema;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Exceptions;

namespace ModelContextProtocol.Extensions.Validation
{
    /// <summary>
    /// Provides input validation capabilities for MCP/JSON-RPC requests
    /// </summary>
    public class InputValidator
    {
        private readonly ILogger<InputValidator> _logger;
        private readonly Dictionary<string, JsonSchema> _methodSchemas = new Dictionary<string, JsonSchema>();

        public InputValidator(ILogger<InputValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Register a JSON schema for validating a specific method's parameters
        /// </summary>
        /// <param name="methodName">Name of the method</param>
        /// <param name="parameterSchema">JSON schema for the method's parameters</param>
        public void RegisterMethodSchema(string methodName, string parameterSchema)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentException("Method name cannot be null or empty", nameof(methodName));
            
            if (string.IsNullOrEmpty(parameterSchema))
                throw new ArgumentException("Parameter schema cannot be null or empty", nameof(parameterSchema));
            
            try
            {
                var schema = JsonSchema.Parse(parameterSchema);
                _methodSchemas[methodName] = schema;
                _logger.LogDebug("Registered validation schema for method {MethodName}", methodName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse schema for method {MethodName}", methodName);
                throw;
            }
        }

        /// <summary>
        /// Validates the parameters for a method against its registered schema
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Validation result indicating success or failure</returns>
        public ValidationResult ValidateMethodParameters(string methodName, JsonElement parameters)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentException("Method name cannot be null or empty", nameof(methodName));
            
            // If no schema is registered for this method, validation passes
            if (!_methodSchemas.TryGetValue(methodName, out var schema))
            {
                _logger.LogDebug("No validation schema registered for method {MethodName}", methodName);
                return new ValidationResult { IsValid = true };
            }
            
            try
            {
                // Perform JSON Schema validation
                var results = schema.Validate(parameters);
                
                if (results.Any())
                {
                    // Validation failed
                    var errorMessages = results.Select(r => r.ToString()).ToList();
                    _logger.LogWarning("Validation failed for method {MethodName}: {Errors}", 
                        methodName, string.Join("; ", errorMessages));
                    
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = -32602, // Invalid params - standard JSON-RPC error code
                        ErrorMessage = "Invalid parameters: " + string.Join("; ", errorMessages)
                    };
                }
                
                // Validation passed
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation for method {MethodName}", methodName);
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorCode = -32603, // Internal error - standard JSON-RPC error code
                    ErrorMessage = "Validation error: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Validates method parameters and throws an exception if validation fails
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <exception cref="McpException">Thrown if validation fails</exception>
        public void ValidateAndThrow(string methodName, JsonElement parameters)
        {
            var result = ValidateMethodParameters(methodName, parameters);
            
            if (!result.IsValid)
            {
                throw new McpException(result.ErrorCode, result.ErrorMessage);
            }
        }

        /// <summary>
        /// Perform basic safety checks on all requests
        /// </summary>
        /// <param name="methodName">Method name to check</param>
        /// <param name="parameters">Parameters to check</param>
        /// <param name="maxDepth">Maximum allowed nesting depth</param>
        /// <param name="maxPropertyCount">Maximum allowed properties</param>
        /// <returns>Validation result</returns>
        public ValidationResult PerformBasicSafetyChecks(
            string methodName, 
            JsonElement parameters, 
            int maxDepth = 10, 
            int maxPropertyCount = 100)
        {
            try
            {
                // Check for potentially dangerous method names
                if (methodName.Contains("__") || methodName.Contains("exec") || methodName.Contains("eval"))
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = -32601, // Method not found
                        ErrorMessage = "Method not found"
                    };
                }
                
                // Check JSON structure complexity
                int actualDepth = CalculateJsonDepth(parameters);
                if (actualDepth > maxDepth)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = -32602, // Invalid params
                        ErrorMessage = $"JSON structure too complex (depth: {actualDepth}, max allowed: {maxDepth})"
                    };
                }
                
                // Check number of properties (potential DoS vector)
                int propertyCount = CountJsonProperties(parameters);
                if (propertyCount > maxPropertyCount)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorCode = -32602, // Invalid params
                        ErrorMessage = $"Too many properties in request (count: {propertyCount}, max allowed: {maxPropertyCount})"
                    };
                }
                
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during basic safety checks for method {MethodName}", methodName);
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorCode = -32603, // Internal error
                    ErrorMessage = "Error during request validation"
                };
            }
        }

        /// <summary>
        /// Calculate the maximum depth of a JSON structure
        /// </summary>
        private int CalculateJsonDepth(JsonElement element, int currentDepth = 0)
        {
            int maxDepth = currentDepth;
            
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        int depth = CalculateJsonDepth(property.Value, currentDepth + 1);
                        maxDepth = Math.Max(maxDepth, depth);
                    }
                    break;
                    
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        int depth = CalculateJsonDepth(item, currentDepth + 1);
                        maxDepth = Math.Max(maxDepth, depth);
                    }
                    break;
                    
                default:
                    maxDepth = currentDepth;
                    break;
            }
            
            return maxDepth;
        }

        /// <summary>
        /// Count the total number of properties in a JSON structure
        /// </summary>
        private int CountJsonProperties(JsonElement element)
        {
            int count = 0;
            
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        count++; // Count the property itself
                        count += CountJsonProperties(property.Value); // Count properties in the value
                    }
                    break;
                    
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        count += CountJsonProperties(item);
                    }
                    break;
            }
            
            return count;
        }
    }

    /// <summary>
    /// Result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether validation was successful
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Error code if validation failed
        /// </summary>
        public int ErrorCode { get; set; }
        
        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
