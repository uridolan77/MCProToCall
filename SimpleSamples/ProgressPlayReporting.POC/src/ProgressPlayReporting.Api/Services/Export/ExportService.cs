using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;
using ProgressPlayReporting.Core.Models.Reports;

namespace ProgressPlayReporting.Api.Services.Export
{
    /// <summary>
    /// Service for exporting reports to different formats
    /// </summary>
    public class ExportService
    {
        private readonly ILogger<ExportService> _logger;
        private readonly IEnumerable<IReportExporter> _exporters;
        
        /// <summary>
        /// Creates a new export service
        /// </summary>
        public ExportService(
            IEnumerable<IReportExporter> exporters,
            ILogger<ExportService> logger)
        {
            _exporters = exporters ?? throw new ArgumentNullException(nameof(exporters));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Export report data to the specified format
        /// </summary>
        /// <param name="data">Data to export</param>
        /// <param name="format">Export format (e.g., "Excel", "CSV", "PDF")</param>
        /// <param name="template">Report template with formatting options</param>
        /// <param name="filename">Output filename (without extension)</param>
        /// <returns>Exported file content and metadata</returns>
        public async Task<ExportResult> ExportAsync(DataTable data, string format, ReportTemplate template, string filename)
        {
            _logger.LogInformation("Exporting report to {Format} format", format);
            
            // Find the requested exporter
            var exporter = _exporters.FirstOrDefault(e => 
                e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
                
            if (exporter == null)
            {
                _logger.LogError("No exporter found for format: {Format}", format);
                throw new ArgumentException($"Unsupported export format: {format}");
            }
            
            try
            {
                // Export the data
                var fileContent = await exporter.ExportAsync(data, template, filename);
                
                // Create the result with metadata
                return new ExportResult
                {
                    FileContent = fileContent,
                    Filename = $"{filename}{exporter.GetFileExtension()}",
                    ContentType = exporter.GetContentType(),
                    Format = format
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to {Format} format", format);
                throw;
            }
        }
        
        /// <summary>
        /// Get a list of all supported export formats
        /// </summary>
        public IEnumerable<string> GetSupportedFormats()
        {
            return _exporters.Select(e => e.Format);
        }
    }
    
    /// <summary>
    /// Result of export operation containing file content and metadata
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Content of the exported file
        /// </summary>
        public byte[] FileContent { get; set; }
        
        /// <summary>
        /// Filename with extension
        /// </summary>
        public string Filename { get; set; }
        
        /// <summary>
        /// MIME content type
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// Export format used
        /// </summary>
        public string Format { get; set; }
    }
}
