using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ProgressPlayReporting.Core.Interfaces;
using ProgressPlayReporting.Core.Models.Reports;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.Api.Services.Export
{
    /// <summary>
    /// Exports report data to CSV format
    /// </summary>
    public class CsvExporter : IReportExporter
    {
        private readonly ILogger<CsvExporter> _logger;
        
        /// <summary>
        /// Creates a new CSV exporter
        /// </summary>
        public CsvExporter(ILogger<CsvExporter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public string Format => "CSV";
        
        /// <inheritdoc />
        public Task<byte[]> ExportAsync(DataTable data, ReportTemplate template, string filename)
        {
            _logger.LogInformation("Exporting {RowCount} rows to CSV format", data.Rows.Count);
            
            try
            {
                using (var memoryStream = new MemoryStream())
                using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8))
                {
                    // Create a StringBuilder for efficiency
                    var csvBuilder = new StringBuilder();
                    
                    // Add column headers, applying display names from template if available
                    for (int i = 0; i < data.Columns.Count; i++)
                    {
                        var columnName = data.Columns[i].ColumnName;
                        
                        // Skip hidden columns
                        if (template?.VisualSettings?.ColumnFormats != null &&
                            template.VisualSettings.ColumnFormats.TryGetValue(columnName, out var format) &&
                            format.Hidden)
                        {
                            continue;
                        }
                        
                        // Apply display name from template if available
                        string displayName = columnName;
                        if (template?.VisualSettings?.ColumnFormats != null &&
                            template.VisualSettings.ColumnFormats.TryGetValue(columnName, out var columnFormat) &&
                            !string.IsNullOrEmpty(columnFormat.DisplayName))
                        {
                            displayName = columnFormat.DisplayName;
                        }
                        
                        // Escape the header if needed and add to CSV
                        csvBuilder.Append(i > 0 ? "," : "");
                        csvBuilder.Append(EscapeCsvField(displayName));
                    }
                    
                    // Write the header row
                    streamWriter.WriteLine(csvBuilder.ToString());
                    
                    // Add data rows
                    foreach (DataRow row in data.Rows)
                    {
                        csvBuilder.Clear();
                        
                        for (int i = 0, visibleIndex = 0; i < data.Columns.Count; i++)
                        {
                            var columnName = data.Columns[i].ColumnName;
                            
                            // Skip hidden columns
                            if (template?.VisualSettings?.ColumnFormats != null &&
                                template.VisualSettings.ColumnFormats.TryGetValue(columnName, out var format) &&
                                format.Hidden)
                            {
                                continue;
                            }
                            
                            // Add separator if not first visible column
                            if (visibleIndex > 0)
                            {
                                csvBuilder.Append(",");
                            }
                            visibleIndex++;
                            
                            // Get the field value and apply formatting if specified
                            var value = row[i];
                            string formattedValue = FormatFieldValue(value, columnName, template);
                            
                            // Escape and add to CSV
                            csvBuilder.Append(EscapeCsvField(formattedValue));
                        }
                        
                        // Write the data row
                        streamWriter.WriteLine(csvBuilder.ToString());
                    }
                    
                    // Ensure all data is written to the memory stream
                    streamWriter.Flush();
                    
                    // Return the CSV as a byte array
                    return Task.FromResult(memoryStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to CSV format");
                throw;
            }
        }
        
        /// <inheritdoc />
        public string GetContentType()
        {
            return "text/csv";
        }
        
        /// <inheritdoc />
        public string GetFileExtension()
        {
            return ".csv";
        }
        
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }
            
            bool requiresQuoting = field.Contains(",") || field.Contains("\"") || field.Contains("\r") || field.Contains("\n");
            
            if (requiresQuoting)
            {
                // Double up any quotes and wrap in quotes
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            
            return field;
        }
        
        private string FormatFieldValue(object value, string columnName, ReportTemplate template)
        {
            if (value == null || value == DBNull.Value)
            {
                return "";
            }
            
            // Check if there's a format specified for this column
            string formatString = null;
            if (template?.VisualSettings?.ColumnFormats != null &&
                template.VisualSettings.ColumnFormats.TryGetValue(columnName, out var columnFormat) &&
                !string.IsNullOrEmpty(columnFormat.Format))
            {
                formatString = columnFormat.Format;
            }
            
            // Apply formatting based on the value type
            if (!string.IsNullOrEmpty(formatString))
            {
                if (value is IFormattable formattable)
                {
                    return formattable.ToString(formatString, null);
                }
            }
            
            // Default toString if no format or not formattable
            return value.ToString();
        }
    }
}
