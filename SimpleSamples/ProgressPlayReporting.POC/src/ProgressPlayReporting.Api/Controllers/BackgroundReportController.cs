using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Api.Models;
using ProgressPlayReporting.Api.Services.Background;
using ProgressPlayReporting.Api.Services.Export;
using ProgressPlayReporting.Core.Models.Reports;

namespace ProgressPlayReporting.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackgroundReportController : ControllerBase
    {
        private readonly ReportProcessingService _reportProcessingService;
        private readonly ExportService _exportService;
        private readonly ILogger<BackgroundReportController> _logger;
        
        public BackgroundReportController(
            ReportProcessingService reportProcessingService,
            ExportService exportService,
            ILogger<BackgroundReportController> logger)
        {
            _reportProcessingService = reportProcessingService ?? throw new ArgumentNullException(nameof(reportProcessingService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        [HttpPost("queue")]
        public IActionResult QueueReport([FromBody] QueueReportRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NaturalLanguageRequest))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "MISSING_PARAMETER",
                        Message = "Natural language request is required",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                // Create a task
                var task = new ReportTask
                {
                    ReportName = request.ReportName ?? "Generated Report",
                    Request = request.NaturalLanguageRequest,
                    VisualizationType = request.VisualizationType ?? VisualizationType.Table,
                    CreatedBy = User.Identity?.Name ?? "anonymous",
                    Priority = request.Priority ?? 0
                };
                
                // Queue the task
                var taskId = _reportProcessingService.QueueTask(task);
                
                // Return the task ID
                return Ok(new { TaskId = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing report");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error queueing report",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
        
        [HttpGet("status/{taskId}")]
        public IActionResult GetReportStatus(string taskId)
        {
            try
            {
                var result = _reportProcessingService.GetTaskResult(taskId);
                
                if (result == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode = "NOT_FOUND",
                        Message = $"Task with ID '{taskId}' not found",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                // Return status information
                return Ok(new
                {
                    result.TaskId,
                    result.Status,
                    result.QueuedAt,
                    result.StartedAt,
                    result.CompletedAt,
                    result.ExecutionTimeMs,
                    result.Error,
                    HasData = result.Data != null,
                    RowCount = result.Data?.Rows.Count,
                    HasTemplate = result.Template != null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report status");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error getting report status",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
        
        [HttpGet("result/{taskId}")]
        public IActionResult GetReportResult(string taskId)
        {
            try
            {
                var result = _reportProcessingService.GetTaskResult(taskId);
                
                if (result == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode = "NOT_FOUND",
                        Message = $"Task with ID '{taskId}' not found",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                if (result.Status != TaskStatus.Completed)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "TASK_NOT_COMPLETED",
                        Message = $"Task is not completed (current status: {result.Status})",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                // Return the full result
                return Ok(new
                {
                    result.TaskId,
                    result.Status,
                    result.QueuedAt,
                    result.StartedAt,
                    result.CompletedAt,
                    result.ExecutionTimeMs,
                    Template = result.Template,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report result");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error getting report result",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
        
        [HttpGet("export/{taskId}")]
        public async Task<IActionResult> ExportReport(string taskId, [FromQuery] string format)
        {
            try
            {
                if (string.IsNullOrEmpty(format))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "MISSING_PARAMETER",
                        Message = "Export format is required",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                var result = _reportProcessingService.GetTaskResult(taskId);
                
                if (result == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode = "NOT_FOUND",
                        Message = $"Task with ID '{taskId}' not found",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                if (result.Status != TaskStatus.Completed)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "TASK_NOT_COMPLETED",
                        Message = $"Task is not completed (current status: {result.Status})",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                if (result.Data == null || result.Template == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "NO_DATA",
                        Message = "Task completed but no data or template is available",
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
                
                // Export the data
                try
                {
                    var exportResult = await _exportService.ExportAsync(
                        result.Data,
                        format,
                        result.Template,
                        $"{result.Template.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                        
                    return File(
                        exportResult.FileContent,
                        exportResult.ContentType,
                        exportResult.Filename);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = "INVALID_EXPORT_FORMAT",
                        Message = ex.Message,
                        RequestId = HttpContext.TraceIdentifier
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error exporting report",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
        
        [HttpGet("queue")]
        public IActionResult GetQueue()
        {
            try
            {
                var tasks = _reportProcessingService.GetAllTaskResults();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report queue");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = "SERVICE_ERROR",
                    Message = "Error getting report queue",
                    Details = new List<string> { ex.Message },
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }
    }
    
    public class QueueReportRequest
    {
        /// <summary>
        /// Natural language description of the report to generate
        /// </summary>
        public string NaturalLanguageRequest { get; set; }
        
        /// <summary>
        /// Optional name for the report (will be auto-generated if not provided)
        /// </summary>
        public string ReportName { get; set; }
        
        /// <summary>
        /// Optional visualization type
        /// </summary>
        public VisualizationType? VisualizationType { get; set; }
        
        /// <summary>
        /// Optional priority (higher values = higher priority)
        /// </summary>
        public int? Priority { get; set; }
    }
}
