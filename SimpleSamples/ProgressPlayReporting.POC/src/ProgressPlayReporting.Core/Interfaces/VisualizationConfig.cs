using System.Collections.Generic;

namespace ProgressPlayReporting.Core.Interfaces
{
    /// <summary>
    /// Configuration for data visualization
    /// </summary>
    public class VisualizationConfig
    {
        /// <summary>
        /// Type of visualization (bar, line, pie, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Title of the visualization
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Description of what the visualization shows
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Fields to use in the visualization
        /// </summary>
        public List<string> DataFields { get; set; } = new List<string>();
        
        /// <summary>
        /// Field to use for X-axis
        /// </summary>
        public string XAxis { get; set; }
        
        /// <summary>
        /// Field to use for Y-axis
        /// </summary>
        public string YAxis { get; set; }
        
        /// <summary>
        /// Field to use for color differentiation
        /// </summary>
        public string Color { get; set; }
        
        /// <summary>
        /// Filters to apply to the data
        /// </summary>
        public List<string> Filters { get; set; } = new List<string>();
        
        /// <summary>
        /// Transformations to apply to the data
        /// </summary>
        public List<string> Transformations { get; set; } = new List<string>();
    }
}
