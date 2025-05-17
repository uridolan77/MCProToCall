using System.Collections.Generic;

namespace ProgressPlayReporting.Api.Models
{
    /// <summary>
    /// Standardized error response for API endpoints
    /// </summary>
    public class ApiErrorResponse
    {
        /// <summary>
        /// Error code identifying the type of error
        /// </summary>
        public string ErrorCode { get; set; }
        
        /// <summary>
        /// User-friendly error message
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Additional details about the error
        /// </summary>
        public List<string> Details { get; set; } = new List<string>();
        
        /// <summary>
        /// Request ID for tracking purposes
        /// </summary>
        public string RequestId { get; set; }
        
        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public string Timestamp { get; set; } = System.DateTimeOffset.UtcNow.ToString("o");
    }
}
