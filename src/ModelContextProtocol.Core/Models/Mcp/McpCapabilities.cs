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
        /// Gets or sets the resource capabilities.
        /// </summary>
        public ResourceCapabilities Resources { get; set; }

        /// <summary>
        /// Gets or sets the tool capabilities.
        /// </summary>
        public ToolCapabilities Tools { get; set; }

        /// <summary>
        /// Gets or sets the prompt capabilities.
        /// </summary>
        public PromptCapabilities Prompts { get; set; }

        /// <summary>
        /// Gets or sets the logging capabilities.
        /// </summary>
        public LoggingCapabilities Logging { get; set; }
    }

    /// <summary>
    /// Represents resource capabilities
    /// </summary>
    public class ResourceCapabilities
    {
        /// <summary>
        /// Whether the server supports resource subscription
        /// </summary>
        public bool Subscribe { get; set; }

        /// <summary>
        /// Whether the server supports resource list change notifications
        /// </summary>
        public bool ListChanged { get; set; }
    }

    /// <summary>
    /// Represents tool capabilities
    /// </summary>
    public class ToolCapabilities
    {
        /// <summary>
        /// Whether the server supports tool list change notifications
        /// </summary>
        public bool ListChanged { get; set; }
    }

    /// <summary>
    /// Represents prompt capabilities
    /// </summary>
    public class PromptCapabilities
    {
        /// <summary>
        /// Whether the server supports prompt list change notifications
        /// </summary>
        public bool ListChanged { get; set; }
    }

    /// <summary>
    /// Represents logging capabilities
    /// </summary>
    public class LoggingCapabilities
    {
        /// <summary>
        /// Whether the server supports logging
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Supported log levels
        /// </summary>
        public List<string> SupportedLevels { get; set; } = new List<string>();
    }
}