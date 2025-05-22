using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// JSON serialization context for source generation
    /// </summary>
    public static class McpJsonContext
    {
        /// <summary>
        /// Gets the default JSON serializer options
        /// </summary>
        public static JsonSerializerOptions DefaultOptions => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
