using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Streaming
{
    /// <summary>
    /// Client-side stream consumer
    /// </summary>
    public class StreamConsumer
    {
        private readonly Channel<StreamParams> _channel;
        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamConsumer"/> class
        /// </summary>
        public StreamConsumer()
        {
            _channel = Channel.CreateUnbounded<StreamParams>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Gets the stream ID
        /// </summary>
        public string StreamId { get; private set; }

        /// <summary>
        /// Processes a stream notification
        /// </summary>
        /// <param name="notification">The notification to process</param>
        public async Task ProcessNotificationAsync(JsonRpcStreamNotification notification)
        {
            StreamId = notification.Params.StreamId;
            
            if (notification.Params.IsComplete)
            {
                _channel.Writer.TryComplete();
            }
            else
            {
                await _channel.Writer.WriteAsync(notification.Params);
            }
        }

        /// <summary>
        /// Consumes the stream and returns a typed async enumerable
        /// </summary>
        /// <typeparam name="T">Type of the stream items</typeparam>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of typed items</returns>
        public async IAsyncEnumerable<T> ConsumeAsync<T>(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);

            await foreach (var item in _channel.Reader.ReadAllAsync(linkedCts.Token))
            {
                if (item.Data is T typedData)
                {
                    yield return typedData;
                }
                else if (item.Data is JsonElement element)
                {
                    yield return JsonSerializer.Deserialize<T>(element.GetRawText());
                }
            }
        }

        /// <summary>
        /// Cancels the stream consumption
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
            _channel.Writer.TryComplete();
        }
    }
}
