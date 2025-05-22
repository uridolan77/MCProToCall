using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Interfaces
{
    /// <summary>
    /// Interface for MCP server implementations
    /// </summary>
    public interface IMcpServer
    {
        /// <summary>
        /// Starts the MCP server
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the MCP server
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Registers a method handler
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="handler">Method handler</param>
        void RegisterMethod(string methodName, Func<JsonElement, Task<object>> handler);

        /// <summary>
        /// Registers a streaming method handler
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="handler">Streaming method handler</param>
        void RegisterStreamingMethod(string methodName, Func<JsonElement, CancellationToken, IAsyncEnumerable<object>> handler);

        /// <summary>
        /// Handles a JSON-RPC request
        /// </summary>
        /// <param name="request">The request to handle</param>
        /// <returns>The response</returns>
        Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request);
    }
}