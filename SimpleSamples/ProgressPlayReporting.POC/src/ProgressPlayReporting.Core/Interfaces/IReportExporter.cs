using System.Data;
using System.Threading.Tasks;
using ProgressPlayReporting.Core.Models.Reports;

namespace ProgressPlayReporting.Core.Interfaces
{
    /// <summary>
    /// Interface for exporting report data to different formats
    /// </summary>
    public interface IReportExporter
    {
        /// <summary>
        /// Supported export format
        /// </summary>
        string Format { get; }
        
        /// <summary>
        /// Export report data to the supported format
        /// </summary>
        /// <param name="data">Data to export</param>
        /// <param name="template">Report template with formatting options</param>
        /// <param name="filename">Output filename (without extension)</param>
        /// <returns>Byte array containing the exported file data</returns>
        Task<byte[]> ExportAsync(DataTable data, ReportTemplate template, string filename);
        
        /// <summary>
        /// Get the content type for this export format
        /// </summary>
        /// <returns>MIME content type</returns>
        string GetContentType();
        
        /// <summary>
        /// Get the file extension for this export format
        /// </summary>
        /// <returns>File extension with dot (e.g., ".xlsx")</returns>
        string GetFileExtension();
    }
}
