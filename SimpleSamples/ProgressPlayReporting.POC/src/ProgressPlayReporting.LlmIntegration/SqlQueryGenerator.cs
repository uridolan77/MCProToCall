using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// Service that uses LLMs to generate SQL queries from natural language
    /// </summary>
    public class SqlQueryGenerator : ISqlQueryGenerator
    {
        private readonly ILlmGateway _llmGateway;
        private readonly PromptManagementService _promptManager;
        private readonly ILogger<SqlQueryGenerator> _logger;

        /// <summary>
        /// Creates a new instance of SqlQueryGenerator
        /// </summary>
        /// <param name="llmGateway">The LLM gateway to use</param>
        /// <param name="promptManager">The prompt management service</param>
        /// <param name="logger">The logger to use</param>
        public SqlQueryGenerator(
            ILlmGateway llmGateway, 
            PromptManagementService promptManager,
            ILogger<SqlQueryGenerator> logger)
        {
            _llmGateway = llmGateway ?? throw new ArgumentNullException(nameof(llmGateway));
            _promptManager = promptManager ?? throw new ArgumentNullException(nameof(promptManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        /// <inheritdoc />
        public async Task<SqlQueryResult> GenerateSqlQueryAsync(string request, DatabaseSchema schema)
        {
            try
            {
                _logger.LogInformation("Generating SQL query for request: {Request}", request);

                // Use the prompt management service with the sql_query_generation template
                var parameters = new Dictionary<string, string>
                {
                    ["request"] = request,
                    ["schema"] = FormatSchemaForPrompt(schema)
                };
                
                var prompt = _promptManager.PreparePrompt("sql_query_generation", parameters);
                
                // Track execution time for analytics
                var stopwatch = Stopwatch.StartNew();
                var response = await _llmGateway.GenerateCompletionAsync(prompt);
                stopwatch.Stop();
                
                // Track the response
                _promptManager.TrackResponse("sql_query_generation", response, stopwatch.ElapsedMilliseconds);

                return ParseQueryResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SQL query");
                throw;
            }
        }        /// <inheritdoc />
        public async Task<SqlQueryValidationResult> ValidateSqlQueryAsync(string query, DatabaseSchema schema)
        {
            try
            {
                _logger.LogInformation("Validating SQL query: {QueryStart}...", 
                    query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                // Use the prompt management service with the sql_query_validation template
                var parameters = new Dictionary<string, string>
                {
                    ["query"] = query,
                    ["schema"] = FormatSchemaForPrompt(schema)
                };
                
                var prompt = _promptManager.PreparePrompt("sql_query_validation", parameters);
                
                // Track execution time for analytics
                var stopwatch = Stopwatch.StartNew();
                var response = await _llmGateway.GenerateCompletionAsync(prompt);
                stopwatch.Stop();
                
                // Track the response
                _promptManager.TrackResponse("sql_query_validation", response, stopwatch.ElapsedMilliseconds);

                return ParseValidationResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SQL query");
                throw;
            }
        }        /// <inheritdoc />
        public async Task<string> ExplainSqlQueryAsync(string query, DatabaseSchema schema = null)
        {
            try
            {
                _logger.LogInformation("Explaining SQL query: {QueryStart}...", 
                    query.Length > 50 ? query.Substring(0, 50) + "..." : query);

                // Use the prompt management service with the sql_query_explanation template
                var parameters = new Dictionary<string, string>
                {
                    ["query"] = query,
                    ["schema"] = schema != null ? FormatSchemaForPrompt(schema) : "Not provided"
                };
                
                var prompt = _promptManager.PreparePrompt("sql_query_explanation", parameters);
                
                // Track execution time for analytics
                var stopwatch = Stopwatch.StartNew();
                var explanation = await _llmGateway.GenerateCompletionAsync(prompt);
                stopwatch.Stop();
                
                // Track the response
                _promptManager.TrackResponse("sql_query_explanation", explanation, stopwatch.ElapsedMilliseconds);

                return explanation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error explaining SQL query");
                throw;
            }
        }

        private string BuildGenerationPrompt(string request, DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert SQL developer specializing in data analysis and reporting for the gaming industry.");
            sb.AppendLine("Generate a SQL query based on the following request and database schema.");
            sb.AppendLine("\nRequest:");
            sb.AppendLine(request);
            
            sb.AppendLine("\nDatabase Schema:");
            sb.AppendLine(FormatSchemaForPrompt(schema));

            sb.AppendLine("\nGenerate a SQL query for the above request using the provided schema.");
            sb.AppendLine("Return your response in the following JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("  \"query\": \"SQL query here\",");
            sb.AppendLine("  \"explanation\": \"Explanation of the query and how it addresses the request\",");
            sb.AppendLine("  \"tablesUsed\": [\"Table1\", \"Table2\"],");
            sb.AppendLine("  \"columnsReferenced\": [\"Table1.Column1\", \"Table2.Column2\"],");
            sb.AppendLine("  \"warnings\": [\"Any warnings or notes about the query\"]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string BuildValidationPrompt(string query, DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert SQL developer specializing in data analysis and reporting for the gaming industry.");
            sb.AppendLine("Validate and fix (if needed) the following SQL query based on the provided database schema.");
            sb.AppendLine("\nSQL Query:");
            sb.AppendLine(query);
            
            sb.AppendLine("\nDatabase Schema:");
            sb.AppendLine(FormatSchemaForPrompt(schema));

            sb.AppendLine("\nValidate the SQL query and identify any issues. If there are issues, provide a corrected version.");
            sb.AppendLine("Return your response in the following JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("  \"isValid\": true/false,");
            sb.AppendLine("  \"correctedQuery\": \"Fixed SQL query here (same as input if valid)\",");
            sb.AppendLine("  \"issues\": [\"List of issues found\"],");
            sb.AppendLine("  \"changeExplanation\": \"Explanation of changes made to fix issues\"");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string BuildExplanationPrompt(string query, DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert SQL developer who specializes in explaining complex SQL queries to non-technical audiences.");
            sb.AppendLine("Explain the following SQL query in clear, concise language that a business user would understand.");
            sb.AppendLine("\nSQL Query:");
            sb.AppendLine(query);
            
            if (schema != null)
            {
                sb.AppendLine("\nDatabase Schema:");
                sb.AppendLine(FormatSchemaForPrompt(schema));
            }

            sb.AppendLine("\nProvide an explanation that includes:");
            sb.AppendLine("1. What information the query is retrieving");
            sb.AppendLine("2. How the data is being filtered or processed");
            sb.AppendLine("3. What business question this query answers");
            sb.AppendLine("4. Any key calculations or transformations being performed");

            return sb.ToString();
        }

        private string FormatSchemaForPrompt(DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Database: {schema.DatabaseName}");
            
            foreach (var table in schema.Tables)
            {
                sb.AppendLine($"\nTable: {table.TableName}");
                sb.AppendLine("Columns:");
                
                foreach (var column in table.Columns)
                {
                    var constraints = new List<string>();
                    if (column.IsPrimaryKey) constraints.Add("PRIMARY KEY");
                    if (column.IsForeignKey) constraints.Add($"FOREIGN KEY REFERENCES {column.ReferencedTable}({column.ReferencedColumn})");
                    if (!column.IsNullable) constraints.Add("NOT NULL");
                    
                    var constraintText = constraints.Any() ? $" ({string.Join(", ", constraints)})" : "";
                    sb.AppendLine($"  - {column.ColumnName}: {column.DataType}{constraintText}");
                }
            }

            if (schema.Dimensions.Any())
            {
                sb.AppendLine("\nDimension Tables:");
                foreach (var dimension in schema.Dimensions)
                {
                    sb.AppendLine($"  - {dimension}");
                }
            }

            if (schema.Facts.Any())
            {
                sb.AppendLine("\nFact Tables:");
                foreach (var fact in schema.Facts)
                {
                    sb.AppendLine($"  - {fact}");
                }
            }

            return sb.ToString();
        }

        private SqlQueryResult ParseQueryResponse(string response)
        {
            try
            {
                // Extract JSON object from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<SqlQueryResult>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                // Fallback: Basic parsing if JSON structure is not detected
                _logger.LogWarning("Response not in expected JSON format, attempting basic parsing");
                
                var result = new SqlQueryResult();
                
                // Look for SQL query pattern
                var sqlStart = response.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
                if (sqlStart >= 0)
                {
                    // Find the end of the query (usually marked by a line break followed by text)
                    var sqlEnd = response.IndexOf("\n\n", sqlStart);
                    if (sqlEnd < 0) sqlEnd = response.Length;
                    
                    result.Query = response.Substring(sqlStart, sqlEnd - sqlStart).Trim();
                    result.Explanation = "Extracted from non-JSON response";
                }
                else
                {
                    result.Query = "";
                    result.Explanation = "Could not extract SQL query from response";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing query response");
                return new SqlQueryResult
                {
                    Query = "",
                    Explanation = "Error parsing response: " + ex.Message,
                    Warnings = new List<string> { "Failed to parse LLM response" }
                };
            }
        }

        private SqlQueryValidationResult ParseValidationResponse(string response)
        {
            try
            {
                // Extract JSON object from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<SqlQueryValidationResult>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                // Fallback: Basic parsing if JSON structure is not detected
                _logger.LogWarning("Response not in expected JSON format, attempting basic parsing");
                
                var result = new SqlQueryValidationResult();
                
                if (response.Contains("valid", StringComparison.OrdinalIgnoreCase) && 
                    !response.Contains("not valid", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = true;
                }
                
                // Look for SQL query pattern
                var sqlStart = response.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
                if (sqlStart >= 0)
                {
                    // Find the end of the query (usually marked by a line break followed by text)
                    var sqlEnd = response.IndexOf("\n\n", sqlStart);
                    if (sqlEnd < 0) sqlEnd = response.Length;
                    
                    result.CorrectedQuery = response.Substring(sqlStart, sqlEnd - sqlStart).Trim();
                }
                
                result.ChangeExplanation = "Extracted from non-JSON response";
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing validation response");
                return new SqlQueryValidationResult
                {
                    IsValid = false,
                    CorrectedQuery = "",
                    Issues = new List<string> { "Failed to parse LLM response: " + ex.Message },
                    ChangeExplanation = "Error parsing response"
                };
            }
        }
    }
}
