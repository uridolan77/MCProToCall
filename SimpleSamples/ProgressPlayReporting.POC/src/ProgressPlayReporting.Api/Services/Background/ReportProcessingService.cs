using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ProgressPlayReporting.Core.Interfaces;
using ProgressPlayReporting.Core.Models.Reports;

namespace ProgressPlayReporting.Api.Services.Background
{
    /// <summary>
    /// Background service for processing report generation tasks
    /// </summary>
    public class ReportProcessingService : BackgroundService
    {
        private readonly ILogger<ReportProcessingService> _logger;
        private readonly ISchemaExtractor _schemaExtractor;
        private readonly ISqlQueryGenerator _queryGenerator;
        private readonly IConfiguration _configuration;
        
        // Thread-safe queue for report generation tasks
        private readonly ConcurrentQueue<ReportTask> _taskQueue = new ConcurrentQueue<ReportTask>();
        
        // Dictionary to store task results
        private readonly ConcurrentDictionary<string, ReportTaskResult> _taskResults = new ConcurrentDictionary<string, ReportTaskResult>();
        
        // Semaphore to limit concurrent processing
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(2, 2); // Allow 2 concurrent tasks
        
        /// <summary>
        /// Creates a new report processing service
        /// </summary>
        public ReportProcessingService(
            ILogger<ReportProcessingService> logger,
            ISchemaExtractor schemaExtractor,
            ISqlQueryGenerator queryGenerator,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schemaExtractor = schemaExtractor ?? throw new ArgumentNullException(nameof(schemaExtractor));
            _queryGenerator = queryGenerator ?? throw new ArgumentNullException(nameof(queryGenerator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        
        /// <summary>
        /// Queue a new report generation task
        /// </summary>
        /// <param name="task">The report task to queue</param>
        /// <returns>The task ID for tracking</returns>
        public string QueueTask(ReportTask task)
        {
            // Generate a task ID if not provided
            if (string.IsNullOrEmpty(task.Id))
            {
                task.Id = Guid.NewGuid().ToString();
            }
            
            // Set task status to queued
            task.Status = TaskStatus.Queued;
            task.QueuedAt = DateTime.UtcNow;
            
            // Initialize the result
            _taskResults[task.Id] = new ReportTaskResult
            {
                TaskId = task.Id,
                Status = TaskStatus.Queued,
                QueuedAt = task.QueuedAt
            };
            
            // Add to queue
            _taskQueue.Enqueue(task);
            
            _logger.LogInformation("Queued report task {TaskId} for {ReportName}", task.Id, task.ReportName);
            
            return task.Id;
        }
        
        /// <summary>
        /// Get the result of a task by ID
        /// </summary>
        public ReportTaskResult GetTaskResult(string taskId)
        {
            if (_taskResults.TryGetValue(taskId, out var result))
            {
                return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get all task results
        /// </summary>
        public IEnumerable<ReportTaskResult> GetAllTaskResults()
        {
            return _taskResults.Values;
        }
        
        /// <summary>
        /// Clear completed tasks older than the specified time
        /// </summary>
        public void ClearOldTasks(TimeSpan age)
        {
            var cutoff = DateTime.UtcNow - age;
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _taskResults)
            {
                if ((kvp.Value.Status == TaskStatus.Completed || kvp.Value.Status == TaskStatus.Failed) &&
                    kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt.Value < cutoff)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _taskResults.TryRemove(key, out _);
            }
            
            _logger.LogInformation("Removed {Count} old completed tasks", keysToRemove.Count);
        }
        
        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Report processing service is starting");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Dequeue a task if available
                    if (_taskQueue.TryDequeue(out var task))
                    {
                        // Acquire semaphore to limit concurrent processing
                        await _semaphore.WaitAsync(stoppingToken);
                        
                        // Process task in background
                        _ = ProcessTaskAsync(task)
                            .ContinueWith(_ => _semaphore.Release(), TaskScheduler.Default);
                    }
                    else
                    {
                        // No tasks, wait before checking again
                        await Task.Delay(1000, stoppingToken);
                    }
                    
                    // Clean up old tasks every hour
                    if (DateTime.UtcNow.Minute == 0 && DateTime.UtcNow.Second < 5)
                    {
                        ClearOldTasks(TimeSpan.FromDays(7)); // Keep tasks for 7 days
                        await Task.Delay(10000, stoppingToken); // Wait to avoid multiple cleanups
                    }
                }
                catch (OperationCanceledException)
                {
                    // Stopping token was canceled
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in report processing service");
                    await Task.Delay(5000, stoppingToken); // Wait before retrying
                }
            }
            
            _logger.LogInformation("Report processing service is stopping");
        }
        
        private async Task ProcessTaskAsync(ReportTask task)
        {
            var result = _taskResults[task.Id];
            
            try
            {
                _logger.LogInformation("Processing report task {TaskId} for {ReportName}", task.Id, task.ReportName);
                
                // Update status to processing
                task.Status = TaskStatus.Processing;
                task.StartedAt = DateTime.UtcNow;
                
                result.Status = TaskStatus.Processing;
                result.StartedAt = task.StartedAt;
                
                // Get the database schema
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var schema = await _schemaExtractor.ExtractSchemaAsync(connectionString);
                
                // Generate the SQL query
                var queryResult = await _queryGenerator.GenerateSqlQueryAsync(task.Request, schema);
                
                // Create a report template
                var template = new ReportTemplate
                {
                    Name = task.ReportName,
                    Description = queryResult.Explanation,
                    SqlQuery = queryResult.Query,
                    OriginalPrompt = task.Request,
                    VisualSettings = new ReportVisualSettings
                    {
                        Title = task.ReportName,
                        VisualizationType = task.VisualizationType
                    },
                    CreatedBy = task.CreatedBy
                };
                
                // Execute the query to get the data
                DataTable data;
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(queryResult.Query, connection))
                    {
                        command.CommandTimeout = 60; // 60 seconds
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            data = new DataTable();
                            data.Load(reader);
                        }
                    }
                }
                
