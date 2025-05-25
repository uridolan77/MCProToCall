using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Caching
{
    /// <summary>
    /// Cache invalidation service for coordinated cache management
    /// </summary>
    public class CacheInvalidationService : ICacheInvalidationService, IDisposable
    {
        private readonly ILogger<CacheInvalidationService> _logger;
        private readonly CacheInvalidationOptions _options;
        private readonly ConcurrentDictionary<string, Func<string, Task>> _subscribers = new();
        private readonly Channel<InvalidationMessage> _invalidationChannel;
        private readonly ChannelWriter<InvalidationMessage> _writer;
        private readonly ChannelReader<InvalidationMessage> _reader;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _processingTask;

        public CacheInvalidationService(
            ILogger<CacheInvalidationService> logger,
            IOptions<CacheInvalidationOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new CacheInvalidationOptions();

            // Create channel for invalidation messages
            var channelOptions = new BoundedChannelOptions(_options.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _invalidationChannel = Channel.CreateBounded<InvalidationMessage>(channelOptions);
            _writer = _invalidationChannel.Writer;
            _reader = _invalidationChannel.Reader;

            // Start background processing
            _processingTask = Task.Run(ProcessInvalidationMessages);
        }

        public async Task InvalidateAsync(string pattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            var message = new InvalidationMessage
            {
                Type = InvalidationType.Pattern,
                Pattern = pattern,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                await _writer.WriteAsync(message, cancellationToken);
                _logger.LogDebug("Queued cache invalidation for pattern: {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue cache invalidation for pattern: {Pattern}", pattern);
                throw;
            }
        }

        public async Task InvalidateByTagsAsync(string[] tags, CancellationToken cancellationToken = default)
        {
            if (tags == null || tags.Length == 0)
                throw new ArgumentException("Tags cannot be null or empty", nameof(tags));

            var message = new InvalidationMessage
            {
                Type = InvalidationType.Tags,
                Tags = tags,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                await _writer.WriteAsync(message, cancellationToken);
                _logger.LogDebug("Queued cache invalidation for tags: {Tags}", string.Join(", ", tags));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue cache invalidation for tags: {Tags}", string.Join(", ", tags));
                throw;
            }
        }

        public Task SubscribeToInvalidationEventsAsync(Func<string, Task> onInvalidation, CancellationToken cancellationToken = default)
        {
            if (onInvalidation == null)
                throw new ArgumentNullException(nameof(onInvalidation));

            var subscriberId = Guid.NewGuid().ToString();
            _subscribers.TryAdd(subscriberId, onInvalidation);

            _logger.LogDebug("Added cache invalidation subscriber: {SubscriberId}", subscriberId);

            // Return a task that completes when cancellation is requested
            return Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Remove subscriber when cancelled
                    _subscribers.TryRemove(subscriberId, out _);
                    _logger.LogDebug("Removed cache invalidation subscriber: {SubscriberId}", subscriberId);
                }
            }, cancellationToken);
        }

        private async Task ProcessInvalidationMessages()
        {
            try
            {
                await foreach (var message in _reader.ReadAllAsync(_cancellationTokenSource.Token))
                {
                    await ProcessInvalidationMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Cache invalidation processing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cache invalidation processing");
            }
        }

        private async Task ProcessInvalidationMessage(InvalidationMessage message)
        {
            try
            {
                var tasks = new List<Task>();

                foreach (var subscriber in _subscribers.Values)
                {
                    try
                    {
                        var notificationKey = message.Type switch
                        {
                            InvalidationType.Pattern => message.Pattern!,
                            InvalidationType.Tags => string.Join(",", message.Tags!),
                            _ => throw new InvalidOperationException($"Unknown invalidation type: {message.Type}")
                        };

                        tasks.Add(subscriber(notificationKey));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error notifying cache invalidation subscriber");
                    }
                }

                // Wait for all notifications to complete with timeout
                var timeoutTask = Task.Delay(_options.NotificationTimeout);
                var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Cache invalidation notification timeout exceeded");
                }

                _logger.LogDebug("Processed cache invalidation message: {Type}", message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cache invalidation message: {Type}", message.Type);
            }
        }

        public void Dispose()
        {
            try
            {
                _writer.Complete();
                _cancellationTokenSource.Cancel();
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during cache invalidation service disposal");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _processingTask?.Dispose();
            }
        }
    }

    /// <summary>
    /// Cache invalidation message
    /// </summary>
    internal class InvalidationMessage
    {
        public InvalidationType Type { get; set; }
        public string? Pattern { get; set; }
        public string[]? Tags { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Types of cache invalidation
    /// </summary>
    internal enum InvalidationType
    {
        Pattern,
        Tags
    }

    /// <summary>
    /// Configuration options for cache invalidation service
    /// </summary>
    public class CacheInvalidationOptions
    {
        /// <summary>
        /// Maximum number of invalidation messages to queue
        /// </summary>
        public int MaxQueueSize { get; set; } = 1000;

        /// <summary>
        /// Timeout for notifying subscribers
        /// </summary>
        public TimeSpan NotificationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to enable pattern matching for invalidation
        /// </summary>
        public bool EnablePatternMatching { get; set; } = true;
    }

    /// <summary>
    /// Pattern matcher for cache keys
    /// </summary>
    public static class CachePatternMatcher
    {
        /// <summary>
        /// Checks if a key matches a pattern (supports * wildcards)
        /// </summary>
        public static bool Matches(string key, string pattern)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(pattern))
                return false;

            if (pattern == "*")
                return true;

            if (!pattern.Contains("*"))
                return string.Equals(key, pattern, StringComparison.OrdinalIgnoreCase);

            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(key, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
