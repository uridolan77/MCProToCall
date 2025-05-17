using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ProgressPlayReporting.Api.Security
{
    /// <summary>
    /// Utility class for SQL query validation and protection against SQL injection
    /// </summary>
    public static class SqlInjectionValidator
    {
        // Common SQL injection patterns
        private static readonly string[] DangerousKeywords = new[] 
        {
            "xp_cmdshell",
            "sp_executesql",
            "sp_configure",
            "exec @",
            "execute @",
            "--",
            ";--",
            ";shutdown",
            "union select",
            "waitfor delay",
            "drop table",
            "drop database",
            "alter login",
            "create login"
        };

        // Regex patterns for identifying potentially dangerous SQL
        private static readonly Regex MultipleConsecutiveSemicolons = new Regex(@";;+", RegexOptions.IgnoreCase);
        private static readonly Regex CommentPattern = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex SqlUnionPattern = new Regex(@"\bunion\b.*?\bselect\b", RegexOptions.IgnoreCase);
        
        /// <summary>
        /// Validates a SQL query for potential SQL injection attempts
        /// </summary>
        /// <param name="sqlQuery">The SQL query to validate</param>
        /// <returns>Validation result with details about any issues found</returns>
        public static SqlValidationResult ValidateQuery(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                return new SqlValidationResult
                {
                    IsValid = false,
                    Issues = new List<string> { "SQL query cannot be empty" }
                };
            }

            var result = new SqlValidationResult
            {
                IsValid = true,
                Issues = new List<string>()
            };

            // Check for dangerous keywords
            foreach (var keyword in DangerousKeywords)
            {
                if (sqlQuery.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Query contains potentially dangerous keyword: {keyword}");
                }
            }

            // Check for multiple consecutive semicolons (often used in injection attempts)
            if (MultipleConsecutiveSemicolons.IsMatch(sqlQuery))
            {
                result.IsValid = false;
                result.Issues.Add("Query contains multiple consecutive semicolons");
            }

            // Check for comment blocks that might be used to comment out parts of the intended query
            if (CommentPattern.IsMatch(sqlQuery))
            {
                // Comment blocks aren't always malicious, but they're suspicious
                result.Issues.Add("Query contains comment blocks, which may be used to alter query logic");
            }

            // Check for UNION-based injection attempts
            if (SqlUnionPattern.IsMatch(sqlQuery))
            {
                // UNION SELECT isn't always malicious, but it's commonly used in injections
                result.Issues.Add("Query contains UNION SELECT pattern, verify that this is intentional");
            }

            return result;
        }

        /// <summary>
        /// Quick check if a query contains potentially unsafe elements
        /// </summary>
        /// <param name="sqlQuery">The SQL query to check</param>
        /// <returns>True if query appears safe, false otherwise</returns>
        public static bool IsSqlSafe(string sqlQuery)
        {
            var result = ValidateQuery(sqlQuery);
            return result.IsValid;
        }
    }

    /// <summary>
    /// Result of SQL query validation
    /// </summary>
    public class SqlValidationResult
    {
        /// <summary>
        /// Whether the query is considered valid/safe
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of issues found with the query
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();
    }
}