                // Update the result
                result.Status = TaskStatus.Completed;
                result.CompletedAt = DateTime.UtcNow;
                result.Template = template;
                result.Data = data;
                result.ExecutionTimeMs = (result.CompletedAt.Value - task.StartedAt.Value).TotalMilliseconds;
                
                _logger.LogInformation("Completed report task {TaskId} in {ExecutionTime}ms", 
                    task.Id, result.ExecutionTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing report task {TaskId}", task.Id);
                
                // Update the result with error
                result.Status = TaskStatus.Failed;
                result.CompletedAt = DateTime.UtcNow;
                result.Error = ex.Message;
                
                if (task.StartedAt.HasValue)
                {
                    result.ExecutionTimeMs = (result.CompletedAt.Value - task.StartedAt.Value).TotalMilliseconds;
                }
            }
        }
    }
    
    /// <summary>
    /// Task for background report generation
    /// </summary>
    public class ReportTask
    {
        /// <summary>
        /// Unique identifier for the task
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Name for the report
        /// </summary>
        public string ReportName { get; set; }
        
        /// <summary>
        /// Natural language request for the report
        /// </summary>
        public string Request { get; set; }
        
        /// <summary>
        /// Type of visualization to use
        /// </summary>
        public VisualizationType VisualizationType { get; set; } = VisualizationType.Table;
        
        /// <summary>
        /// Current status of the task
        /// </summary>
        public TaskStatus Status { get; set; } = TaskStatus.Created;
        
        /// <summary>
        /// When the task was queued
        /// </summary>
        public DateTime QueuedAt { get; set; }
        
        /// <summary>
        /// When the task started processing
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// User who created the task
        /// </summary>
        public string CreatedBy { get; set; } = "anonymous";
        
        /// <summary>
        /// Priority of the task (higher values = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;
    }
    
    /// <summary>
    /// Result of a report generation task
    /// </summary>
    public class ReportTaskResult
    {
        /// <summary>
        /// ID of the task this result is for
        /// </summary>
        public string TaskId { get; set; }
        
        /// <summary>
        /// Current status of the task
        /// </summary>
        public TaskStatus Status { get; set; }
        
        /// <summary>
        /// When the task was queued
        /// </summary>
        public DateTime QueuedAt { get; set; }
        
        /// <summary>
        /// When the task started processing
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// When the task completed (success or failure)
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Error message if the task failed
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public double ExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Report template generated
        /// </summary>
        public ReportTemplate Template { get; set; }
        
        /// <summary>
        /// Report data generated
        /// </summary>
        public DataTable Data { get; set; }
    }
    
    /// <summary>
    /// Status of a background task
    /// </summary>
    public enum TaskStatus
    {
        Created,
        Queued,
        Processing,
        Completed,
        Failed
    }
}
