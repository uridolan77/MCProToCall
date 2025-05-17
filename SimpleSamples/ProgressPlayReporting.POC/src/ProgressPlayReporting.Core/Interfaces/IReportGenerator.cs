using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using ProgressPlayReporting.Core.Models.Visualization;

namespace ProgressPlayReporting.Core.Interfaces
{
    /// <summary>
    /// Interface for report generation capabilities using LLMs
    /// </summary>
    public interface IReportGenerator
    {
        /// <summary>
        /// Generates a report based on natural language request and data
        /// </summary>
        /// <param name="request">Natural language description of the report needed</param>
        /// <param name="data">Dataset containing the results to analyze</param>
        /// <param name="schema">Optional database schema for additional context</param>
        /// <returns>Generated report</returns>
        Task<ReportResult> GenerateReportAsync(string request, DataTable data, DatabaseSchema schema = null);
        
        /// <summary>
        /// Analyzes data and extracts key insights
        /// </summary>
        /// <param name="data">Dataset to analyze</param>
        /// <param name="analysisGoal">Optional description of what to look for</param>
        /// <returns>Analysis results with insights</returns>
        Task<DataAnalysisResult> AnalyzeDataAsync(DataTable data, string analysisGoal = null);
        
        /// <summary>
        /// Translates a natural language request into an optimal visualization configuration
        /// </summary>
        /// <param name="request">Natural language description of the visualization needed</param>
        /// <param name="data">Dataset to visualize</param>
        /// <returns>Visualization configuration</returns>
        Task<VisualizationConfig> GenerateVisualizationConfigAsync(string request, DataTable data);
    }

    /// <summary>
    /// Result of report generation
    /// </summary>
    public class ReportResult
    {
        /// <summary>
        /// Title of the report
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Main report content
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// Executive summary
        /// </summary>
        public string Summary { get; set; }
        
        /// <summary>
        /// Key insights extracted from the data
        /// </summary>
        public List<string> KeyInsights { get; set; } = new List<string>();
        
        /// <summary>
        /// Recommended visualizations
        /// </summary>
        public List<VisualizationConfig> Visualizations { get; set; } = new List<VisualizationConfig>();
        
        /// <summary>
        /// Suggested follow-up analyses or actions
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of data analysis
    /// </summary>
    public class DataAnalysisResult
    {
        /// <summary>
        /// Key insights extracted from the data
        /// </summary>
        public List<string> Insights { get; set; } = new List<string>();
        
        /// <summary>
        /// Statistical summaries
        /// </summary>
        public Dictionary<string, object> Statistics { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Anomalies or outliers detected
        /// </summary>
        public List<string> Anomalies { get; set; } = new List<string>();
        
        /// <summary>
        /// Trends identified in the data
        /// </summary>
        public List<string> Trends { get; set; } = new List<string>();
        
        /// <summary>
        /// Correlations between variables
        /// </summary>
        public List<CorrelationResult> Correlations { get; set; } = new List<CorrelationResult>();
    }    /// <summary>
    /// Correlation between two variables
    /// </summary>
    public class CorrelationResult
    {
        /// <summary>
        /// First variable in the correlation
        /// </summary>
        public string Variable1 { get; set; }
        
        /// <summary>
        /// Second variable in the correlation
        /// </summary>
        public string Variable2 { get; set; }
        
        /// <summary>
        /// Correlation coefficient
        /// </summary>
        public double Coefficient { get; set; }
        
        /// <summary>
        /// Description of the correlation
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Configuration for a chart axis
    /// </summary>
    public class AxisConfig
    {
        /// <summary>
        /// Title for the axis
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Data field to use for this axis
        /// </summary>
        public string Field { get; set; }
        
        /// <summary>
        /// Type of data (category, date, number)
        /// </summary>
        public string DataType { get; set; }
        
        /// <summary>
        /// Formatting options
        /// </summary>
        public string Format { get; set; }
    }

    /// <summary>
    /// Configuration for a data series
    /// </summary>
    public class SeriesConfig
    {
        /// <summary>
        /// Name of the series
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Data field to use
        /// </summary>
        public string Field { get; set; }
        
        /// <summary>
        /// Type of series (line, bar, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Color for the series
        /// </summary>
        public string Color { get; set; }
    }
}
