using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;
using ProgressPlayReporting.Api.Models;
using ProgressPlayReporting.Api.Security;
using ProgressPlayReporting.Validators.Interfaces;

namespace ProgressPlayReporting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly ISqlQueryGenerator _queryGenerator;
        private readonly ISchemaExtractor _schemaExtractor;
        private readonly IQueryValidator _queryValidator;
        private readonly ILogger<QueryController> _logger;
        private readonly IConfiguration _configuration;

        public QueryController(
            ISqlQueryGenerator queryGenerator,
            ISchemaExtractor schemaExtractor,
            IQueryValidator queryValidator,
            ILogger<QueryController> logger,
            IConfiguration configuration)
        {
            _queryGenerator = queryGenerator;
            _schemaExtractor = schemaExtractor;
            _queryValidator = queryValidator ?? throw new ArgumentNullException(nameof(queryValidator));
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQuery([FromBody] QueryGenerationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NaturalLanguageRequest))
                {
                    return BadRequest("Natural language request is required");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Get the database schema
                var schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);

                // Generate the SQL query
                var queryResult = await _queryGenerator.GenerateSqlQueryAsync(request.NaturalLanguageRequest, schema);

                return Ok(queryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SQL query");
                return StatusCode(500, "An error occurred while generating the SQL query");
            }
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateQuery([FromBody] QueryValidationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest("SQL query is required");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Get the database schema
                var schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);

                // Validate the SQL query
                var validationResult = await _queryGenerator.ValidateSqlQueryAsync(request.SqlQuery, schema);

                return Ok(validationResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SQL query");
                return StatusCode(500, "An error occurred while validating the SQL query");
            }
        }

        [HttpPost("explain")]
        public async Task<IActionResult> ExplainQuery([FromBody] QueryExplanationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest("SQL query is required");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Get the database schema if includeSchema is true
                DatabaseSchema schema = null;
                if (request.IncludeSchema)
                {
                    schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);
                }

                // Explain the SQL query
                var explanation = await _queryGenerator.ExplainSqlQueryAsync(request.SqlQuery, schema);

                return Ok(new { Explanation = explanation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error explaining SQL query");
                return StatusCode(500, "An error occurred while explaining the SQL query");
            }
        }        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteQuery([FromBody] QueryExecutionRequest request, 
            [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest(new ApiErrorResponse 
                    { 
                        ErrorCode = "MISSING_PARAMETER", 
                        Message = "SQL query is required",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 100;
                if (pageSize > 1000) pageSize = 1000; // Limit maximum page size

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest(new ApiErrorResponse 
                    { 
                        ErrorCode = "CONFIGURATION_ERROR", 
                        Message = "Database connection string not configured",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                // Validate the query for potential SQL injection
                var validationResult = await _queryValidator.ValidateQueryAsync(request.SqlQuery);
                if (!validationResult.IsValid && validationResult.Severity >= ValidationSeverity.Error)
                {
                    return BadRequest(new ApiErrorResponse 
                    { 
                        ErrorCode = "SQL_VALIDATION_ERROR", 
                        Message = "Query contains potential security threats or syntax errors",
                        Details = validationResult.Issues.ConvertAll(i => i.Message),
                        RequestId = HttpContext.TraceIdentifier
                    });
                }

                // If the request is for a paged result, modify the query to include pagination
                string pagedQuery = request.SqlQuery;
                if (!pagedQuery.ToLowerInvariant().Contains("order by"))
                {
                    _logger.LogWarning("Query does not contain ORDER BY clause which is recommended for pagination");
                }

                // Wrap the original query in a pagination query using SQL Server's OFFSET-FETCH syntax
                if (page > 1 || pageSize < 1000)
                {
                    // Simple check if the query already has an OFFSET clause
                    if (!pagedQuery.ToLowerInvariant().Contains("offset"))
                    {
                        // For SQL Server 2012 and later, add OFFSET/FETCH
                        pagedQuery = $@"
                            WITH OriginalQuery AS (
                                {request.SqlQuery}
                            )
                            SELECT * FROM OriginalQuery
                            ORDER BY (SELECT NULL)
                            OFFSET {(page - 1) * pageSize} ROWS
                            FETCH NEXT {pageSize} ROWS ONLY";
                    }
                }

                // Calculate total count (optional, can impact performance)
                int totalCount = 0;
                bool includeTotalCount = request.IncludeTotalCount;
                
                if (includeTotalCount)
                {
                    // Get total count with a separate query for accurate pagination
                    string countQuery = $@"
                        WITH OriginalQuery AS (
                            {request.SqlQuery}
                        )
                        SELECT COUNT(*) FROM OriginalQuery";
                    
                    try
                    {
                        using (var connection = new SqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            using (var command = new SqlCommand(countQuery, connection))
                            {
                                command.CommandTimeout = 30;
                                totalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error calculating total count, proceeding without total count");
                        includeTotalCount = false;
                    }
                }

                // Execute the paged query and return the results
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(pagedQuery, connection))
                    {
                        // Set a timeout to prevent long-running queries
                        command.CommandTimeout = 30; // 30 seconds

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);
                            
                            // Return result with pagination information
                            var result = new
                            {
                                Data = dataTable,
                                Pagination = new 
                                {
                                    Page = page,
                                    PageSize = pageSize,
                                    TotalCount = includeTotalCount ? totalCount : (int?)null,
                                    TotalPages = includeTotalCount ? (int)Math.Ceiling((double)totalCount / pageSize) : (int?)null
                                }
                            };
                            
                            return Ok(result);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error executing query");
                return StatusCode(400, new ApiErrorResponse 
                { 
                    ErrorCode = "SQL_ERROR", 
                    Message = "Error in SQL query",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query");
                return StatusCode(500, new ApiErrorResponse 
                { 
                    ErrorCode = "EXECUTION_ERROR", 
                    Message = "An error occurred while executing the SQL query",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
    }

    public class QueryGenerationRequest
    {
        public string NaturalLanguageRequest { get; set; }
    }

    public class QueryValidationRequest
    {
        public string SqlQuery { get; set; }
    }

    public class QueryExplanationRequest
    {
        public string SqlQuery { get; set; }
        public bool IncludeSchema { get; set; } = true;
    }

    public class QueryExecutionRequest
    {
        public string SqlQuery { get; set; }
        public bool IncludeTotalCount { get; set; } = false;
    }
}
