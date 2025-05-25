using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Streaming
{
    /// <summary>
    /// Processes streams of data through configurable pipelines
    /// </summary>
    public interface IStreamProcessor
    {
        /// <summary>
        /// Processes a stream through a pipeline
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="input">Input stream</param>
        /// <param name="pipeline">Processing pipeline</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed output stream</returns>
        Task<IAsyncEnumerable<TOutput>> ProcessStreamAsync<TInput, TOutput>(
            IAsyncEnumerable<TInput> input,
            IStreamProcessingPipeline<TInput, TOutput> pipeline,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a stream processing pipeline
        /// </summary>
        /// <param name="name">Pipeline name</param>
        /// <param name="pipeline">Pipeline implementation</param>
        void RegisterPipeline<TInput, TOutput>(string name, IStreamProcessingPipeline<TInput, TOutput> pipeline);

        /// <summary>
        /// Gets available pipelines
        /// </summary>
        /// <returns>Available pipeline names</returns>
        Task<string[]> GetAvailablePipelinesAsync();

        /// <summary>
        /// Gets stream processing metrics
        /// </summary>
        /// <returns>Processing metrics</returns>
        Task<StreamProcessingMetrics> GetMetricsAsync();
    }

    /// <summary>
    /// Pipeline for processing streams of data
    /// </summary>
    /// <typeparam name="TInput">Input type</typeparam>
    /// <typeparam name="TOutput">Output type</typeparam>
    public interface IStreamProcessingPipeline<TInput, TOutput>
    {
        /// <summary>
        /// Gets the pipeline name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Processes the input stream
        /// </summary>
        /// <param name="input">Input stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Output stream</returns>
        IAsyncEnumerable<TOutput> ProcessAsync(IAsyncEnumerable<TInput> input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets pipeline configuration
        /// </summary>
        /// <returns>Pipeline configuration</returns>
        StreamPipelineConfiguration GetConfiguration();
    }

    /// <summary>
    /// Aggregates real-time data streams
    /// </summary>
    public interface IRealTimeDataAggregator
    {
        /// <summary>
        /// Aggregates data over a time window
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TAggregated">Aggregated output type</typeparam>
        /// <param name="stream">Input stream</param>
        /// <param name="window">Time window for aggregation</param>
        /// <param name="aggregator">Aggregation function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated results</returns>
        IAsyncEnumerable<TAggregated> AggregateAsync<T, TAggregated>(
            IAsyncEnumerable<T> stream,
            TimeSpan window,
            Func<IEnumerable<T>, TAggregated> aggregator,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Aggregates data over a sliding window
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TAggregated">Aggregated output type</typeparam>
        /// <param name="stream">Input stream</param>
        /// <param name="windowSize">Window size</param>
        /// <param name="slideInterval">Slide interval</param>
        /// <param name="aggregator">Aggregation function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated results</returns>
        IAsyncEnumerable<TAggregated> AggregateSlidingWindowAsync<T, TAggregated>(
            IAsyncEnumerable<T> stream,
            TimeSpan windowSize,
            TimeSpan slideInterval,
            Func<IEnumerable<T>, TAggregated> aggregator,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Aggregates data by count
        /// </summary>
        /// <typeparam name="T">Input type</typeparam>
        /// <typeparam name="TAggregated">Aggregated output type</typeparam>
        /// <param name="stream">Input stream</param>
        /// <param name="batchSize">Batch size</param>
        /// <param name="aggregator">Aggregation function</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated results</returns>
        IAsyncEnumerable<TAggregated> AggregateByCountAsync<T, TAggregated>(
            IAsyncEnumerable<T> stream,
            int batchSize,
            Func<IEnumerable<T>, TAggregated> aggregator,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Manages event sourcing operations
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Appends events to a stream
        /// </summary>
        /// <param name="streamId">Stream identifier</param>
        /// <param name="events">Events to append</param>
        /// <param name="expectedVersion">Expected stream version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Append result</returns>
        Task<EventAppendResult> AppendEventsAsync(string streamId, IEnumerable<DomainEvent> events, long? expectedVersion = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads events from a stream
        /// </summary>
        /// <param name="streamId">Stream identifier</param>
        /// <param name="fromVersion">Starting version</param>
        /// <param name="maxCount">Maximum number of events to read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of events</returns>
        IAsyncEnumerable<DomainEvent> ReadStreamAsync(string streamId, long fromVersion = 0, int? maxCount = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a snapshot for a stream
        /// </summary>
        /// <typeparam name="T">Snapshot type</typeparam>
        /// <param name="streamId">Stream identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Snapshot</returns>
        Task<Snapshot<T>> GetSnapshotAsync<T>(string streamId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a snapshot for a stream
        /// </summary>
        /// <typeparam name="T">Snapshot type</typeparam>
        /// <param name="streamId">Stream identifier</param>
        /// <param name="snapshot">Snapshot data</param>
        /// <param name="version">Stream version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Save result</returns>
        Task<SnapshotSaveResult> SaveSnapshotAsync<T>(string streamId, T snapshot, long version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to all events
        /// </summary>
        /// <param name="fromPosition">Starting position</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of all events</returns>
        IAsyncEnumerable<DomainEvent> SubscribeToAllAsync(long? fromPosition = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to events from a specific stream
        /// </summary>
        /// <param name="streamId">Stream identifier</param>
        /// <param name="fromVersion">Starting version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of events</returns>
        IAsyncEnumerable<DomainEvent> SubscribeToStreamAsync(string streamId, long? fromVersion = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Configuration for stream processing pipelines
    /// </summary>
    public class StreamPipelineConfiguration
    {
        /// <summary>
        /// Gets or sets the pipeline name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the buffer size
        /// </summary>
        public int BufferSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum parallelism
        /// </summary>
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the processing timeout
        /// </summary>
        public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to preserve order
        /// </summary>
        public bool PreserveOrder { get; set; } = true;

        /// <summary>
        /// Gets or sets error handling strategy
        /// </summary>
        public ErrorHandlingStrategy ErrorHandling { get; set; } = ErrorHandlingStrategy.Continue;

        /// <summary>
        /// Gets or sets pipeline metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Error handling strategies for stream processing
    /// </summary>
    public enum ErrorHandlingStrategy
    {
        /// <summary>
        /// Stop processing on error
        /// </summary>
        Stop,

        /// <summary>
        /// Continue processing, skip failed items
        /// </summary>
        Continue,

        /// <summary>
        /// Retry failed items
        /// </summary>
        Retry,

        /// <summary>
        /// Send failed items to dead letter queue
        /// </summary>
        DeadLetter
    }

    /// <summary>
    /// Metrics for stream processing
    /// </summary>
    public class StreamProcessingMetrics
    {
        /// <summary>
        /// Gets or sets total items processed
        /// </summary>
        public long TotalItemsProcessed { get; set; }

        /// <summary>
        /// Gets or sets successful items
        /// </summary>
        public long SuccessfulItems { get; set; }

        /// <summary>
        /// Gets or sets failed items
        /// </summary>
        public long FailedItems { get; set; }

        /// <summary>
        /// Gets or sets average processing time per item
        /// </summary>
        public TimeSpan AverageProcessingTime { get; set; }

        /// <summary>
        /// Gets or sets current throughput (items per second)
        /// </summary>
        public double ThroughputPerSecond { get; set; }

        /// <summary>
        /// Gets or sets active pipelines
        /// </summary>
        public int ActivePipelines { get; set; }

        /// <summary>
        /// Gets or sets pipeline-specific metrics
        /// </summary>
        public Dictionary<string, PipelineMetrics> PipelineMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets when metrics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Metrics for a specific pipeline
    /// </summary>
    public class PipelineMetrics
    {
        /// <summary>
        /// Gets or sets the pipeline name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets items processed by this pipeline
        /// </summary>
        public long ItemsProcessed { get; set; }

        /// <summary>
        /// Gets or sets average processing time
        /// </summary>
        public TimeSpan AverageProcessingTime { get; set; }

        /// <summary>
        /// Gets or sets error rate
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets last activity time
        /// </summary>
        public DateTime? LastActivity { get; set; }
    }

    /// <summary>
    /// Domain event for event sourcing
    /// </summary>
    public class DomainEvent
    {
        /// <summary>
        /// Gets or sets the event ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the stream ID
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// Gets or sets the event type
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the event data
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets event metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the event version
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// Gets or sets when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the correlation ID
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the causation ID
        /// </summary>
        public string CausationId { get; set; }
    }

    /// <summary>
    /// Result of appending events to a stream
    /// </summary>
    public class EventAppendResult
    {
        /// <summary>
        /// Gets or sets whether the append was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the new stream version
        /// </summary>
        public long NewVersion { get; set; }

        /// <summary>
        /// Gets or sets the number of events appended
        /// </summary>
        public int EventsAppended { get; set; }

        /// <summary>
        /// Gets or sets any errors that occurred
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Snapshot of stream state
    /// </summary>
    /// <typeparam name="T">Snapshot data type</typeparam>
    public class Snapshot<T>
    {
        /// <summary>
        /// Gets or sets the stream ID
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// Gets or sets the snapshot data
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Gets or sets the stream version at snapshot time
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// Gets or sets when the snapshot was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets snapshot metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result of saving a snapshot
    /// </summary>
    public class SnapshotSaveResult
    {
        /// <summary>
        /// Gets or sets whether the save was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the snapshot version
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// Gets or sets any errors that occurred
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}
