using System.Collections.Generic;

namespace ModelContextProtocol.Core.Models.Mcp
{
    /// <summary>
    /// Represents the capabilities of the MCP server.
    /// </summary>
    public class McpCapabilities
    {
        /// <summary>
        /// Gets or sets the version of the MCP server.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the list of resources exposed by the MCP server.
        /// </summary>
        public List<McpResource> Resources { get; set; }

        /// <summary>
        /// Gets or sets the list of tools exposed by the MCP server.
        /// </summary>
        public List<McpTool> Tools { get; set; }

        /// <summary>
        /// Gets or sets the list of prompts exposed by the MCP server.
        /// </summary>
        public List<McpPrompt> Prompts { get; set; }
    }
}