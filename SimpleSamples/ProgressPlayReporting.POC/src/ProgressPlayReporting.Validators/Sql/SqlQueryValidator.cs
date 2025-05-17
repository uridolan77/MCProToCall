using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProgressPlayReporting.Validators.Interfaces;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.Validators.Sql
{
    /// <summary>
    /// SQL query validator that checks for security issues and correctness
    /// </summary>
    public class SqlQueryValidator : IQueryValidator
    {
        private readonly ILogger<SqlQueryValidator> _logger;
        
        // Blacklisted patterns that indicate potential SQL injection
        private static readonly string[] DangerousKeywords = new[] 
        {
            "xp_cmdshell",
            "sp_executesql",
            "sp_configure",
            "exec @",
            "execute @",
            "into outfile",
            "into dumpfile",
            ";shutdown",
            "waitfor delay",
            "drop table",
            "drop database",
            "alter login",
            "create login",
            "master..sysobjects",
            "msdb.dbo.backuphistory"
        };

        // Regex patterns for more complex security checks
        private static readonly Regex BatchSeparatorPattern = new Regex(@";\s*(\n|$)", RegexOptions.IgnoreCase);
        private static readonly Regex CommentPattern = new Regex(@"/\*.*?\*/|--.*?$", RegexOptions.Multiline | RegexOptions.Singleline);
        private static readonly Regex SqlUnionPattern = new Regex(@"\bunion\b.*?\bselect\b", RegexOptions.IgnoreCase);
        private static readonly Regex HexEncodedStringPattern = new Regex(@"0x[0-9a-fA-F]{10,}", RegexOptions.IgnoreCase);
        private static readonly Regex MultipleConsecutiveSemicolons = new Regex(@";;+", RegexOptions.IgnoreCase);
        
        /// <summary>
        /// Creates a new instance of the SQL query validator
        /// </summary>
        public SqlQueryValidator(ILogger<SqlQueryValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public Task<ValidationResult> ValidateQueryAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                var result = ValidationResult.Invalid(ValidationSeverity.Error);
                result.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_EMPTY",
                    Message = "SQL query cannot be empty",
                    Severity = ValidationSeverity.Error
                });
                return Task.FromResult(result);
            }
            
            var validationResult = ValidationResult.Valid();
            
            // Check for dangerous keywords
            foreach (var keyword in DangerousKeywords)
            {
                if (query.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    validationResult.IsValid = false;
                    validationResult.Severity = ValidationSeverity.Critical;
                    validationResult.Issues.Add(new ValidationIssue
                    {
                        Code = "SQL_DANGEROUS_KEYWORD",
                        Message = $"Query contains potentially dangerous keyword: {keyword}",
                        Severity = ValidationSeverity.Critical
                    });
                }
            }
            
            // Check for multiple statements (which could be used for injection)
            if (BatchSeparatorPattern.IsMatch(query))
            {
                validationResult.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_MULTIPLE_STATEMENTS", 
                    Message = "Query contains multiple SQL statements (semicolon followed by a newline)",
                    Severity = ValidationSeverity.Warning
                });
                
                // Only set IsValid to false if no critical issues were found yet
                if (validationResult.Severity < ValidationSeverity.Critical)
                {
                    validationResult.IsValid = false;
                    validationResult.Severity = ValidationSeverity.Warning;
                }
            }
            
            // Check for multiple consecutive semicolons (often used in injection attempts)
            if (MultipleConsecutiveSemicolons.IsMatch(query))
            {
                validationResult.IsValid = false;
                validationResult.Severity = ValidationSeverity.Error;
                validationResult.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_CONSECUTIVE_SEMICOLONS",
                    Message = "Query contains multiple consecutive semicolons",
                    Severity = ValidationSeverity.Error
                });
            }
            
            // Check for comment blocks that might be used to comment out parts of the intended query
            if (CommentPattern.IsMatch(query))
            {
                validationResult.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_COMMENTS",
                    Message = "Query contains comment blocks, which may be used to alter query logic",
                    Severity = ValidationSeverity.Info
                });
            }
            
            // Check for UNION-based injection attempts
            if (SqlUnionPattern.IsMatch(query))
            {
                validationResult.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_UNION_SELECT",
                    Message = "Query contains UNION SELECT pattern, verify that this is intentional",
                    Severity = ValidationSeverity.Warning
                });
                
                // Only set IsValid to false if no critical or error issues were found yet
                if (validationResult.Severity < ValidationSeverity.Error)
                {
                    validationResult.IsValid = false;
                    validationResult.Severity = ValidationSeverity.Warning;
                }
            }
            
            // Check for hex-encoded strings (potential obfuscation)
            if (HexEncodedStringPattern.IsMatch(query))
            {
                validationResult.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_HEX_ENCODING",
                    Message = "Query contains hex-encoded strings, which may be used to obfuscate malicious code",
                    Severity = ValidationSeverity.Warning
                });
                
                // Only set IsValid to false if no critical or error issues were found yet
                if (validationResult.Severity < ValidationSeverity.Error)
                {
                    validationResult.IsValid = false;
                    validationResult.Severity = ValidationSeverity.Warning;
                }
            }
            
            return Task.FromResult(validationResult);
        }
        
        /// <inheritdoc />
        public async Task<ValidationResult> ValidateQueryAgainstDatabaseAsync(string query, string connectionString)
        {
            // First perform static analysis
            var result = await ValidateQueryAsync(query);
            
            // If the query has critical issues, don't bother checking against the database
            if (result.Severity == ValidationSeverity.Critical)
            {
                return result;
            }
            
            try
            {
                // Check if the query is valid by executing a query plan request without running the query
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Use SET SHOWPLAN_TEXT to check query validity without executing
                    using (var commandShowPlan = new SqlCommand("SET SHOWPLAN_TEXT ON", connection))
                    {
                        await commandShowPlan.ExecuteNonQueryAsync();
                    }
                    
                    try
                    {
                        using (var command = new SqlCommand(query, connection))
                        {
                            // Set a short timeout to avoid long operations
                            command.CommandTimeout = 5;
                            
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                // If it gets here, the query is syntactically valid
                                // The result set contains the execution plan, but we don't need it
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        // Query syntax error
                        result.IsValid = false;
                        result.Severity = ValidationSeverity.Error;
                        result.Issues.Add(new ValidationIssue
                        {
                            Code = "SQL_SYNTAX_ERROR",
                            Message = $"SQL syntax error: {ex.Message}",
                            Severity = ValidationSeverity.Error
                        });
                    }
                    finally
                    {
                        // Turn off showplan
                        using (var commandShowPlan = new SqlCommand("SET SHOWPLAN_TEXT OFF", connection))
                        {
                            await commandShowPlan.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Unable to connect or other general error
                _logger.LogError(ex, "Error validating query against database");
                result.Issues.Add(new ValidationIssue
                {
                    Code = "SQL_VALIDATION_ERROR",
                    Message = $"Error validating query against database: {ex.Message}",
                    Severity = ValidationSeverity.Error
                });
            }
            
            return result;
        }
    }
}
