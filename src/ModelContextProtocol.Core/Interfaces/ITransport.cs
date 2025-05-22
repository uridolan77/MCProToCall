using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Interfaces
{
    /// <summary>
    /// Interface for transport implementations that can send and receive JSON-RPC messages
    /// </summary>
    public interface ITransport
    {
        /// <summary>
        /// Receives a JSON-RPC request from the transport
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The received JSON-RPC request, or null if the connection was closed</returns>
        Task<JsonRpcRequest> ReceiveRequestAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends a JSON-RPC response through the transport
        /// </summary>
        /// <param name="response">The response to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendResponseAsync(JsonRpcResponse response, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a JSON-RPC notification through the transport
        /// </summary>
        /// <param name="notification">The notification to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendNotificationAsync(object notification, CancellationToken cancellationToken);

        /// <summary>
        /// Closes the transport connection
        /// </summary>
        Task CloseAsync();
    }
}
