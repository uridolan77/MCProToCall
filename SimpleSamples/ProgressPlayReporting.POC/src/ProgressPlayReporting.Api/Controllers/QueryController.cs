using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly ISqlQueryGenerator _queryGenerator;
        private readonly ISchemaExtractor _schemaExtractor;
        private readonly ILogger<QueryController> _logger;
        private readonly IConfiguration _configuration;

        public QueryController(
            ISqlQueryGenerator queryGenerator,
            ISchemaExtractor schemaExtractor,
            ILogger<QueryController> logger,
            IConfiguration configuration)
        {
            _queryGenerator = queryGenerator;
            _schemaExtractor = schemaExtractor;
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
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteQuery([FromBody] QueryExecutionRequest request)
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

                // Execute the query and return the results
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(request.SqlQuery, connection))
                    {
                        // Set a timeout to prevent long-running queries
                        command.CommandTimeout = 30; // 30 seconds

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);
                            return Ok(dataTable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query");
                return StatusCode(500, "An error occurred while executing the SQL query: " + ex.Message);
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
    }
}
