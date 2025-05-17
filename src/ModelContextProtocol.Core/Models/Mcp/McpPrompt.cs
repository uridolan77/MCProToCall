using System;
using System.Collections.Generic;

/// <summary>
/// Represents a prompt template exposed by the MCP Server
/// </summary>
public class McpPrompt
{
    /// <summary>
    /// Unique identifier for the prompt
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Display name for the prompt
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the prompt's purpose
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Prompt template with placeholder variables
    /// </summary>
    public string Template { get; set; }

    /// <summary>
    /// Schema for prompt variables in JSON Schema format
    /// </summary>
    public object VariablesSchema { get; set; }
}