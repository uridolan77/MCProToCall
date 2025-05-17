using System;  
using System.Collections.Generic;  

namespace ModelContextProtocol.Core.Models.Mcp  
{  
    /// <summary>  
    /// Represents a tool exposed by the MCP Server  
    /// </summary>  
    public class McpTool  
    {  
        /// <summary>  
        /// Unique identifier for the tool  
        /// </summary>  
        public string Id { get; set; }  
          
        /// <summary>  
        /// Display name for the tool  
        /// </summary>  
        public string Name { get; set; }  
          
        /// <summary>  
        /// Description of the tool's functionality  
        /// </summary>  
        public string Description { get; set; }  
          
        /// <summary>  
        /// Tool's input schema in JSON Schema format  
        /// </summary>  
        public object InputSchema { get; set; }  
          
        /// <summary>  
        /// Tool's output schema in JSON Schema format  
        /// </summary>  
        public object OutputSchema { get; set; }  
    }  
}