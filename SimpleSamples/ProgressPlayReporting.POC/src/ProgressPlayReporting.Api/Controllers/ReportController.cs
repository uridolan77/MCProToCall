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
    public class ReportController : ControllerBase
    {
        private readonly IReportGenerator _reportGenerator;
        private readonly ISchemaExtractor _schemaExtractor;
        private readonly ILogger<ReportController> _logger;
        private readonly IConfiguration _configuration;

        public ReportController(
            IReportGenerator reportGenerator,
            ISchemaExtractor schemaExtractor,
            ILogger<ReportController> logger,
            IConfiguration configuration)
        {
            _reportGenerator = reportGenerator;
            _schemaExtractor = schemaExtractor;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateReport([FromBody] ReportGenerationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NaturalLanguageRequest))
                {
                    return BadRequest("Natural language request is required");
                }

                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest("SQL query is required to generate a report");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Execute the query to get the data
                DataTable data;
                try
                {
                    data = await ExecuteQueryAsync(connectionString, request.SqlQuery);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error executing SQL query: {ex.Message}");
                }

                // Get the database schema if includeSchema is true
                DatabaseSchema schema = null;
                if (request.IncludeSchema)
                {
                    schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);
                }

                // Generate the report
                var report = await _reportGenerator.GenerateReportAsync(request.NaturalLanguageRequest, data, schema);

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                return StatusCode(500, "An error occurred while generating the report: " + ex.Message);
            }
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeData([FromBody] DataAnalysisRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest("SQL query is required for data analysis");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Execute the query to get the data
                DataTable data;
                try
                {
                    data = await ExecuteQueryAsync(connectionString, request.SqlQuery);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error executing SQL query: {ex.Message}");
                }

                // Analyze the data
                var analysis = await _reportGenerator.AnalyzeDataAsync(data, request.AnalysisGoal);

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing data");
                return StatusCode(500, "An error occurred while analyzing the data: " + ex.Message);
            }
        }

        [HttpPost("visualize")]
        public async Task<IActionResult> GenerateVisualization([FromBody] VisualizationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NaturalLanguageRequest))
                {
                    return BadRequest("Natural language request is required");
                }

                if (string.IsNullOrEmpty(request.SqlQuery))
                {
                    return BadRequest("SQL query is required to generate a visualization");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                // Execute the query to get the data
                DataTable data;
                try
                {
                    data = await ExecuteQueryAsync(connectionString, request.SqlQuery);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Error executing SQL query: {ex.Message}");
                }

                // Generate the visualization configuration
                var visualization = await _reportGenerator.GenerateVisualizationConfigAsync(request.NaturalLanguageRequest, data);

                return Ok(visualization);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating visualization configuration");
                return StatusCode(500, "An error occurred while generating the visualization configuration: " + ex.Message);
            }
        }

        private async Task<DataTable> ExecuteQueryAsync(string connectionString, string sqlQuery)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(sqlQuery, connection))
                {
                    // Set a timeout to prevent long-running queries
                    command.CommandTimeout = 60; // 60 seconds

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var dataTable = new DataTable();
                        dataTable.Load(reader);
                        return dataTable;
                    }
                }
            }
        }
    }

    public class ReportGenerationRequest
    {
        public string NaturalLanguageRequest { get; set; }
        public string SqlQuery { get; set; }
        public bool IncludeSchema { get; set; } = true;
    }

    public class DataAnalysisRequest
    {
        public string SqlQuery { get; set; }
        public string AnalysisGoal { get; set; }
    }

    public class VisualizationRequest
    {
        public string NaturalLanguageRequest { get; set; }
        public string SqlQuery { get; set; }
    }
}
