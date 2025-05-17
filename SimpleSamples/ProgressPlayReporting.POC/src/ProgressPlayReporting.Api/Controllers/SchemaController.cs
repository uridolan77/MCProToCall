using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly ISchemaExtractor _schemaExtractor;
        private readonly ILogger<SchemaController> _logger;
        private readonly IConfiguration _configuration;

        public SchemaController(
            ISchemaExtractor schemaExtractor,
            ILogger<SchemaController> logger,
            IConfiguration configuration)
        {
            _schemaExtractor = schemaExtractor;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetDatabaseSchema()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                var schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);
                return Ok(schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database schema");
                return StatusCode(500, "An error occurred while retrieving the database schema");
            }
        }

        [HttpGet("tables")]
        public async Task<IActionResult> GetAllTables()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                var tableNames = await _schemaExtractor.GetAllTableNamesAsync(connectionString);
                return Ok(tableNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table names");
                return StatusCode(500, "An error occurred while retrieving table names");
            }
        }

        [HttpGet("tables/{tableName}")]
        public async Task<IActionResult> GetTableSchema(string tableName)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("Database connection string not configured");
                }

                var tableSchema = await _schemaExtractor.GetTableSchemaAsync(connectionString, tableName);
                return Ok(tableSchema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schema for table {TableName}", tableName);
                return StatusCode(500, $"An error occurred while retrieving schema for table {tableName}");
            }
        }
    }
}
