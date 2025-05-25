using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Core.Models.JsonRpc;

namespace ModelContextProtocol.Core.Streaming
{
    /// <summary>
    /// Represents a streaming JSON-RPC notification for partial results
    /// </summary>
    public class JsonRpcStreamNotification
    {
        public string JsonRpc { get; set; } = "2.0";
        public string Method { get; set; } = "stream.data";
        public StreamParams Params { get; set; }
    }

    public class StreamParams
    {
        public string StreamId { get; set; }
        public int SequenceNumber { get; set; }
        public object Data { get; set; }
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Interface for streaming-capable methods
    /// </summary>
    public interface IStreamingMethod
    {
        IAsyncEnumerable<object> ExecuteStreamingAsync(
            JsonElement parameters,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Manages streaming responses
    /// </summary>
    public class StreamingResponseManager
    {
        private readonly ILogger<StreamingResponseManager> _logger;
        private readonly Dictionary<string, StreamContext> _activeStreams;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public StreamingResponseManager(ILogger<StreamingResponseManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeStreams = new Dictionary<string, StreamContext>();
        }

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

    /// <summary>
    /// Example streaming method implementation for LLM responses
    /// </summary>
    public class LlmStreamingMethod : IStreamingMethod
    {
        private readonly ILogger<LlmStreamingMethod> _logger;

        public LlmStreamingMethod(ILogger<LlmStreamingMethod> logger)
        {
            _logger = logger;
        }

        public async IAsyncEnumerable<object> ExecuteStreamingAsync(
            JsonElement parameters,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var prompt = parameters.GetProperty("prompt").GetString();
            var maxTokens = parameters.GetProperty("maxTokens").GetInt32();

            _logger.LogInformation("Starting LLM streaming for prompt: {Prompt}", 
                prompt?.Substring(0, Math.Min(50, prompt.Length)) + "...");

            // Simulate LLM token generation
            var tokens = new[] 
            { 
                "The", "quick", "brown", "fox", "jumps", 
                "over", "the", "lazy", "dog", "." 
            };

            foreach (var token in tokens)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                // Simulate processing time
                await Task.Delay(100, cancellationToken);

                yield return new
                {
                    token = token,
                    timestamp = DateTime.UtcNow,
                    confidence = 0.95
                };
            }
        }
    }

    /// <summary>
    /// Client-side stream consumer
    /// </summary>
    public class StreamConsumer
    {
        private readonly Channel<StreamParams> _channel;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public StreamConsumer()
        {
            _channel = Channel.CreateUnbounded<StreamParams>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public string StreamId { get; private set; }

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

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
            _channel.Writer.TryComplete();
        }
    }
}