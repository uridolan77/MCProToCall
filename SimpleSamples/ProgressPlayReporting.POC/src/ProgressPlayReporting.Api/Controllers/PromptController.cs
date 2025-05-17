using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Api.Models;
using ProgressPlayReporting.LlmIntegration;

namespace ProgressPlayReporting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromptController : ControllerBase
    {
        private readonly PromptManagementService _promptManager;
        private readonly ILogger<PromptController> _logger;

        public PromptController(
            PromptManagementService promptManager,
            ILogger<PromptController> logger)
        {
            _promptManager = promptManager ?? throw new ArgumentNullException(nameof(promptManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public IActionResult GetPromptTemplates()
        {
            try
            {
                var templates = _promptManager.GetAllTemplates();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving prompt templates");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error retrieving prompt templates",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetPromptTemplate(string id)
        {
            try
            {
                var template = _promptManager.GetTemplate(id);
                return Ok(template);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ApiErrorResponse
                {
                    ErrorCode = "NOT_FOUND",
                    Message = $"Prompt template with ID '{id}' not found",
                    RequestId = HttpContext.TraceIdentifier
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving prompt template");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error retrieving prompt template",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpPost]
        public IActionResult CreatePromptTemplate([FromBody] PromptTemplateRequest request)
        {
            try
            {
                // Validate the request
                if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.TemplateText))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "INVALID_REQUEST",
                        Message = "Name and Template Text are required",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }

                // Create and register the template
                var template = new PromptTemplate
                {
                    Id = string.IsNullOrEmpty(request.Id) ? Guid.NewGuid().ToString() : request.Id,
                    Name = request.Name,
                    Description = request.Description,
                    TemplateText = request.TemplateText,
                    Version = request.Version ?? "1.0",
                    Tags = request.Tags ?? new List<string>()
                };

                _promptManager.RegisterTemplate(template);

                return CreatedAtAction(nameof(GetPromptTemplate), new { id = template.Id }, template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prompt template");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error creating prompt template",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("usage")]
        public IActionResult GetUsageStatistics()
        {
            try
            {
                var templates = _promptManager.GetAllTemplates();
                var usageStats = templates.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Version,
                    t.UsageCount,
                    t.LastUsed,
                    AverageResponseTime = t.ResponseLog.Count > 0 ? t.ResponseLog.Average(r => r.ExecutionTimeMs) : 0,
                    AverageResponseLength = t.ResponseLog.Count > 0 ? t.ResponseLog.Average(r => r.ResponseLength) : 0,
                    ResponseCount = t.ResponseLog.Count
                });

                return Ok(usageStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving prompt usage statistics");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error retrieving prompt usage statistics",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
    }

    public class PromptTemplateRequest
    {
        /// <summary>
        /// Optional ID for the prompt template (generated if not provided)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the template
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the template
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The actual template text with placeholders
        /// </summary>
        public string TemplateText { get; set; }

        /// <summary>
        /// Version of the template
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Tags for organizing templates
        /// </summary>
        public List<string> Tags { get; set; }
    }
}
