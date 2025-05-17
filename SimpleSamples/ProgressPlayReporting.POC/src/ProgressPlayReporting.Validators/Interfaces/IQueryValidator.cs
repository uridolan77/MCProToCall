using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgressPlayReporting.Validators.Interfaces
{
    /// <summary>
    /// Interface for validating SQL queries for security and correctness
    /// </summary>
    public interface IQueryValidator
    {
        /// <summary>
        /// Validates a SQL query for potential security issues and correctness
        /// </summary>
        /// <param name="query">The SQL query to validate</param>
        /// <returns>Validation result with details about any issues found</returns>
        Task<ValidationResult> ValidateQueryAsync(string query);
        
        /// <summary>
        /// Validates a query against a specific database context
        /// </summary>
        /// <param name="query">The query to validate</param>
        /// <param name="connectionString">Database connection string to validate against</param>
        /// <returns>Validation result with details about any issues found</returns>
        Task<ValidationResult> ValidateQueryAgainstDatabaseAsync(string query, string connectionString);
    }
    
    /// <summary>
    /// Result of query validation process
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the query is valid and safe to execute
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Severity level of any issues found
        /// </summary>
        public ValidationSeverity Severity { get; set; }
        
        /// <summary>
        /// List of issues found during validation
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

        /// <summary>
        /// Create a new valid result
        /// </summary>
        public static ValidationResult Valid()
        {
            return new ValidationResult { IsValid = true, Severity = ValidationSeverity.None };
        }

        /// <summary>
        /// Create a new invalid result with the specified severity
        /// </summary>
        public static ValidationResult Invalid(ValidationSeverity severity)
        {
            return new ValidationResult { IsValid = false, Severity = severity };
        }
    }

    /// <summary>
    /// Issue found during query validation
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// Issue code for identification
        /// </summary>
        public string Code { get; set; }
        
        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Severity level of the issue
        /// </summary>
        public ValidationSeverity Severity { get; set; }
        
        /// <summary>
        /// Position in the query where the issue was found (if applicable)
        /// </summary>
        public int? Position { get; set; }
    }

    /// <summary>
    /// Severity levels for validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// No issues
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Informational message
        /// </summary>
        Info = 1,
        
        /// <summary>
        /// Warning that might indicate a problem
        /// </summary>
        Warning = 2,
        
        /// <summary>
        /// Serious issue that should be fixed
        /// </summary>
        Error = 3,
        
        /// <summary>
        /// Critical security vulnerability
        /// </summary>
        Critical = 4
    }
}
