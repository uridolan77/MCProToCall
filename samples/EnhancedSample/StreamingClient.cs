using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Core.Models.JsonRpc;
using ModelContextProtocol.Core.Streaming;

namespace EnhancedSample
{
    /// <summary>
    /// Example WebSocket client with streaming support
    /// </summary>
    public class StreamingClient : IDisposable
    {
        private readonly Uri _serverUri;
        private ClientWebSocket _webSocket;
        private readonly CancellationTokenSource _cts;
        private readonly StreamConsumer _streamConsumer;
        private int _requestId;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingClient"/> class
        /// </summary>
        /// <param name="serverUri">Server URI</param>
        public StreamingClient(string serverUri)
        {
            _serverUri = new Uri(serverUri);
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            _streamConsumer = new StreamConsumer();
            _requestId = 0;
        }

        /// <summary>
        /// Connects to the server
        /// </summary>
        public async Task ConnectAsync()
        {
            await _webSocket.ConnectAsync(_serverUri, _cts.Token);
            Console.WriteLine($"Connected to {_serverUri}");

            // Start listening for messages
            _ = Task.Run(ReceiveMessagesAsync);
        }

        /// <summary>
        /// Calls a method on the server
        /// </summary>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="method">Method name</param>
        /// <param name="parameters">Method parameters</param>
        /// <returns>Method result</returns>
        public async Task<TResult> CallMethodAsync<TResult>(string method, object parameters = null)
        {
            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new JsonRpcRequest
            {
                Id = requestId,
                Method = method,
                Params = JsonDocument.Parse(JsonSerializer.Serialize(parameters ?? new { })).RootElement
            };

            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(requestBytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);

            // Wait for response
            var responseCompletionSource = new TaskCompletionSource<JsonRpcResponse>();
            var responseHandler = new Action<JsonRpcResponse>(response =>
            {
                if (response.Id == requestId)
                {
                    responseCompletionSource.TrySetResult(response);
                }
            });

            // Register response handler
            ResponseReceived += responseHandler;

            try
            {
                var response = await responseCompletionSource.Task;
                var resultJson = JsonSerializer.Serialize(response.Result);
                return JsonSerializer.Deserialize<TResult>(resultJson);
            }
            finally
            {
                // Unregister response handler
                ResponseReceived -= responseHandler;
            }
        }

        /// <summary>
        /// Calls a streaming method and returns a stream consumer
        /// </summary>
        /// <param name="method">Method name</param>
        /// <param name="parameters">Method parameters</param>
        /// <returns>Stream ID</returns>
        public async Task<string> CallStreamingMethodAsync(string method, object parameters = null)
        {
            var result = await CallMethodAsync<StreamingResult>(method, parameters);
            return result.StreamId;
        }

        /// <summary>
        /// Consumes a stream
        /// </summary>
        /// <typeparam name="T">Stream item type</typeparam>
        /// <param name="streamId">Stream ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of stream items</returns>
        public async IAsyncEnumerable<T> ConsumeStreamAsync<T>(string streamId, CancellationToken cancellationToken = default)
        {
            await foreach (var item in _streamConsumer.ConsumeAsync<T>(cancellationToken))
            {
                yield return item;
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            var receiveBuffer = new ArraySegment<byte>(buffer);

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using var ms = new System.IO.MemoryStream();
                    
                    do
                    {
                        result = await _webSocket.ReceiveAsync(receiveBuffer, _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                            return;
                        }

                        ms.Write(receiveBuffer.Array, receiveBuffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    ms.Seek(0, System.IO.SeekOrigin.Begin);
                    var message = Encoding.UTF8.GetString(ms.ToArray());

                    // Try to parse as response
                    try
                    {
                        var response = JsonSerializer.Deserialize<JsonRpcResponse>(message);
                        if (response != null)
                        {
                            OnResponseReceived(response);
                            continue;
                        }
                    }
                    catch { }

                    // Try to parse as stream notification
                    try
                    {
                        var notification = JsonSerializer.Deserialize<JsonRpcStreamNotification>(message);
                        if (notification != null)
                        {
                            await _streamConsumer.ProcessNotificationAsync(notification);
                            continue;
                        }
                    }
                    catch { }

                    Console.WriteLine($"Received unknown message: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving messages: {ex.Message}");
            }
        }

        /// <summary>
        /// Event raised when a response is received
        /// </summary>
        public event Action<JsonRpcResponse> ResponseReceived;

        /// <summary>
        /// Raises the ResponseReceived event
        /// </summary>
        /// <param name="response">Response</param>
        protected virtual void OnResponseReceived(JsonRpcResponse response)
        {
            ResponseReceived?.Invoke(response);
        }

        /// <summary>
        /// Disposes the client
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _webSocket.Dispose();
            _cts.Dispose();
        }

        /// <summary>
        /// Result of a streaming method call
        /// </summary>
        private class StreamingResult
        {
            /// <summary>
            /// Gets or sets the stream ID
            /// </summary>
            public string StreamId { get; set; }

            /// <summary>
            /// Gets or sets the message
            /// </summary>
            public string Message { get; set; }
        }
    }
}
