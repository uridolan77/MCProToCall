using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// Service for prompt management, versioning, and tracking
    /// </summary>
    public class PromptManagementService
    {
        private readonly ILogger<PromptManagementService> _logger;
        private readonly Dictionary<string, PromptTemplate> _templates = new Dictionary<string, PromptTemplate>();
        
        public PromptManagementService(ILogger<PromptManagementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize with default templates
            InitializeDefaultTemplates();
        }
        
        /// <summary>
        /// Get a prompt template by ID
        /// </summary>
        public PromptTemplate GetTemplate(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                throw new ArgumentNullException(nameof(templateId));
            }
            
            if (_templates.TryGetValue(templateId, out var template))
            {
                return template;
            }
            
            throw new KeyNotFoundException($"Prompt template with ID '{templateId}' not found");
        }
        
        /// <summary>
        /// Register a new prompt template
        /// </summary>
        public void RegisterTemplate(PromptTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            
            if (string.IsNullOrEmpty(template.Id))
            {
                template.Id = Guid.NewGuid().ToString();
            }
            
            _templates[template.Id] = template;
            _logger.LogInformation("Registered prompt template: {TemplateId} - {TemplateName}", template.Id, template.Name);
        }
        
        /// <summary>
        /// Prepare a prompt by filling in placeholders with values
        /// </summary>
        public string PreparePrompt(string templateId, Dictionary<string, string> parameters)
        {
            var template = GetTemplate(templateId);
            
            // Track prompt usage
            template.UsageCount++;
            template.LastUsed = DateTime.UtcNow;
            
            // Apply any global variables
            var allParameters = new Dictionary<string, string>(parameters);
            AddGlobalVariables(allParameters);
            
            // Replace placeholders with values
            return ReplacePlaceholders(template.TemplateText, allParameters);
        }
        
        /// <summary>
        /// Track a response to a prompt for analytics
        /// </summary>
        public void TrackResponse(string templateId, string response, double executionTimeMs)
        {
            if (_templates.TryGetValue(templateId, out var template))
            {
                template.ResponseLog.Add(new PromptResponse
                {
                    Timestamp = DateTime.UtcNow,
                    ResponseLength = response?.Length ?? 0,
                    ExecutionTimeMs = executionTimeMs
                });
                
                // Keep log size manageable
                if (template.ResponseLog.Count > 100)
                {
                    template.ResponseLog.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// Get all registered templates
        /// </summary>
        public IEnumerable<PromptTemplate> GetAllTemplates()
        {
            return _templates.Values;
        }
        
        private string ReplacePlaceholders(string template, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }
            
            string result = template;
            
            foreach (var param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value);
            }
            
            return result;
        }
        
        private void AddGlobalVariables(Dictionary<string, string> parameters)
        {
            parameters["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            parameters["time"] = DateTime.UtcNow.ToString("HH:mm:ss");
            parameters["datetime"] = DateTime.UtcNow.ToString("o");
        }
        
        private void InitializeDefaultTemplates()
        {
            // SQL Query Generation
            RegisterTemplate(new PromptTemplate
            {
                Id = "sql_query_generation",
                Name = "SQL Query Generation",
                Description = "Generate a SQL query from a natural language request",
                TemplateText = @"You are a SQL expert. Generate a SQL query for SQL Server based on the following natural language request:

Natural Language Request: {request}

Database Schema:
{schema}

Generate ONLY the SQL query that answers the request. The query should be optimized, secure, and follow best practices.
If the request is ambiguous, make reasonable assumptions and explain them in a comment.
",
                Version = "1.0"
            });
            
            // SQL Query Validation
            RegisterTemplate(new PromptTemplate
            {
                Id = "sql_query_validation",
                Name = "SQL Query Validation",
                Description = "Validate and fix a SQL query",
                TemplateText = @"You are a SQL expert. Review the following SQL query and identify any issues, then provide a corrected version:

SQL Query:
{query}

Database Schema:
{schema}

Identify any issues with the query, including:
- Syntax errors
- Performance problems
- Security concerns
- Logic issues

Then provide a corrected version of the query with improvements.
",
                Version = "1.0"
            });
            
            // SQL Query Explanation
            RegisterTemplate(new PromptTemplate
            {
                Id = "sql_query_explanation",
                Name = "SQL Query Explanation",
                Description = "Explain a SQL query in natural language",
                TemplateText = @"You are a SQL expert. Explain the following SQL query in plain English:

SQL Query:
{query}

Database Schema:
{schema}

Provide a clear explanation of what this query does, including:
1. What data it retrieves or modifies
2. Any filtering, grouping, or aggregation
3. The business meaning of the query
4. Any potential performance implications
",
                Version = "1.0"
            });
            
            // Report Generation
            RegisterTemplate(new PromptTemplate
            {
                Id = "report_generation",
                Name = "Report Generation",
                Description = "Generate a complete report configuration from a natural language request",
                TemplateText = @"You are a reporting expert. Generate a complete report configuration based on the following natural language request:

Natural Language Request: {request}

Database Schema:
{schema}

Create a comprehensive report configuration including:
1. The SQL query to retrieve the data
2. Appropriate visualization type (table, bar chart, line chart, etc.)
3. Column formatting rules (dates, numbers, currency, etc.)
4. Sorting and filtering recommendations
5. A clear title and description for the report
",
                Version = "1.0"
            });
        }
    }
    
    /// <summary>
    /// Represents a prompt template with versioning and usage tracking
    /// </summary>
    public class PromptTemplate
    {
        /// <summary>
        /// Unique identifier for the template
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
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// When the template was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the template was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// How many times this template has been used
        /// </summary>
        public int UsageCount { get; set; } = 0;
        
        /// <summary>
        /// When this template was last used
        /// </summary>
        public DateTime? LastUsed { get; set; }
        
        /// <summary>
        /// Log of recent responses (for analytics)
        /// </summary>
        public List<PromptResponse> ResponseLog { get; set; } = new List<PromptResponse>();
        
        /// <summary>
        /// Tags for organizing templates
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Tracks a response to a prompt
    /// </summary>
    public class PromptResponse
    {
        /// <summary>
        /// When the response was generated
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Length of the response in characters
        /// </summary>
        public int ResponseLength { get; set; }
        
        /// <summary>
        /// Time taken to generate the response in milliseconds
        /// </summary>
        public double ExecutionTimeMs { get; set; }
    }
}
