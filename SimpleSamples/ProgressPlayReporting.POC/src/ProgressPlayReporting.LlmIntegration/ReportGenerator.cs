using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// Implementation of the report generator using LLM to analyze data and generate reports
    /// </summary>
    public class ReportGenerator : IReportGenerator
    {
        private readonly ILlmGateway _llmGateway;
        private readonly ILogger<ReportGenerator> _logger;

        /// <summary>
        /// Creates a new instance of ReportGenerator
        /// </summary>
        /// <param name="llmGateway">The LLM gateway to use</param>
        /// <param name="logger">The logger to use</param>
        public ReportGenerator(ILlmGateway llmGateway, ILogger<ReportGenerator> logger)
        {
            _llmGateway = llmGateway ?? throw new ArgumentNullException(nameof(llmGateway));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ReportResult> GenerateReportAsync(string request, DataTable data, DatabaseSchema schema = null)
        {
            try
            {
                _logger.LogInformation("Generating report for request: {Request}", request);

                var prompt = BuildReportPrompt(request, data, schema);
                var response = await _llmGateway.GenerateCompletionAsync(prompt);

                return ParseReportResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<DataAnalysisResult> AnalyzeDataAsync(DataTable data, string analysisGoal = null)
        {
            try
            {
                _logger.LogInformation("Analyzing data with goal: {AnalysisGoal}", 
                    string.IsNullOrEmpty(analysisGoal) ? "General analysis" : analysisGoal);

                var prompt = BuildAnalysisPrompt(data, analysisGoal);
                var response = await _llmGateway.GenerateCompletionAsync(prompt);

                return ParseAnalysisResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing data");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VisualizationConfig> GenerateVisualizationConfigAsync(string request, DataTable data)
        {
            try
            {
                _logger.LogInformation("Generating visualization config for request: {Request}", request);

                var prompt = BuildVisualizationPrompt(request, data);
                var response = await _llmGateway.GenerateCompletionAsync(prompt);

                return ParseVisualizationResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating visualization config");
                throw;
            }
        }

        private string BuildReportPrompt(string request, DataTable data, DatabaseSchema schema)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert data analyst specializing in creating reports for the gaming industry.");
            sb.AppendLine("Generate a comprehensive report based on the provided data and request.");
            
            sb.AppendLine("\nRequest:");
            sb.AppendLine(request);
            
            sb.AppendLine("\nData Schema:");
            foreach (DataColumn column in data.Columns)
            {
                sb.AppendLine($"- {column.ColumnName}: {column.DataType.Name}");
            }
            
            sb.AppendLine("\nData Sample (first 5 rows):");
            int rowCount = Math.Min(data.Rows.Count, 5);
            for (int i = 0; i < rowCount; i++)
            {
                var rowData = new List<string>();
                foreach (DataColumn column in data.Columns)
                {
                    rowData.Add($"{column.ColumnName}: {data.Rows[i][column]}");
                }
                sb.AppendLine($"Row {i + 1}: {string.Join(", ", rowData)}");
            }
            
            if (schema != null)
            {
                sb.AppendLine("\nDatabase Context:");
                sb.AppendLine($"Database: {schema.DatabaseName}");
                sb.AppendLine("Relevant Tables:");
                foreach (var table in schema.Tables)
                {
                    sb.AppendLine($"- {table.TableName}");
                }
            }
            
            sb.AppendLine("\nGenerate a comprehensive report that includes:");
            sb.AppendLine("1. A title for the report");
            sb.AppendLine("2. An executive summary");
            sb.AppendLine("3. Key insights from the data");
            sb.AppendLine("4. Detailed analysis");
            sb.AppendLine("5. Recommended visualizations");
            sb.AppendLine("6. Actionable recommendations");
            
            sb.AppendLine("\nReturn your response in the following JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("  \"title\": \"Report title\",");
            sb.AppendLine("  \"summary\": \"Executive summary\",");
            sb.AppendLine("  \"content\": \"Detailed report content\",");
            sb.AppendLine("  \"keyInsights\": [\"Insight 1\", \"Insight 2\"],");
            sb.AppendLine("  \"visualizations\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"type\": \"chart type\",");
            sb.AppendLine("      \"title\": \"Visualization title\",");
            sb.AppendLine("      \"description\": \"What this visualization shows\",");
            sb.AppendLine("      \"dataFields\": [\"Field1\", \"Field2\"]");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"recommendations\": [\"Recommendation 1\", \"Recommendation 2\"]");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private string BuildAnalysisPrompt(DataTable data, string analysisGoal)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert data analyst specializing in analyzing gaming industry data.");
            sb.AppendLine("Perform a comprehensive analysis of the provided data.");
            
            if (!string.IsNullOrEmpty(analysisGoal))
            {
                sb.AppendLine("\nAnalysis Goal:");
                sb.AppendLine(analysisGoal);
            }
            
            sb.AppendLine("\nData Schema:");
            foreach (DataColumn column in data.Columns)
            {
                sb.AppendLine($"- {column.ColumnName}: {column.DataType.Name}");
            }
            
            sb.AppendLine("\nData Sample (first 5 rows):");
            int rowCount = Math.Min(data.Rows.Count, 5);
            for (int i = 0; i < rowCount; i++)
            {
                var rowData = new List<string>();
                foreach (DataColumn column in data.Columns)
                {
                    rowData.Add($"{column.ColumnName}: {data.Rows[i][column]}");
                }
                sb.AppendLine($"Row {i + 1}: {string.Join(", ", rowData)}");
            }
            
            sb.AppendLine("\nPerform the following analysis:");
            sb.AppendLine("1. Identify key insights from the data");
            sb.AppendLine("2. Calculate relevant statistics");
            sb.AppendLine("3. Identify any anomalies or outliers");
            sb.AppendLine("4. Identify trends in the data");
            sb.AppendLine("5. Identify correlations between variables");
            sb.AppendLine("6. Suggest potential data-driven actions");
            
            sb.AppendLine("\nReturn your response in the following JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("  \"insights\": [\"Insight 1\", \"Insight 2\"],");
            sb.AppendLine("  \"statistics\": {\"stat1\": \"value1\", \"stat2\": \"value2\"},");
            sb.AppendLine("  \"anomalies\": [\"Anomaly 1\", \"Anomaly 2\"],");
            sb.AppendLine("  \"trends\": [\"Trend 1\", \"Trend 2\"],");
            sb.AppendLine("  \"correlations\": [\"Correlation 1\", \"Correlation 2\"],");
            sb.AppendLine("  \"actions\": [\"Action 1\", \"Action 2\"]");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private string BuildVisualizationPrompt(string request, DataTable data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert data visualization specialist.");
            sb.AppendLine("Generate a visualization configuration based on the provided data and request.");
            
            sb.AppendLine("\nRequest:");
            sb.AppendLine(request);
            
            sb.AppendLine("\nData Schema:");
            foreach (DataColumn column in data.Columns)
            {
                sb.AppendLine($"- {column.ColumnName}: {column.DataType.Name}");
            }
            
            sb.AppendLine("\nData Sample (first 5 rows):");
            int rowCount = Math.Min(data.Rows.Count, 5);
            for (int i = 0; i < rowCount; i++)
            {
                var rowData = new List<string>();
                foreach (DataColumn column in data.Columns)
                {
                    rowData.Add($"{column.ColumnName}: {data.Rows[i][column]}");
                }
                sb.AppendLine($"Row {i + 1}: {string.Join(", ", rowData)}");
            }
            
            sb.AppendLine("\nRecommend an optimal visualization for this data and request.");
            sb.AppendLine("Consider:");
            sb.AppendLine("1. The most appropriate chart type (bar, line, pie, scatter, etc.)");
            sb.AppendLine("2. Which fields should be used for X-axis, Y-axis, colors, etc.");
            sb.AppendLine("3. Appropriate title and description");
            sb.AppendLine("4. Any filters or transformations needed");
            
            sb.AppendLine("\nReturn your response in the following JSON format:");
            sb.AppendLine("{");
            sb.AppendLine("  \"type\": \"chart type\",");
            sb.AppendLine("  \"title\": \"Visualization title\",");
            sb.AppendLine("  \"description\": \"What this visualization shows\",");
            sb.AppendLine("  \"xAxis\": \"Field for X-axis\",");
            sb.AppendLine("  \"yAxis\": \"Field for Y-axis\",");
            sb.AppendLine("  \"color\": \"Field for color differentiation (optional)\",");
            sb.AppendLine("  \"filters\": [\"Filter 1\", \"Filter 2\"],");
            sb.AppendLine("  \"transformations\": [\"Transformation 1\"]");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private ReportResult ParseReportResponse(string response)
        {
            try
            {
                // Extract JSON object from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var result = JsonSerializer.Deserialize<ReportResult>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    // Handle null collections
                    result.KeyInsights ??= new List<string>();
                    result.Visualizations ??= new List<VisualizationConfig>();
                    result.Recommendations ??= new List<string>();
                    
                    return result;
                }
                
                // Fallback: Basic parsing if JSON structure is not detected
                _logger.LogWarning("Response not in expected JSON format, attempting basic parsing");
                
                var fallbackResult = new ReportResult
                {
                    Content = response,
                    Title = ExtractTitle(response),
                    Summary = ExtractSummary(response)
                };
                
                return fallbackResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing report response");
                return new ReportResult
                {
                    Title = "Error Generating Report",
                    Content = "An error occurred while generating the report: " + ex.Message,
                    Summary = "Report generation failed"
                };
            }
        }

        private DataAnalysisResult ParseAnalysisResponse(string response)
        {
            try
            {
                // Extract JSON object from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var deserializedObj = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    var result = new DataAnalysisResult();
                    
                    // Parse insights
                    if (deserializedObj.TryGetProperty("insights", out var insightsElement) && 
                        insightsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var insight in insightsElement.EnumerateArray())
                        {
                            result.Insights.Add(insight.GetString());
                        }
                    }
                    
                    // Parse statistics
                    if (deserializedObj.TryGetProperty("statistics", out var statsElement) && 
                        statsElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var stat in statsElement.EnumerateObject())
                        {
                            result.Statistics[stat.Name] = stat.Value.ToString();
                        }
                    }
                    
                    // Parse anomalies
                    if (deserializedObj.TryGetProperty("anomalies", out var anomaliesElement) && 
                        anomaliesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var anomaly in anomaliesElement.EnumerateArray())
                        {
                            result.Anomalies.Add(anomaly.GetString());
                        }
                    }
                    
                    // Parse trends
                    if (deserializedObj.TryGetProperty("trends", out var trendsElement) && 
                        trendsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var trend in trendsElement.EnumerateArray())
                        {
                            result.Trends.Add(trend.GetString());
                        }
                    }
                    
                    // Parse correlations
                    if (deserializedObj.TryGetProperty("correlations", out var correlationsElement) && 
                        correlationsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var correlation in correlationsElement.EnumerateArray())
                        {
                            result.Correlations.Add(correlation.GetString());
                        }
                    }
                    
                    return result;
                }
                
                // Fallback: Basic parsing if JSON structure is not detected
                _logger.LogWarning("Response not in expected JSON format, attempting basic parsing");
                
                var fallbackResult = new DataAnalysisResult();
                fallbackResult.Insights.Add("Analysis could not be parsed in structured format");
                fallbackResult.Insights.Add("Please review the raw response for details");
                
                return fallbackResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing analysis response");
                var result = new DataAnalysisResult();
                result.Insights.Add("Error parsing analysis response: " + ex.Message);
                return result;
            }
        }

        private VisualizationConfig ParseVisualizationResponse(string response)
        {
            try
            {
                // Extract JSON object from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonSerializer.Deserialize<VisualizationConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                // Fallback: Basic parsing if JSON structure is not detected
                _logger.LogWarning("Response not in expected JSON format, attempting basic parsing");
                
                return new VisualizationConfig
                {
                    Type = "text",
                    Title = "Visualization Recommendation",
                    Description = "Could not parse structured visualization config. Raw response: " + response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing visualization response");
                return new VisualizationConfig
                {
                    Type = "error",
                    Title = "Visualization Error",
                    Description = "An error occurred while parsing visualization config: " + ex.Message
                };
            }
        }

        private string ExtractTitle(string text)
        {
            // Try to extract a title from text, looking for patterns like "# Title" or "Title:" at the beginning of a line
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("# "))
                {
                    return trimmed.Substring(2).Trim();
                }
                if (trimmed.StartsWith("Title:"))
                {
                    return trimmed.Substring(6).Trim();
                }
            }
            
            // If no title found, return the first non-empty line
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    return trimmed.Length > 50 ? trimmed.Substring(0, 47) + "..." : trimmed;
                }
            }
            
            return "Generated Report";
        }

        private string ExtractSummary(string text)
        {
            // Try to extract a summary from text, looking for patterns like "## Summary" or "Summary:" or "Executive Summary:"
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("## Summary") || 
                    trimmed.StartsWith("Summary:") || 
                    trimmed.StartsWith("Executive Summary"))
                {
                    // Find the next non-empty line
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        var nextLine = lines[j].Trim();
                        if (!string.IsNullOrEmpty(nextLine) && !nextLine.StartsWith("#"))
                        {
                            return nextLine;
                        }
                    }
                }
            }
            
            // If no summary found, return empty
            return "";
        }
    }
}
