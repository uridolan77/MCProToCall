using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace ModelContextProtocol.Core.Streaming
{
    /// <summary>
    /// Interface for streaming-capable methods
    /// </summary>
    public interface IStreamingMethod
    {
        /// <summary>
        /// Executes the streaming method and returns an asynchronous stream of results
        /// </summary>
        /// <param name="parameters">Method parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An asynchronous stream of result objects</returns>
        IAsyncEnumerable<object> ExecuteStreamingAsync(
            JsonElement parameters,
            CancellationToken cancellationToken);
    }
}
