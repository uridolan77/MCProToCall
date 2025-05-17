using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Validation
{
    /// <summary>
    /// Interface for validating input against schemas
    /// </summary>
    public interface IInputValidator
    {
        /// <summary>
        /// Validates input against a schema
        /// </summary>
        /// <param name="input">The input to validate</param>
        /// <param name="schemaId">The ID of the schema to validate against</param>
        /// <returns>Validation result</returns>
        Task<ValidationResult> ValidateAsync(object input, string schemaId);

        /// <summary>
        /// Registers a schema
        /// </summary>
        /// <param name="schemaId">The ID of the schema</param>
        /// <param name="schema">The schema</param>
        void RegisterSchema(string schemaId, object schema);

        /// <summary>
        /// Gets a schema by ID
        /// </summary>
        /// <param name="schemaId">The ID of the schema</param>
        /// <returns>The schema</returns>
        object GetSchema(string schemaId);

        /// <summary>
        /// Gets all registered schemas
        /// </summary>
        /// <returns>Dictionary of schema IDs to schemas</returns>
        Dictionary<string, object> GetAllSchemas();
    }

    /// <summary>
    /// Result of validation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the validation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation errors
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        /// <summary>
        /// Error code for JSON-RPC error responses
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// Error message for JSON-RPC error responses
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="errors">Validation errors</param>
        public static ValidationResult Fail(List<ValidationError> errors)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = errors
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="error">Validation error</param>
        public static ValidationResult Fail(ValidationError error)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError> { error }
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="path">JSON path to the error</param>
        public static ValidationResult Fail(string message, string path = null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Message = message,
                        Path = path
                    }
                }
            };
        }
    }

    /// <summary>
    /// Validation error
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// JSON path to the error
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        public string ErrorCode { get; set; }
    }
}
