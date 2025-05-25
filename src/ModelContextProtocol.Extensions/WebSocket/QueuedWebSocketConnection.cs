using System.Net.WebSockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.WebSocket
{
    /// <summary>
    /// WebSocket connection with message queuing for reliable delivery
    /// </summary>
    public class QueuedWebSocketConnection : IDisposable
    {
        private readonly System.Net.WebSockets.WebSocket _webSocket;
        private readonly Channel<WebSocketMessage> _sendQueue;
        private readonly SemaphoreSlim _sendSemaphore;
        private readonly ILogger<QueuedWebSocketConnection> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _sendLoopTask;
        private readonly QueuedWebSocketOptions _options;
        private bool _disposed;

        public QueuedWebSocketConnection(
            System.Net.WebSockets.WebSocket webSocket,
            QueuedWebSocketOptions options,
            ILogger<QueuedWebSocketConnection> logger)
        {
            _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _sendQueue = Channel.CreateBounded<WebSocketMessage>(new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            _sendSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            // Start the send loop
            _sendLoopTask = ProcessSendQueueAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("QueuedWebSocketConnection initialized with queue capacity {Capacity}",
                _options.QueueCapacity);
        }

        /// <summary>
        /// Enqueues a message for sending
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async ValueTask EnqueueMessageAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException($"WebSocket is not open. Current state: {_webSocket.State}");
            }

            try
            {
                await _sendQueue.Writer.WriteAsync(message, cancellationToken);
                _logger.LogDebug("Message enqueued for sending: {MessageType}, Size: {Size} bytes",
                    message.MessageType, message.Data.Length);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException("Send queue is closed");
            }
        }

        /// <summary>
        /// Receives a message from the WebSocket
        /// </summary>
        /// <param name="buffer">Buffer to receive into</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>WebSocket receive result</returns>
        public async ValueTask<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                _logger.LogDebug("Received WebSocket message: {MessageType}, Size: {Size} bytes, EndOfMessage: {EndOfMessage}",
                    result.MessageType, result.Count, result.EndOfMessage);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving WebSocket message");
                throw;
            }
        }

        /// <summary>
        /// Closes the WebSocket connection gracefully
        /// </summary>
        /// <param name="closeStatus">Close status</param>
        /// <param name="statusDescription">Status description</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task CloseAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string? statusDescription = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed || _webSocket.State == WebSocketState.Closed)
                return;

            try
            {
                _logger.LogInformation("Closing WebSocket connection with status {CloseStatus}: {Description}",
                    closeStatus, statusDescription);

                // Stop accepting new messages
                _sendQueue.Writer.Complete();

                // Wait for send queue to drain (with timeout)
                using var timeoutCts = new CancellationTokenSource(_options.DrainTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                try
                {
                    await _sendLoopTask.WaitAsync(combinedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Send queue did not drain within timeout period");
                }

                // Close the WebSocket
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing WebSocket connection");
            }
        }

        /// <summary>
        /// Gets the current state of the WebSocket
        /// </summary>
        public WebSocketState State => _webSocket.State;

        /// <summary>
        /// Gets the close status of the WebSocket
        /// </summary>
        public WebSocketCloseStatus? CloseStatus => _webSocket.CloseStatus;

        /// <summary>
        /// Gets the close status description
        /// </summary>
        public string? CloseStatusDescription => _webSocket.CloseStatusDescription;

        /// <summary>
        /// Gets queue statistics
        /// </summary>
        public QueueStatistics GetQueueStatistics()
        {
            var reader = _sendQueue.Reader;
            return new QueueStatistics
            {
                QueuedMessages = reader.CanCount ? reader.Count : -1,
                QueueCapacity = _options.QueueCapacity,
                IsCompleted = reader.Completion.IsCompleted
            };
        }

        private async Task ProcessSendQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Send queue processing started");

            try
            {
                await foreach (var message in _sendQueue.Reader.ReadAllAsync(cancellationToken))
                {
                    await SendMessageWithRetryAsync(message, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Send queue processing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in send queue processing");
            }
            finally
            {
                _logger.LogDebug("Send queue processing completed");
            }
        }

        private async Task SendMessageWithRetryAsync(WebSocketMessage message, CancellationToken cancellationToken)
        {
            var attempt = 0;
            var maxAttempts = _options.MaxRetryAttempts;

            while (attempt < maxAttempts)
            {
                try
                {
                    await _sendSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (_webSocket.State != WebSocketState.Open)
                        {
                            _logger.LogWarning("Cannot send message, WebSocket state is {State}", _webSocket.State);
                            return;
                        }

                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(message.Data),
                            message.MessageType,
                            message.EndOfMessage,
                            cancellationToken);

                        _logger.LogDebug("Message sent successfully: {MessageType}, Size: {Size} bytes",
                            message.MessageType, message.Data.Length);
                        return;
                    }
                    finally
                    {
                        _sendSemaphore.Release();
                    }
                }
                catch (Exception ex) when (attempt < maxAttempts - 1)
                {
                    attempt++;
                    var delay = TimeSpan.FromMilliseconds(_options.RetryDelayMs * Math.Pow(2, attempt - 1));

                    _logger.LogWarning(ex,
                        "Failed to send message (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                        attempt, maxAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send message after {Attempts} attempts", maxAttempts);
                    throw;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(QueuedWebSocketConnection));
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _cancellationTokenSource.Cancel();
                _sendQueue.Writer.Complete();

                // Wait for send loop to complete (with timeout)
                if (!_sendLoopTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Send loop did not complete within timeout during disposal");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
            finally
            {
                _sendSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
                _webSocket?.Dispose();
            }
        }
    }

    /// <summary>
    /// WebSocket message for queuing
    /// </summary>
    public class WebSocketMessage
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public WebSocketMessageType MessageType { get; set; } = WebSocketMessageType.Text;
        public bool EndOfMessage { get; set; } = true;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? MessageId { get; set; }
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Configuration options for queued WebSocket connections
    /// </summary>
    public class QueuedWebSocketOptions
    {
        /// <summary>
        /// Maximum number of messages that can be queued
        /// </summary>
        public int QueueCapacity { get; set; } = 1000;

        /// <summary>
        /// Maximum number of retry attempts for failed sends
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay in milliseconds between retry attempts
        /// </summary>
        public int RetryDelayMs { get; set; } = 100;

        /// <summary>
        /// Timeout for draining the queue during close
        /// </summary>
        public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Whether to enable message prioritization
        /// </summary>
        public bool EnablePrioritization { get; set; } = false;

        /// <summary>
        /// Whether to enable message deduplication
        /// </summary>
        public bool EnableDeduplication { get; set; } = false;
    }

    /// <summary>
    /// Statistics about the message queue
    /// </summary>
    public class QueueStatistics
    {
        public int QueuedMessages { get; set; }
        public int QueueCapacity { get; set; }
        public bool IsCompleted { get; set; }
        public double UtilizationPercentage => QueueCapacity > 0 ? (double)QueuedMessages / QueueCapacity * 100 : 0;
    }
}
