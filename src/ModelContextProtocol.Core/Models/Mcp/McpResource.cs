using System.Collections.Generic;

/// <summary>
/// Represents a resource exposed by the MCP Server
/// </summary>
public class McpResource
{
    /// <summary>
    /// Unique identifier for the resource
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Display name for the resource
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Type of the resource (e.g., document, image)
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Additional metadata for the resource
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}