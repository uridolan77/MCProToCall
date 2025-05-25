using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// High-performance message processor using memory pooling
    /// </summary>
    public class MessageProcessor : IDisposable
    {
        private readonly ArrayPool<byte> _arrayPool;
        private readonly MemoryPool<byte> _memoryPool;
        private readonly ILogger<MessageProcessor> _logger;
        private bool _disposed;

        public MessageProcessor(ILogger<MessageProcessor> logger)
        {
            _arrayPool = ArrayPool<byte>.Shared;
            _memoryPool = MemoryPool<byte>.Shared;
            _logger = logger;
        }

        /// <summary>
        /// Processes messages from a stream using memory pooling
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="handler">Handler for processing message chunks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="bufferSize">Buffer size for reading</param>
        public async ValueTask ProcessMessageAsync(
            Stream stream,
            Func<ReadOnlyMemory<byte>, ValueTask> handler,
            CancellationToken cancellationToken,
            int bufferSize = 4096)
        {
            ThrowIfDisposed();

            using var owner = _memoryPool.Rent(bufferSize);
            var memory = owner.Memory;

            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(memory, cancellationToken)) > 0)
                {
                    await handler(memory[..bytesRead]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message stream");
                throw;
            }
        }

        /// <summary>
        /// Processes messages in batches for better throughput
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="batchHandler">Handler for processing message batches</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="batchSize">Number of messages per batch</param>
        /// <param name="bufferSize">Buffer size for reading</param>
        public async ValueTask ProcessMessageBatchAsync(
            Stream stream,
            Func<ReadOnlyMemory<byte>[], ValueTask> batchHandler,
            CancellationToken cancellationToken,
            int batchSize = 10,
            int bufferSize = 4096)
        {
            ThrowIfDisposed();

            var batch = new List<ReadOnlyMemory<byte>>(batchSize);
            var buffers = new List<IMemoryOwner<byte>>();

            try
            {
                using var owner = _memoryPool.Rent(bufferSize);
                var memory = owner.Memory;

                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(memory, cancellationToken)) > 0)
                {
                    // Create a copy for the batch since the original memory will be reused
                    var batchOwner = _memoryPool.Rent(bytesRead);
                    memory[..bytesRead].CopyTo(batchOwner.Memory);
                    
                    batch.Add(batchOwner.Memory[..bytesRead]);
                    buffers.Add(batchOwner);

                    if (batch.Count >= batchSize)
                    {
                        await batchHandler(batch.ToArray());
                        
                        // Clean up batch
                        foreach (var buffer in buffers)
                        {
                            buffer.Dispose();
                        }
                        batch.Clear();
                        buffers.Clear();
                    }
                }

                // Process remaining messages in batch
                if (batch.Count > 0)
                {
                    await batchHandler(batch.ToArray());
                }
            }
            finally
            {
                // Clean up any remaining buffers
                foreach (var buffer in buffers)
                {
                    buffer.Dispose();
                }
            }
        }

        /// <summary>
        /// Writes data to a stream using array pooling
        /// </summary>
        /// <param name="stream">The output stream</param>
        /// <param name="data">Data to write</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async ValueTask WriteToStreamAsync(
            Stream stream,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                await stream.WriteAsync(data, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to stream");
                throw;
            }
        }

        /// <summary>
        /// Copies data between streams using pooled buffers
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="destination">Destination stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="bufferSize">Buffer size for copying</param>
        public async ValueTask CopyStreamAsync(
            Stream source,
            Stream destination,
            CancellationToken cancellationToken,
            int bufferSize = 81920)
        {
            ThrowIfDisposed();

            using var owner = _memoryPool.Rent(bufferSize);
            var buffer = owner.Memory;

            try
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer[..bytesRead], cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying streams");
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MessageProcessor));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryPool?.Dispose();
                _disposed = true;
            }
        }
    }
}
