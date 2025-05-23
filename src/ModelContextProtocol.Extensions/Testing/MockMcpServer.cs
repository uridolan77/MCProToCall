using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Mock MCP server for testing client implementations
    /// </summary>
    public class MockMcpServer : IMcpServer
    {
        private readonly ILogger<MockMcpServer> _logger;
        private readonly Dictionary<string, Func<JsonElement, Task<object>>> _methodHandlers;
        private readonly Dictionary<string, object> _resources;
        private readonly Dictionary<string, object> _tools;
        private readonly Dictionary<string, object> _prompts;
        private readonly List<JsonRpcRequest> _receivedRequests;
        private bool _isListening;

        public MockMcpServer(ILogger<MockMcpServer> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MockMcpServer>.Instance;
            _methodHandlers = new Dictionary<string, Func<JsonElement, Task<object>>>();
            _resources = new Dictionary<string, object>();
            _tools = new Dictionary<string, object>();
            _prompts = new Dictionary<string, object>();
            _receivedRequests = new List<JsonRpcRequest>();
            _isListening = false;

            SetupDefaultMethods();
        }

        /// <summary>
        /// Indicates if the mock server is listening
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// Available methods on the server
        /// </summary>
        public Dictionary<string, Func<JsonElement, Task<object>>> Methods => _methodHandlers;

        /// <summary>
        /// All requests received by the server
        /// </summary>
        public IReadOnlyList<JsonRpcRequest> ReceivedRequests => _receivedRequests.AsReadOnly();

        /// <summary>
        /// Call count for each method
        /// </summary>
        public Dictionary<string, int> CallCounts { get; } = new Dictionary<string, int>();        /// <summary>
        /// Registers a method handler
        /// </summary>
        public void RegisterMethod(string method, Func<JsonElement, Task<object>> handler)
        {
            _logger.LogDebug("Registering mock method: {Method}", method);
            _methodHandlers[method] = handler;
        }

        /// <summary>
        /// Registers a streaming method handler
        /// </summary>
        public void RegisterStreamingMethod(string methodName, Func<JsonElement, CancellationToken, IAsyncEnumerable<object>> handler)
        {
            _logger.LogDebug("Registering mock streaming method: {Method}", methodName);
            // For mock purposes, convert streaming handler to regular handler that returns enumerable
            _methodHandlers[methodName] = async parameters =>
            {
                var cancellationToken = CancellationToken.None; // Mock implementation
                var results = new List<object>();
                await foreach (var item in handler(parameters, cancellationToken))
                {
                    results.Add(item);
                }
                return results;
            };
        }

        /// <summary>
        /// Registers a resource
        /// </summary>
        /// <param name="resourceId">Resource identifier</param>
        /// <param name="resource">Resource data</param>
        public void RegisterResource(string resourceId, object resource)
        {
            _logger.LogDebug("Registering mock resource: {ResourceId}", resourceId);
            _resources[resourceId] = resource;
        }

        /// <summary>
        /// Registers a tool
        /// </summary>
        /// <param name="toolId">Tool identifier</param>
        /// <param name="tool">Tool definition</param>
        public void RegisterTool(string toolId, object tool)
        {
            _logger.LogDebug("Registering mock tool: {ToolId}", toolId);
            _tools[toolId] = tool;
        }

        /// <summary>
        /// Registers a prompt
        /// </summary>
        /// <param name="promptId">Prompt identifier</param>
        /// <param name="prompt">Prompt definition</param>
        public void RegisterPrompt(string promptId, object prompt)
        {
            _logger.LogDebug("Registering mock prompt: {PromptId}", promptId);
            _prompts[promptId] = prompt;
        }

        /// <summary>
        /// Configures the server to throw exceptions for testing
        /// </summary>
        /// <param name="method">Method to fail</param>
        /// <param name="exception">Exception to throw</param>
        public void SetupException(string method, Exception exception)
        {
            _methodHandlers[method] = _ => throw exception;
        }

        /// <summary>
        /// Configures the server to simulate delays
        /// </summary>
        /// <param name="method">Method to delay</param>
        /// <param name="delay">Delay duration</param>
        /// <param name="result">Result to return after delay</param>
        public void SetupDelay<T>(string method, TimeSpan delay, T result)
        {
            _methodHandlers[method] = async _ =>
            {
                await Task.Delay(delay);
                return result;
            };
        }

        /// <summary>
        /// Starts the mock server
        /// </summary>
        public async Task StartAsync()
        {
            _logger.LogInformation("Starting mock MCP server...");
            await Task.Delay(10); // Simulate startup delay
            _isListening = true;
            _logger.LogInformation("Mock MCP server started");
        }        /// <summary>
        /// Stops the mock server
        /// </summary>
        public async Task StopAsync()
        {
            _logger.LogInformation("Stopping mock MCP server...");
            await Task.Delay(10); // Simulate graceful shutdown delay
            _isListening = false;
            _logger.LogInformation("Mock MCP server stopped");
        }

        /// <summary>
        /// Stops the mock server (synchronous version for backward compatibility)
        /// </summary>
        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Handles a JSON-RPC request
        /// </summary>
        public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
        {
            if (!_isListening)
            {
                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32000,
                        Message = "Server not listening"
                    }
                };
            }

            _receivedRequests.Add(request);
            IncrementCallCount(request.Method);

            _logger.LogDebug("Mock server handling request: {Method}", request.Method);

            try
            {
                if (_methodHandlers.TryGetValue(request.Method, out var handler))
                {
                    var result = await handler(request.Params);
                      return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = JsonSerializer.SerializeToElement(result)
                    };
                }

                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = $"Method '{request.Method}' not found"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling request for method: {Method}", request.Method);

                return new JsonRpcErrorResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = ex.Message,
                        Data = JsonSerializer.SerializeToElement(new { type = ex.GetType().Name })
                    }
                };
            }
        }

        /// <summary>
        /// Gets the number of times a method was called
        /// </summary>
        /// <param name="method">Method name</param>
        /// <returns>Call count</returns>
        public int GetCallCount(string method)
        {
            return CallCounts.TryGetValue(method, out var count) ? count : 0;
        }

        /// <summary>
        /// Gets requests for a specific method
        /// </summary>
        /// <param name="method">Method name</param>
        /// <returns>Requests for the method</returns>
        public IEnumerable<JsonRpcRequest> GetRequestsForMethod(string method)
        {
            return _receivedRequests.Where(r => r.Method == method);
        }

        /// <summary>
        /// Resets the mock server state
        /// </summary>
        public void Reset()
        {
            _receivedRequests.Clear();
            CallCounts.Clear();
            _isListening = false;
            _logger.LogInformation("Mock server reset");
        }        /// <summary>
        /// Simulates server capabilities
        /// </summary>
        /// <returns>Server capabilities</returns>
        public McpCapabilities GetCapabilities()
        {
            return new McpCapabilities
            {
                Version = "1.0.0",
                Resources = new ResourceCapabilities
                {
                    Subscribe = true,
                    ListChanged = true
                },
                Tools = new ToolCapabilities
                {
                    ListChanged = true
                },
                Prompts = new PromptCapabilities
                {
                    ListChanged = true
                }
            };
        }

        private void IncrementCallCount(string method)
        {
            CallCounts[method] = CallCounts.TryGetValue(method, out var count) ? count + 1 : 1;
        }

        private void SetupDefaultMethods()
        {
            // System methods
            RegisterMethod("system.getCapabilities", async _ =>
            {
                await Task.Delay(5);
                return GetCapabilities();
            });

            RegisterMethod("system.ping", async _ =>
            {
                await Task.Delay(2);
                return new { status = "pong", timestamp = DateTime.UtcNow };
            });

            RegisterMethod("system.info", async _ =>
            {
                await Task.Delay(5);
                return new
                {
                    name = "Mock MCP Server",
                    version = "1.0.0",
                    description = "Mock server for testing",
                    mockServer = true
                };
            });

            // Resource methods
            RegisterMethod("resources/list", async _ =>
            {
                await Task.Delay(10);
                return new
                {
                    resources = _resources.Keys.Select(id => new
                    {
                        uri = $"mock://resource/{id}",
                        name = id,
                        description = $"Mock resource {id}",
                        mimeType = "application/json"
                    }).ToArray()
                };
            });

            RegisterMethod("resources/read", async parameters =>
            {
                await Task.Delay(10);

                if (parameters.TryGetProperty("uri", out var uriElement))
                {
                    var uri = uriElement.GetString();
                    var resourceId = uri?.Split('/').LastOrDefault();

                    if (resourceId != null && _resources.TryGetValue(resourceId, out var resource))
                    {
                        return new
                        {
                            contents = new[]
                            {
                                new
                                {
                                    uri = uri,
                                    mimeType = "application/json",
                                    text = JsonSerializer.Serialize(resource)
                                }
                            }
                        };
                    }
                }

                throw new ArgumentException("Resource not found");
            });

            // Tool methods
            RegisterMethod("tools/list", async _ =>
            {
                await Task.Delay(10);
                return new
                {
                    tools = _tools.Select(kvp => new
                    {
                        name = kvp.Key,
                        description = $"Mock tool {kvp.Key}",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object>
                            {
                                ["input"] = new { type = "string", description = "Tool input" }
                            }
                        }
                    }).ToArray()
                };
            });

            RegisterMethod("tools/call", async parameters =>
            {
                await Task.Delay(15);

                if (parameters.TryGetProperty("name", out var nameElement))
                {
                    var toolName = nameElement.GetString();

                    if (_tools.ContainsKey(toolName))
                    {
                        var input = parameters.TryGetProperty("arguments", out var argsElement)
                            ? argsElement.ToString()
                            : "{}";

                        return new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = $"Mock tool '{toolName}' executed with input: {input}"
                                }
                            }
                        };
                    }
                }

                throw new ArgumentException("Tool not found");
            });

            // Prompt methods
            RegisterMethod("prompts/list", async _ =>
            {
                await Task.Delay(10);
                return new
                {
                    prompts = _prompts.Select(kvp => new
                    {
                        name = kvp.Key,
                        description = $"Mock prompt {kvp.Key}",
                        arguments = new[]
                        {
                            new
                            {
                                name = "input",
                                description = "Prompt input",
                                required = false
                            }
                        }
                    }).ToArray()
                };
            });

            RegisterMethod("prompts/get", async parameters =>
            {
                await Task.Delay(10);

                if (parameters.TryGetProperty("name", out var nameElement))
                {
                    var promptName = nameElement.GetString();

                    if (_prompts.ContainsKey(promptName))
                    {
                        return new
                        {
                            description = $"Mock prompt {promptName}",
                            messages = new[]
                            {
                                new
                                {
                                    role = "user",
                                    content = new
                                    {
                                        type = "text",
                                        text = $"This is a mock prompt for {promptName}"
                                    }
                                }
                            }
                        };
                    }
                }

                throw new ArgumentException("Prompt not found");
            });
        }

        public void Dispose()
        {
            Stop();
            Reset();
            _logger.LogInformation("Mock server disposed");
        }
    }
}
