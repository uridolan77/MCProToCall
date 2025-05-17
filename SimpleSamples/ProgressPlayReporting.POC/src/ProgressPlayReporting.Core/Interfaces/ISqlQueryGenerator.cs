using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgressPlayReporting.Core.Interfaces
{
    /// <summary>
    /// Interface for generating SQL queries using an LLM
    /// </summary>
    public interface ISqlQueryGenerator
    {
        /// <summary>
        /// Generates a SQL query based on natural language request and database schema
        /// </summary>
        /// <param name="request">Natural language description of the report/query needed</param>
        /// <param name="schema">Database schema to use for context</param>
        /// <returns>Generated SQL query and explanation</returns>
        Task<SqlQueryResult> GenerateSqlQueryAsync(string request, DatabaseSchema schema);
        
        /// <summary>
        /// Validates and potentially fixes a SQL query for the given database schema
        /// </summary>
        /// <param name="query">The SQL query to validate</param>
        /// <param name="schema">Database schema to validate against</param>
        /// <returns>Validation result with potential fixes</returns>
        Task<SqlQueryValidationResult> ValidateSqlQueryAsync(string query, DatabaseSchema schema);
        
        /// <summary>
        /// Explains the logic and structure of a SQL query in natural language
        /// </summary>
        /// <param name="query">The SQL query to explain</param>
        /// <param name="schema">Optional database schema for context</param>
        /// <returns>Natural language explanation of the query</returns>
        Task<string> ExplainSqlQueryAsync(string query, DatabaseSchema schema = null);
    }

    /// <summary>
    /// Result of SQL query generation
    /// </summary>
    public class SqlQueryResult
    {
        /// <summary>
        /// The generated SQL query
        /// </summary>
        public string Query { get; set; }
        
        /// <summary>
        /// Natural language explanation of the query
        /// </summary>
        public string Explanation { get; set; }
        
        /// <summary>
        /// Tables used in the query
        /// </summary>
        public List<string> TablesUsed { get; set; } = new List<string>();
        
        /// <summary>
        /// Columns referenced in the query
        /// </summary>
        public List<string> ColumnsReferenced { get; set; } = new List<string>();
        
        /// <summary>
        /// Any warnings or notes about the generated query
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of SQL query validation
    /// </summary>
    public class SqlQueryValidationResult
    {
        /// <summary>
        /// Whether the query is valid
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Fixed/corrected SQL query if the original had issues
        /// </summary>
        public string CorrectedQuery { get; set; }
        
        /// <summary>
        /// Description of any issues found
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();
        
        /// <summary>
        /// Explanation of changes made to fix issues
        /// </summary>
        public string ChangeExplanation { get; set; }
    }
}
