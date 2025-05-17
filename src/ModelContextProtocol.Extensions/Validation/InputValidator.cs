using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Exceptions;

namespace ModelContextProtocol.Extensions.Validation
{
    /// <summary>
    /// Provides input validation capabilities for MCP/JSON-RPC requests
    /// </summary>
    public class InputValidator : IInputValidator
    {
        private readonly ILogger<InputValidator> _logger;
        private readonly Dictionary<string, object> _schemas = new Dictionary<string, object>();

        public InputValidator(ILogger<InputValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers a schema
        /// </summary>
        /// <param name="schemaId">The ID of the schema</param>
        /// <param name="schema">The schema</param>
        public void RegisterSchema(string schemaId, object schema)
        {
            if (string.IsNullOrEmpty(schemaId))
                throw new ArgumentException("Schema ID cannot be null or empty", nameof(schemaId));

            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            try
            {
                _schemas[schemaId] = schema;
                _logger.LogDebug("Registered schema with ID {SchemaId}", schemaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register schema with ID {SchemaId}", schemaId);
                throw;
            }
        }

        /// <summary>
        /// Gets a schema by ID
        /// </summary>
        /// <param name="schemaId">The ID of the schema</param>
        /// <returns>The schema</returns>
        public object GetSchema(string schemaId)
        {
            if (string.IsNullOrEmpty(schemaId))
                throw new ArgumentException("Schema ID cannot be null or empty", nameof(schemaId));

            if (_schemas.TryGetValue(schemaId, out var schema))
            {
                return schema;
            }

            return null;
        }

        /// <summary>
        /// Gets all registered schemas
        /// </summary>
        /// <returns>Dictionary of schema IDs to schemas</returns>
        public Dictionary<string, object> GetAllSchemas()
        {
            return new Dictionary<string, object>(_schemas);
        }

        /// <summary>
        /// Validates input against a schema
        /// </summary>
        /// <param name="input">The input to validate</param>
        /// <param name="schemaId">The ID of the schema to validate against</param>
        /// <returns>Validation result</returns>
        public async Task<ValidationResult> ValidateAsync(object input, string schemaId)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (string.IsNullOrEmpty(schemaId))
                throw new ArgumentException("Schema ID cannot be null or empty", nameof(schemaId));

            // If no schema is registered for this ID, validation passes
            if (!_schemas.TryGetValue(schemaId, out var schema))
            {
                _logger.LogDebug("No validation schema registered with ID {SchemaId}", schemaId);
                return ValidationResult.Success();
            }

            try
            {
                // For now, we'll just do a simple validation
                // In a real implementation, we would use a proper JSON Schema validator

                // Convert input to JSON for validation
                var inputJson = JsonSerializer.Serialize(input);
                var inputElement = JsonDocument.Parse(inputJson).RootElement;

                // Perform basic validation
                var basicChecks = PerformBasicSafetyChecks(schemaId, inputElement);
                if (!basicChecks.IsValid)
                {
                    return ValidationResult.Fail(new ValidationError
                    {
                        Message = basicChecks.ErrorMessage,
                        ErrorCode = basicChecks.ErrorCode.ToString()
                    });
                }

                // In a real implementation, we would validate against the schema here
                // For now, we'll just return success

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation for schema {SchemaId}", schemaId);
                return ValidationResult.Fail($"Validation error: {ex.Message}");
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
            if (!_schemas.TryGetValue(methodName, out var schema))
            {
                _logger.LogDebug("No validation schema registered for method {MethodName}", methodName);
                return new ValidationResult { IsValid = true };
            }

            try
            {
                // In a real implementation, we would validate against the schema here
                // For now, we'll just do basic safety checks

                var basicChecks = PerformBasicSafetyChecks(methodName, parameters);
                if (!basicChecks.IsValid)
                {
                    return basicChecks;
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


}
