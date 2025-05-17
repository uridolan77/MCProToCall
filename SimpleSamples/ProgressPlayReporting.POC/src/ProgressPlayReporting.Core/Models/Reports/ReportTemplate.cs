using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgressPlayReporting.Core.Models.Reports
{
    /// <summary>
    /// Represents a report template for generating reports from SQL data
    /// </summary>
    public class ReportTemplate
    {
        /// <summary>
        /// Unique identifier for the template
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Name of the report template
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Description of what the report shows
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// SQL query used to generate the report data
        /// </summary>
        public string SqlQuery { get; set; }
        
        /// <summary>
        /// Natural language prompt that was used to generate this report
        /// </summary>
        public string OriginalPrompt { get; set; }
        
        /// <summary>
        /// Visual configuration for rendering the report
        /// </summary>
        public ReportVisualSettings VisualSettings { get; set; } = new ReportVisualSettings();
        
        /// <summary>
        /// Export format options for this report
        /// </summary>
        public List<string> SupportedExportFormats { get; set; } = new List<string> { "Excel", "CSV", "PDF" };
        
        /// <summary>
        /// Timestamp when this template was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Timestamp when this template was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether this is a system template or a user template
        /// </summary>
        public bool IsSystemTemplate { get; set; } = false;
        
        /// <summary>
        /// User who created this template
        /// </summary>
        public string CreatedBy { get; set; }
        
        /// <summary>
        /// Tags/categories for organizing templates
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
        
        /// <summary>
        /// Parameters that can be used to customize the report
        /// </summary>
        public List<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
    }

    /// <summary>
    /// Parameter for customizing a report
    /// </summary>
    public class ReportParameter
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Data type of the parameter
        /// </summary>
        public ParameterType Type { get; set; }
        
        /// <summary>
        /// User-friendly display name
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// Optional description of the parameter
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Default value for the parameter
        /// </summary>
        public object DefaultValue { get; set; }
        
        /// <summary>
        /// For enum/list types, the available options
        /// </summary>
        public List<ParameterOption> Options { get; set; } = new List<ParameterOption>();
        
        /// <summary>
        /// Whether this parameter is required
        /// </summary>
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>
    /// Option for a parameter with predefined choices
    /// </summary>
    public class ParameterOption
    {
        /// <summary>
        /// Value to use in the query
        /// </summary>
        public string Value { get; set; }
        
        /// <summary>
        /// User-friendly display label
        /// </summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// Parameter data types
    /// </summary>
    public enum ParameterType
    {
        String,
        Number,
        Date,
        Boolean,
        DateTime,
        Enum,
        Array
    }

    /// <summary>
    /// Visual settings for rendering a report
    /// </summary>
    public class ReportVisualSettings
    {
        /// <summary>
        /// Type of visualization to use
        /// </summary>
        public VisualizationType VisualizationType { get; set; } = VisualizationType.Table;
        
        /// <summary>
        /// For charts, which columns to use for x/y axes
        /// </summary>
        public Dictionary<string, string> AxisMappings { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Color scheme for the report
        /// </summary>
        public string ColorScheme { get; set; } = "default";
        
        /// <summary>
        /// Title to display on the report
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Optional subtitle for the report
        /// </summary>
        public string Subtitle { get; set; }
        
        /// <summary>
        /// Column formatting rules
        /// </summary>
        public Dictionary<string, ColumnFormat> ColumnFormats { get; set; } = new Dictionary<string, ColumnFormat>();
    }

    /// <summary>
    /// Format settings for a specific column
    /// </summary>
    public class ColumnFormat
    {
        /// <summary>
        /// Display name for the column
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// Format string (e.g., "C" for currency, "P" for percentage)
        /// </summary>
        public string Format { get; set; }
        
        /// <summary>
        /// Whether to hide this column in the output
        /// </summary>
        public bool Hidden { get; set; } = false;
        
        /// <summary>
        /// Sort direction for this column, if it's the sort column
        /// </summary>
        public SortDirection? SortDirection { get; set; }
        
        /// <summary>
        /// Priority order for sorting (1 = primary, 2 = secondary, etc.)
        /// </summary>
        public int? SortPriority { get; set; }
    }

    /// <summary>
    /// Sort direction for report columns
    /// </summary>
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// Types of visualizations available for reports
    /// </summary>
    public enum VisualizationType
    {
        Table,
        BarChart,
        LineChart,
        PieChart,
        ScatterPlot,
        AreaChart,
        Gauge,
        KPI,
        Map
    }
}
