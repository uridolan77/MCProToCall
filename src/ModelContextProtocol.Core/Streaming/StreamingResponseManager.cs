using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Streaming
{
    /// <summary>
    /// Manages streaming responses
    /// </summary>
    public class StreamingResponseManager
    {
        private readonly ILogger<StreamingResponseManager> _logger;
        private readonly Dictionary<string, StreamContext> _activeStreams;
        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingResponseManager"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
        public StreamingResponseManager(ILogger<StreamingResponseManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeStreams = new Dictionary<string, StreamContext>();
        }

        /// <summary>
        /// Starts a new stream
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <param name="dataStream">Data stream</param>
        /// <param name="sendNotification">Function to send notifications</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream ID</returns>
        public async Task<string> StartStreamAsync(
            string requestId,
            IAsyncEnumerable<object> dataStream,
            Func<JsonRpcStreamNotification, Task> sendNotification,
            CancellationToken cancellationToken)
        {
            var streamId = Guid.NewGuid().ToString();
            var context = new StreamContext
            {
                StreamId = streamId,
                RequestId = requestId,
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            };

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _activeStreams[streamId] = context;
            }
            finally
            {
                _lock.Release();
            }

            // Start streaming in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await StreamDataAsync(context, dataStream, sendNotification);
                }
                finally
                {
                    await RemoveStreamAsync(streamId);
                }
            }, context.CancellationTokenSource.Token);

            return streamId;
        }

        private async Task StreamDataAsync(
            StreamContext context,
            IAsyncEnumerable<object> dataStream,
            Func<JsonRpcStreamNotification, Task> sendNotification)
        {
            var sequenceNumber = 0;

            try
            {
                await foreach (var item in dataStream.WithCancellation(context.CancellationTokenSource.Token))
                {
                    var notification = new JsonRpcStreamNotification
                    {
                        Params = new StreamParams
                        {
                            StreamId = context.StreamId,
                            SequenceNumber = sequenceNumber++,
                            Data = item,
                            IsComplete = false
                        }
                    };

                    await sendNotification(notification);
                    _logger.LogDebug("Sent stream chunk {SequenceNumber} for stream {StreamId}", 
                        sequenceNumber - 1, context.StreamId);
                }

                // Send completion notification
                var completionNotification = new JsonRpcStreamNotification
                {
                    Params = new StreamParams
                    {
                        StreamId = context.StreamId,
                        SequenceNumber = sequenceNumber,
                        Data = null,
                        IsComplete = true
                    }
                };

                await sendNotification(completionNotification);
                _logger.LogInformation("Stream {StreamId} completed successfully", context.StreamId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stream {StreamId} was cancelled", context.StreamId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream {StreamId}", context.StreamId);
                
                // Send error notification
                var errorNotification = new JsonRpcStreamNotification
                {
                    Method = "stream.error",
                    Params = new StreamParams
                    {
                        StreamId = context.StreamId,
                        SequenceNumber = sequenceNumber,
                        Data = new { error = ex.Message },
                        IsComplete = true
                    }
                };

                await sendNotification(errorNotification);
            }
        }

        /// <summary>
        /// Cancels a stream
        /// </summary>
        /// <param name="streamId">Stream ID</param>
        public async Task CancelStreamAsync(string streamId)
        {
            await _lock.WaitAsync();
            try
            {
                if (_activeStreams.TryGetValue(streamId, out var context))
                {
                    context.CancellationTokenSource.Cancel();
                    _activeStreams.Remove(streamId);
                    _logger.LogInformation("Cancelled stream {StreamId}", streamId);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task RemoveStreamAsync(string streamId)
        {
            await _lock.WaitAsync();
            try
            {
                if (_activeStreams.TryGetValue(streamId, out var context))
                {
                    context.CancellationTokenSource?.Dispose();
                    _activeStreams.Remove(streamId);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private class StreamContext
        {
            public string StreamId { get; set; }
            public string RequestId { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
        }
    }
}
