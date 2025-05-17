using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Core.Interfaces;
using ModelContextProtocol.Core.Models.Mcp;

namespace ModelContextProtocol.Extensions.Client
{
    /// <summary>
    /// Adapter class that implements IMcpClient interface using the McpClient class
    /// </summary>
    public class McpClientAdapter : IMcpClient
    {
        private readonly McpClient _client;
        private readonly ILogger<McpClientAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the McpClientAdapter class
        /// </summary>
        /// <param name="client">The McpClient instance</param>
        /// <param name="logger">Logger for the adapter</param>
        public McpClientAdapter(McpClient client, ILogger<McpClientAdapter> logger)
        {
            _client = client;
            _logger = logger;
        }

        /// <summary>
        /// Connects to the MCP server
        /// </summary>
        public async Task ConnectAsync()
        {
            _logger.LogInformation("Connecting to MCP server");
            // McpClient doesn't have a ConnectAsync method, but it connects automatically when needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Disconnects from the MCP server
        /// </summary>
        public async Task DisconnectAsync()
        {
            _logger.LogInformation("Disconnecting from MCP server");
            // McpClient doesn't have a DisconnectAsync method, but we can dispose it if needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Calls a method on the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected result type</typeparam>
        /// <param name="method">Method name</param>
        /// <param name="parameters">Method parameters</param>
        /// <returns>Method result</returns>
        public async Task<TResult> CallMethodAsync<TResult>(string method, object parameters = null)
        {
            _logger.LogDebug("Calling method {Method}", method);
            return await _client.CallMethodAsync<TResult>(method, parameters);
        }

        /// <summary>
        /// Gets the capabilities of the MCP server
        /// </summary>
        /// <returns>Server capabilities</returns>
        public async Task<McpCapabilities> GetCapabilitiesAsync()
        {
            _logger.LogDebug("Getting server capabilities");
            return await _client.CallMethodAsync<McpCapabilities>("system.getCapabilities");
        }

        /// <summary>
        /// Gets a resource from the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected resource type</typeparam>
        /// <param name="resourceId">Resource ID</param>
        /// <returns>Resource</returns>
        public async Task<TResult> GetResourceAsync<TResult>(string resourceId)
        {
            _logger.LogDebug("Getting resource {ResourceId}", resourceId);
            return await _client.CallMethodAsync<TResult>("resource.get", new { id = resourceId });
        }

        /// <summary>
        /// Executes a tool on the MCP server
        /// </summary>
        /// <typeparam name="TResult">Expected result type</typeparam>
        /// <param name="toolId">Tool ID</param>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        public async Task<TResult> ExecuteToolAsync<TResult>(string toolId, object input)
        {
            _logger.LogDebug("Executing tool {ToolId}", toolId);
            return await _client.CallMethodAsync<TResult>("tool.execute", new { id = toolId, input });
        }

        /// <summary>
        /// Renders a prompt on the MCP server
        /// </summary>
        /// <param name="promptId">Prompt ID</param>
        /// <param name="variables">Prompt variables</param>
        /// <returns>Rendered prompt</returns>
        public async Task<string> RenderPromptAsync(string promptId, object variables)
        {
            _logger.LogDebug("Rendering prompt {PromptId}", promptId);
            return await _client.CallMethodAsync<string>("prompt.render", new { id = promptId, variables });
        }

        /// <summary>
        /// Sends a message to the MCP server
        /// </summary>
        /// <param name="message">Message to send</param>
        public async Task SendMessageAsync(string message)
        {
            _logger.LogDebug("Sending message");
            await _client.CallMethodAsync<object>("message.send", new { content = message });
        }

        /// <summary>
        /// Receives a message from the MCP server
        /// </summary>
        /// <returns>Received message</returns>
        public async Task<string> ReceiveMessageAsync()
        {
            _logger.LogDebug("Receiving message");
            return await _client.CallMethodAsync<string>("message.receive");
        }
    }
}
