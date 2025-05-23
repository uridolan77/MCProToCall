using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.Mcp;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Extensions.Documentation
{
    /// <summary>
    /// Generates OpenAPI/Swagger documentation for MCP servers
    /// </summary>
    public class McpOpenApiGenerator
    {
        private readonly ILogger<McpOpenApiGenerator> _logger;
        private readonly McpOpenApiOptions _options;

        public McpOpenApiGenerator(ILogger<McpOpenApiGenerator> logger = null, McpOpenApiOptions options = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<McpOpenApiGenerator>.Instance;
            _options = options ?? new McpOpenApiOptions();
        }

        /// <summary>
        /// Generates OpenAPI specification for an MCP server
        /// </summary>
        /// <param name="server">MCP server to document</param>
        /// <param name="serverInfo">Server information</param>
        /// <returns>OpenAPI specification as JSON</returns>
        public async Task<string> GenerateOpenApiSpecAsync(IMcpServer server, McpServerInfo serverInfo = null)
        {
            _logger.LogInformation("Generating OpenAPI specification for MCP server");

            serverInfo ??= new McpServerInfo();
            var spec = CreateBaseOpenApiSpec(serverInfo);

            // Add MCP-specific paths and schemas
            AddMcpPaths(spec, server);
            AddMcpSchemas(spec);
            AddSecuritySchemes(spec);

            var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("OpenAPI specification generated successfully");
            return json;
        }

        /// <summary>
        /// Generates and saves OpenAPI specification to a file
        /// </summary>
        /// <param name="server">MCP server to document</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="serverInfo">Server information</param>
        public async Task GenerateAndSaveAsync(IMcpServer server, string filePath, McpServerInfo serverInfo = null)
        {
            var spec = await GenerateOpenApiSpecAsync(server, serverInfo);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, spec);
            _logger.LogInformation("OpenAPI specification saved to: {FilePath}", filePath);
        }

        /// <summary>
        /// Generates markdown documentation from OpenAPI spec
        /// </summary>
        /// <param name="server">MCP server to document</param>
        /// <param name="serverInfo">Server information</param>
        /// <returns>Markdown documentation</returns>
        public async Task<string> GenerateMarkdownDocumentationAsync(IMcpServer server, McpServerInfo serverInfo = null)
        {
            _logger.LogInformation("Generating markdown documentation for MCP server");

            serverInfo ??= new McpServerInfo();
            var markdown = new List<string>();

            // Header
            markdown.Add($"# {serverInfo.Title} API Documentation");
            markdown.Add("");
            markdown.Add($"**Version:** {serverInfo.Version}");
            if (!string.IsNullOrEmpty(serverInfo.Description))
            {
                markdown.Add("");
                markdown.Add($"**Description:** {serverInfo.Description}");
            }
            markdown.Add("");

            // Server information
            markdown.Add("## Server Information");
            markdown.Add("");
            markdown.Add("| Property | Value |");
            markdown.Add("|----------|-------|");
            markdown.Add($"| Base URL | {serverInfo.BaseUrl ?? "http://localhost:8080"} |");
            markdown.Add($"| Protocol | Model Context Protocol (MCP) |");
            markdown.Add($"| Transport | HTTP/WebSocket |");
            markdown.Add("");

            // Capabilities - Create default capabilities since McpServer doesn't expose GetCapabilities directly
            var capabilities = new McpCapabilities
            {
                Version = "1.0.0",
                Resources = new ResourceCapabilities { Subscribe = true, ListChanged = true },
                Tools = new ToolCapabilities { ListChanged = true },
                Prompts = new PromptCapabilities { ListChanged = true },
                Logging = new LoggingCapabilities { Enabled = true, SupportedLevels = new List<string> { "debug", "info", "warn", "error" } }
            };

            markdown.Add("## Server Capabilities");
            markdown.Add("");
            AddCapabilitiesMarkdown(markdown, capabilities);
            markdown.Add("");

            // Methods
            markdown.Add("## Available Methods");
            markdown.Add("");
            AddMethodsMarkdown(markdown, server);

            // Authentication
            markdown.Add("## Authentication");
            markdown.Add("");
            markdown.Add("This MCP server supports the following authentication methods:");
            markdown.Add("");
            markdown.Add("- **None**: No authentication required for development/testing");
            markdown.Add("- **Bearer Token**: JWT-based authentication (if enabled)");
            markdown.Add("- **Client Certificates**: Mutual TLS authentication (if enabled)");
            markdown.Add("");

            // Examples
            markdown.Add("## Request Examples");
            markdown.Add("");
            AddExamplesMarkdown(markdown, server);

            var result = string.Join(Environment.NewLine, markdown);
            _logger.LogInformation("Markdown documentation generated successfully");
            return result;
        }

        private Dictionary<string, object> CreateBaseOpenApiSpec(McpServerInfo serverInfo)
        {
            return new Dictionary<string, object>
            {
                ["openapi"] = "3.0.3",
                ["info"] = new Dictionary<string, object>
                {
                    ["title"] = serverInfo.Title,
                    ["description"] = serverInfo.Description ?? "Model Context Protocol (MCP) Server API",
                    ["version"] = serverInfo.Version,
                    ["contact"] = serverInfo.Contact != null ? new Dictionary<string, object>
                    {
                        ["name"] = serverInfo.Contact.Name,
                        ["email"] = serverInfo.Contact.Email,
                        ["url"] = serverInfo.Contact.Url
                    } : null,
                    ["license"] = serverInfo.License != null ? new Dictionary<string, object>
                    {
                        ["name"] = serverInfo.License.Name,
                        ["url"] = serverInfo.License.Url
                    } : null
                },
                ["servers"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["url"] = serverInfo.BaseUrl ?? "http://localhost:8080",
                        ["description"] = "MCP Server"
                    }
                },
                ["paths"] = new Dictionary<string, object>(),
                ["components"] = new Dictionary<string, object>
                {
                    ["schemas"] = new Dictionary<string, object>(),
                    ["securitySchemes"] = new Dictionary<string, object>()
                }
            };
        }

        private void AddMcpPaths(Dictionary<string, object> spec, IMcpServer server)
        {
            var paths = (Dictionary<string, object>)spec["paths"];

            // Main MCP endpoint
            paths["/mcp"] = new Dictionary<string, object>
            {
                ["post"] = new Dictionary<string, object>
                {
                    ["summary"] = "Execute MCP JSON-RPC request",
                    ["description"] = "Send a JSON-RPC request to the MCP server",
                    ["requestBody"] = new Dictionary<string, object>
                    {
                        ["required"] = true,
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["$ref"] = "#/components/schemas/JsonRpcRequest"
                                }
                            }
                        }
                    },
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Successful response",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object>
                                    {
                                        ["oneOf"] = new[]
                                        {
                                            new Dictionary<string, object> { ["$ref"] = "#/components/schemas/JsonRpcSuccessResponse" },
                                            new Dictionary<string, object> { ["$ref"] = "#/components/schemas/JsonRpcErrorResponse" }
                                        }
                                    }
                                }
                            }
                        },
                        ["400"] = new Dictionary<string, object>
                        {
                            ["description"] = "Bad request - Invalid JSON-RPC format"
                        },
                        ["500"] = new Dictionary<string, object>
                        {
                            ["description"] = "Internal server error"
                        }
                    },
                    ["tags"] = new[] { "MCP" }
                }
            };

            // WebSocket endpoint
            paths["/ws"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "WebSocket MCP connection",
                    ["description"] = "Establish a WebSocket connection for real-time MCP communication",
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["101"] = new Dictionary<string, object>
                        {
                            ["description"] = "Switching protocols to WebSocket"
                        },
                        ["400"] = new Dictionary<string, object>
                        {
                            ["description"] = "Bad request - Invalid WebSocket upgrade"
                        }
                    },
                    ["tags"] = new[] { "WebSocket" }
                }
            };

            // Health check endpoint
            paths["/health"] = new Dictionary<string, object>
            {
                ["get"] = new Dictionary<string, object>
                {
                    ["summary"] = "Health check",
                    ["description"] = "Check server health status",
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Server is healthy",
                            ["content"] = new Dictionary<string, object>
                            {
                                ["application/json"] = new Dictionary<string, object>
                                {
                                    ["schema"] = new Dictionary<string, object>
                                    {
                                        ["$ref"] = "#/components/schemas/HealthResponse"
                                    }
                                }
                            }
                        }
                    },
                    ["tags"] = new[] { "System" }
                }
            };

            // Metrics endpoint
            if (_options.IncludeMetricsEndpoint)
            {
                paths["/metrics"] = new Dictionary<string, object>
                {
                    ["get"] = new Dictionary<string, object>
                    {
                        ["summary"] = "Prometheus metrics",
                        ["description"] = "Get server metrics in Prometheus format",
                        ["responses"] = new Dictionary<string, object>
                        {
                            ["200"] = new Dictionary<string, object>
                            {
                                ["description"] = "Metrics data",
                                ["content"] = new Dictionary<string, object>
                                {
                                    ["text/plain"] = new Dictionary<string, object>
                                    {
                                        ["schema"] = new Dictionary<string, object>
                                        {
                                            ["type"] = "string"
                                        }
                                    }
                                }
                            }
                        },
                        ["tags"] = new[] { "Monitoring" }
                    }
                };
            }
        }

        private void AddMcpSchemas(Dictionary<string, object> spec)
        {
            var schemas = (Dictionary<string, object>)((Dictionary<string, object>)spec["components"])["schemas"];

            // JSON-RPC schemas
            schemas["JsonRpcRequest"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "jsonrpc", "method", "id" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["const"] = "2.0"
                    },
                    ["method"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The method name to call"
                    },
                    ["params"] = new Dictionary<string, object>
                    {
                        ["description"] = "Method parameters"
                    },
                    ["id"] = new Dictionary<string, object>
                    {
                        ["oneOf"] = new[]
                        {
                            new Dictionary<string, object> { ["type"] = "string" },
                            new Dictionary<string, object> { ["type"] = "number" }
                        },
                        ["description"] = "Request identifier"
                    }
                }
            };

            schemas["JsonRpcSuccessResponse"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "jsonrpc", "result", "id" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["const"] = "2.0"
                    },
                    ["result"] = new Dictionary<string, object>
                    {
                        ["description"] = "Method result"
                    },
                    ["id"] = new Dictionary<string, object>
                    {
                        ["oneOf"] = new[]
                        {
                            new Dictionary<string, object> { ["type"] = "string" },
                            new Dictionary<string, object> { ["type"] = "number" }
                        }
                    }
                }
            };

            schemas["JsonRpcErrorResponse"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "jsonrpc", "error", "id" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["const"] = "2.0"
                    },
                    ["error"] = new Dictionary<string, object>
                    {
                        ["$ref"] = "#/components/schemas/JsonRpcError"
                    },
                    ["id"] = new Dictionary<string, object>
                    {
                        ["oneOf"] = new[]
                        {
                            new Dictionary<string, object> { ["type"] = "string" },
                            new Dictionary<string, object> { ["type"] = "number" },
                            new Dictionary<string, object> { ["type"] = "null" }
                        }
                    }
                }
            };

            schemas["JsonRpcError"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "code", "message" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["code"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Error code"
                    },
                    ["message"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Error message"
                    },
                    ["data"] = new Dictionary<string, object>
                    {
                        ["description"] = "Additional error data"
                    }
                }
            };

            // MCP-specific schemas
            schemas["McpCapabilities"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["resources"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ResourceCapabilities" },
                    ["tools"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ToolCapabilities" },
                    ["prompts"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/PromptCapabilities" },
                    ["logging"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/LoggingCapabilities" }
                }
            };

            schemas["ResourceCapabilities"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["subscribe"] = new Dictionary<string, object> { ["type"] = "boolean" },
                    ["listChanged"] = new Dictionary<string, object> { ["type"] = "boolean" }
                }
            };

            schemas["ToolCapabilities"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["listChanged"] = new Dictionary<string, object> { ["type"] = "boolean" }
                }
            };

            schemas["PromptCapabilities"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["listChanged"] = new Dictionary<string, object> { ["type"] = "boolean" }
                }
            };

            schemas["LoggingCapabilities"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            };

            schemas["HealthResponse"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["status"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["enum"] = new[] { "healthy", "unhealthy", "degraded" }
                    },
                    ["timestamp"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["format"] = "date-time"
                    },
                    ["version"] = new Dictionary<string, object>
                    {
                        ["type"] = "string"
                    }
                }
            };
        }

        private void AddSecuritySchemes(Dictionary<string, object> spec)
        {
            var securitySchemes = (Dictionary<string, object>)((Dictionary<string, object>)spec["components"])["securitySchemes"];

            securitySchemes["BearerAuth"] = new Dictionary<string, object>
            {
                ["type"] = "http",
                ["scheme"] = "bearer",
                ["bearerFormat"] = "JWT",
                ["description"] = "JWT Bearer token authentication"
            };

            securitySchemes["ClientCertificate"] = new Dictionary<string, object>
            {
                ["type"] = "mutualTLS",
                ["description"] = "Client certificate authentication"
            };
        }

        private void AddCapabilitiesMarkdown(List<string> markdown, McpCapabilities capabilities)
        {
            markdown.Add("| Capability | Supported |");
            markdown.Add("|------------|-----------|");

            if (capabilities.Resources != null)
            {
                markdown.Add($"| Resources - Subscribe | {(capabilities.Resources.Subscribe ? "✅" : "❌")} |");
                markdown.Add($"| Resources - List Changed | {(capabilities.Resources.ListChanged ? "✅" : "❌")} |");
            }

            if (capabilities.Tools != null)
            {
                markdown.Add($"| Tools - List Changed | {(capabilities.Tools.ListChanged ? "✅" : "❌")} |");
            }

            if (capabilities.Prompts != null)
            {
                markdown.Add($"| Prompts - List Changed | {(capabilities.Prompts.ListChanged ? "✅" : "❌")} |");
            }

            markdown.Add($"| Logging | {(capabilities.Logging != null ? "✅" : "❌")} |");
        }

        private void AddMethodsMarkdown(List<string> markdown, IMcpServer server)
        {
            markdown.Add("### Standard MCP Methods");
            markdown.Add("");
            markdown.Add("| Method | Description |");
            markdown.Add("|--------|-------------|");
            markdown.Add("| `system.getCapabilities` | Get server capabilities |");
            markdown.Add("| `system.ping` | Ping the server |");
            markdown.Add("| `system.info` | Get server information |");
            markdown.Add("| `resources/list` | List available resources |");
            markdown.Add("| `resources/read` | Read a specific resource |");
            markdown.Add("| `tools/list` | List available tools |");
            markdown.Add("| `tools/call` | Execute a tool |");
            markdown.Add("| `prompts/list` | List available prompts |");
            markdown.Add("| `prompts/get` | Get a specific prompt |");
            markdown.Add("");

            // Note: Custom methods would be listed here if the McpServer exposed its registered methods
            // Since the current McpServer implementation doesn't expose the methods dictionary,
            // we only show the standard MCP methods above
        }

        private void AddExamplesMarkdown(List<string> markdown, IMcpServer server)
        {
            markdown.Add("### Get Server Capabilities");
            markdown.Add("");
            markdown.Add("```json");
            markdown.Add("{");
            markdown.Add("  \"jsonrpc\": \"2.0\",");
            markdown.Add("  \"method\": \"system.getCapabilities\",");
            markdown.Add("  \"params\": {},");
            markdown.Add("  \"id\": \"1\"");
            markdown.Add("}");
            markdown.Add("```");
            markdown.Add("");

            markdown.Add("### Ping Server");
            markdown.Add("");
            markdown.Add("```json");
            markdown.Add("{");
            markdown.Add("  \"jsonrpc\": \"2.0\",");
            markdown.Add("  \"method\": \"system.ping\",");
            markdown.Add("  \"params\": {},");
            markdown.Add("  \"id\": \"2\"");
            markdown.Add("}");
            markdown.Add("```");
            markdown.Add("");

            markdown.Add("### List Resources");
            markdown.Add("");
            markdown.Add("```json");
            markdown.Add("{");
            markdown.Add("  \"jsonrpc\": \"2.0\",");
            markdown.Add("  \"method\": \"resources/list\",");
            markdown.Add("  \"params\": {},");
            markdown.Add("  \"id\": \"3\"");
            markdown.Add("}");
            markdown.Add("```");
            markdown.Add("");
        }
    }

    /// <summary>
    /// Configuration options for OpenAPI generation
    /// </summary>
    public class McpOpenApiOptions
    {
        /// <summary>
        /// Whether to include metrics endpoint in documentation
        /// </summary>
        public bool IncludeMetricsEndpoint { get; set; } = true;

        /// <summary>
        /// Whether to include WebSocket documentation
        /// </summary>
        public bool IncludeWebSocketEndpoints { get; set; } = true;

        /// <summary>
        /// Whether to include authentication documentation
        /// </summary>
        public bool IncludeAuthentication { get; set; } = true;

        /// <summary>
        /// Custom tags to add to the specification
        /// </summary>
        public List<string> CustomTags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Server information for OpenAPI specification
    /// </summary>
    public class McpServerInfo
    {
        /// <summary>
        /// API title
        /// </summary>
        public string Title { get; set; } = "MCP Server API";

        /// <summary>
        /// API description
        /// </summary>
        public string Description { get; set; } = "Model Context Protocol Server API";

        /// <summary>
        /// API version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Base URL for the server
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Contact information
        /// </summary>
        public ContactInfo Contact { get; set; }

        /// <summary>
        /// License information
        /// </summary>
        public LicenseInfo License { get; set; }
    }

    /// <summary>
    /// Contact information for OpenAPI specification
    /// </summary>
    public class ContactInfo
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// License information for OpenAPI specification
    /// </summary>
    public class LicenseInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
