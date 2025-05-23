using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Mock MCP client for testing scenarios
    /// </summary>
    public class MockMcpClient : IMcpClient
    {
        private readonly ILogger<MockMcpClient> _logger;
        private readonly Dictionary<string, Func<object, Task<object>>> _methodHandlers;
        private readonly Dictionary<string, object> _resources;
        private readonly Dictionary<string, Func<object, Task<object>>> _tools;
        private bool _isConnected;
        private McpCapabilities _capabilities;

        public MockMcpClient(ILogger<MockMcpClient> logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MockMcpClient>.Instance;
            _methodHandlers = new Dictionary<string, Func<object, Task<object>>>();
            _resources = new Dictionary<string, object>();
            _tools = new Dictionary<string, Func<object, Task<object>>>();
            _isConnected = false;

            SetupDefaultCapabilities();
            SetupDefaultMethods();
        }

        /// <summary>
        /// Indicates if the mock client is connected
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Call count for tracking invocations
        /// </summary>
        public Dictionary<string, int> CallCounts { get; } = new Dictionary<string, int>();

        /// <summary>
        /// Last parameters passed to methods
        /// </summary>
        public Dictionary<string, object> LastParameters { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Configures a method handler for testing
        /// </summary>
        /// <param name="method">Method name</param>
        /// <param name="handler">Handler function</param>
        public void SetupMethod(string method, Func<object, Task<object>> handler)
        {
            _methodHandlers[method] = handler;
        }

        /// <summary>
        /// Configures a resource for testing
        /// </summary>
        /// <param name="resourceId">Resource identifier</param>
        /// <param name="resource">Resource data</param>
        public void SetupResource(string resourceId, object resource)
        {
            _resources[resourceId] = resource;
        }

        /// <summary>
        /// Configures a tool for testing
        /// </summary>
        /// <param name="toolId">Tool identifier</param>
        /// <param name="handler">Tool handler function</param>
        public void SetupTool(string toolId, Func<object, Task<object>> handler)
        {
            _tools[toolId] = handler;
        }

        /// <summary>
        /// Simulates connection to the server
        /// </summary>
        public async Task ConnectAsync()
        {
            _logger.LogInformation("Mock client connecting...");
            await Task.Delay(10); // Simulate connection delay
            _isConnected = true;
            _logger.LogInformation("Mock client connected");
        }

        /// <summary>
        /// Simulates disconnection from the server
        /// </summary>
        public async Task DisconnectAsync()
        {
            _logger.LogInformation("Mock client disconnecting...");
            await Task.Delay(10); // Simulate disconnection delay
            _isConnected = false;
            _logger.LogInformation("Mock client disconnected");
        }

        /// <summary>
        /// Mock implementation of CallMethodAsync
        /// </summary>
        public async Task<TResult> CallMethodAsync<TResult>(string method, object parameters)
        {
            IncrementCallCount(method);
            LastParameters[method] = parameters;

            _logger.LogDebug("Mock client calling method: {Method}", method);

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            if (_methodHandlers.TryGetValue(method, out var handler))
            {
                var result = await handler(parameters);
                return (TResult)result;
            }

            // Default responses for common methods
            switch (method)
            {
                case "system.getCapabilities":
                    return (TResult)(object)_capabilities;

                case "system.ping":
                    return (TResult)(object)new { status = "pong", timestamp = DateTime.UtcNow };

                case "system.info":
                    return (TResult)(object)new
                    {
                        name = "Mock MCP Client",
                        version = "1.0.0",
                        description = "Mock client for testing"
                    };

                default:
                    throw new NotSupportedException($"Mock method '{method}' not configured");
            }
        }

        /// <summary>
        /// Mock implementation of GetCapabilitiesAsync
        /// </summary>
        public async Task<McpCapabilities> GetCapabilitiesAsync()
        {
            IncrementCallCount("GetCapabilities");
            _logger.LogDebug("Mock client getting capabilities");

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            await Task.Delay(10); // Simulate network delay
            return _capabilities;
        }

        /// <summary>
        /// Mock implementation of GetResourceAsync
        /// </summary>
        public async Task<TResult> GetResourceAsync<TResult>(string resourceId)
        {
            IncrementCallCount("GetResource");
            LastParameters["GetResource"] = resourceId;

            _logger.LogDebug("Mock client getting resource: {ResourceId}", resourceId);

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            await Task.Delay(10); // Simulate network delay

            if (_resources.TryGetValue(resourceId, out var resource))
            {
                return (TResult)resource;
            }

            throw new ArgumentException($"Resource '{resourceId}' not found", nameof(resourceId));
        }

        /// <summary>
        /// Mock implementation of ExecuteToolAsync
        /// </summary>
        public async Task<TResult> ExecuteToolAsync<TResult>(string toolId, object input)
        {
            IncrementCallCount("ExecuteTool");
            LastParameters["ExecuteTool"] = new { toolId, input };

            _logger.LogDebug("Mock client executing tool: {ToolId}", toolId);

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            if (_tools.TryGetValue(toolId, out var tool))
            {
                var result = await tool(input);
                return (TResult)result;
            }

            throw new ArgumentException($"Tool '{toolId}' not found", nameof(toolId));
        }        /// <summary>
        /// Mock implementation of RenderPromptAsync
        /// </summary>
        public async Task<string> RenderPromptAsync(string promptId, object variables)
        {
            IncrementCallCount("RenderPrompt");
            LastParameters["RenderPrompt"] = new { promptId, variables };

            _logger.LogDebug("Mock client rendering prompt: {PromptId}", promptId);

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            await Task.Delay(10); // Simulate network delay

            // Return a mock rendered prompt
            return $"Rendered prompt '{promptId}' with variables: {JsonSerializer.Serialize(variables)}";
        }

        /// <summary>
        /// Mock implementation of SendMessageAsync
        /// </summary>
        public async Task SendMessageAsync(string message)
        {
            IncrementCallCount("SendMessage");
            LastParameters["SendMessage"] = message;

            _logger.LogDebug("Mock client sending message: {Message}", message);

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            await Task.Delay(10); // Simulate network delay
        }

        /// <summary>
        /// Mock implementation of ReceiveMessageAsync
        /// </summary>
        public async Task<string> ReceiveMessageAsync()
        {
            IncrementCallCount("ReceiveMessage");

            _logger.LogDebug("Mock client receiving message");

            if (!_isConnected)
            {
                throw new InvalidOperationException("Mock client is not connected");
            }

            await Task.Delay(10); // Simulate network delay

            // Return a mock message
            return $"Mock message received at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        }

        /// <summary>
        /// Resets all call counts and parameters
        /// </summary>
        public void Reset()
        {
            CallCounts.Clear();
            LastParameters.Clear();
            _isConnected = false;
        }

        /// <summary>
        /// Configures the mock to throw exceptions for testing error scenarios
        /// </summary>
        /// <param name="method">Method to fail</param>
        /// <param name="exception">Exception to throw</param>
        public void SetupException(string method, Exception exception)
        {
            _methodHandlers[method] = _ => throw exception;
        }

        /// <summary>
        /// Configures the mock to simulate network delays
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
        /// Gets the number of times a method was called
        /// </summary>
        /// <param name="method">Method name</param>
        /// <returns>Call count</returns>
        public int GetCallCount(string method)
        {
            return CallCounts.TryGetValue(method, out var count) ? count : 0;
        }

        /// <summary>
        /// Gets the last parameters passed to a method
        /// </summary>
        /// <param name="method">Method name</param>
        /// <returns>Last parameters or null if method not called</returns>
        public object GetLastParameters(string method)
        {
            return LastParameters.TryGetValue(method, out var parameters) ? parameters : null;
        }

        private void IncrementCallCount(string method)
        {
            CallCounts[method] = CallCounts.TryGetValue(method, out var count) ? count + 1 : 1;
        }        private void SetupDefaultCapabilities()
        {
            _capabilities = new McpCapabilities
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

        private void SetupDefaultMethods()
        {
            // Setup default method handlers that can be overridden
            _methodHandlers["system.getCapabilities"] = async _ =>
            {
                await Task.Delay(10);
                return _capabilities;
            };

            _methodHandlers["system.ping"] = async _ =>
            {
                await Task.Delay(5);
                return new { status = "pong", timestamp = DateTime.UtcNow };
            };

            _methodHandlers["system.info"] = async _ =>
            {
                await Task.Delay(10);
                return new
                {
                    name = "Mock MCP Client",
                    version = "1.0.0",
                    description = "Mock client for testing",
                    mockClient = true
                };
            };
        }

        public void Dispose()
        {
            Reset();
            _logger.LogInformation("Mock client disposed");
        }
    }
}
