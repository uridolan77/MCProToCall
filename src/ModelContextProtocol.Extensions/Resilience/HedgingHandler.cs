using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;

namespace ModelContextProtocol.Extensions.Resilience
{
    /// <summary>
    /// HTTP handler that implements request hedging for improved reliability
    /// </summary>
    public class HedgingHandler : DelegatingHandler
    {
        private readonly ILogger<HedgingHandler> _logger;
        private readonly HedgingOptions _options;
        private long _totalRequests;
        private long _hedgedRequests;
        private long _primaryWins;
        private long _hedgedWins;

        public HedgingHandler(
            ILogger<HedgingHandler> logger,
            IOptions<TlsOptions> tlsOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = tlsOptions?.Value?.HedgingOptions ?? new HedgingOptions();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!ShouldHedgeRequest(request))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            Interlocked.Increment(ref _totalRequests);
            Interlocked.Increment(ref _hedgedRequests);

            _logger.LogDebug("Starting hedged request for {Method} {Uri}", request.Method, request.RequestUri);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Start primary request
                var primaryRequest = CloneRequest(request);
                var primaryTask = base.SendAsync(primaryRequest, cts.Token);

                // Wait for hedging delay
                var delayTask = Task.Delay(_options.HedgingDelayMs, cts.Token);
                var firstCompleted = await Task.WhenAny(primaryTask, delayTask);

                // If primary completed successfully within delay, return it
                if (firstCompleted == primaryTask)
                {
                    if (primaryTask.IsCompletedSuccessfully && IsSuccessfulResponse(primaryTask.Result))
                    {
                        Interlocked.Increment(ref _primaryWins);
                        _logger.LogDebug("Primary request completed successfully in {Duration}ms", stopwatch.ElapsedMilliseconds);
                        return primaryTask.Result;
                    }
                    else if (primaryTask.IsFaulted)
                    {
                        _logger.LogDebug("Primary request failed, starting hedged request: {Exception}", primaryTask.Exception?.GetBaseException()?.Message);
                    }
                }

                // Start hedged requests
                var hedgedTasks = new Task<HttpResponseMessage>[_options.MaxHedgedRequests];
                for (int i = 0; i < _options.MaxHedgedRequests; i++)
                {
                    var hedgedRequest = CloneRequest(request);
                    hedgedTasks[i] = base.SendAsync(hedgedRequest, cts.Token);
                    _logger.LogTrace("Started hedged request {Index}", i + 1);
                }

                // Wait for any request to complete successfully
                var allTasks = new Task<HttpResponseMessage>[hedgedTasks.Length + 1];
                allTasks[0] = primaryTask;
                Array.Copy(hedgedTasks, 0, allTasks, 1, hedgedTasks.Length);

                while (allTasks.Length > 0)
                {
                    var completedTask = await Task.WhenAny(allTasks);
                    
                    if (completedTask.IsCompletedSuccessfully && IsSuccessfulResponse(completedTask.Result))
                    {
                        cts.Cancel(); // Cancel remaining requests
                        
                        if (completedTask == primaryTask)
                        {
                            Interlocked.Increment(ref _primaryWins);
                            _logger.LogDebug("Primary request won in {Duration}ms", stopwatch.ElapsedMilliseconds);
                        }
                        else
                        {
                            Interlocked.Increment(ref _hedgedWins);
                            _logger.LogDebug("Hedged request won in {Duration}ms", stopwatch.ElapsedMilliseconds);
                        }
                        
                        return completedTask.Result;
                    }
                    else if (completedTask.IsFaulted)
                    {
                        _logger.LogTrace("Request failed: {Exception}", completedTask.Exception?.GetBaseException()?.Message);
                    }

                    // Remove completed task and continue
                    var remainingTasks = new Task<HttpResponseMessage>[allTasks.Length - 1];
                    int index = 0;
                    for (int i = 0; i < allTasks.Length; i++)
                    {
                        if (allTasks[i] != completedTask)
                        {
                            remainingTasks[index++] = allTasks[i];
                        }
                    }
                    allTasks = remainingTasks;
                }

                // If we get here, all requests failed
                _logger.LogWarning("All hedged requests failed for {Method} {Uri}", request.Method, request.RequestUri);
                
                // Return the primary task result (which should contain the exception)
                return await primaryTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Hedged request cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during hedged request execution");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                cts.Cancel(); // Ensure all remaining requests are cancelled
            }
        }

        private bool ShouldHedgeRequest(HttpRequestMessage request)
        {
            if (!_options.EnableHedging)
                return false;

            // Only hedge safe HTTP methods
            if (request.Method != HttpMethod.Get && 
                request.Method != HttpMethod.Head && 
                request.Method != HttpMethod.Options)
            {
                return false;
            }

            // Check if the operation is in the hedged operations list
            var path = request.RequestUri?.AbsolutePath;
            if (path != null && _options.HedgedOperations?.Count > 0)
            {
                foreach (var operation in _options.HedgedOperations)
                {
                    if (path.Contains(operation, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }

            return true; // Hedge by default if no specific operations are configured
        }

        private static bool IsSuccessfulResponse(HttpResponseMessage response)
        {
            return response?.IsSuccessStatusCode == true;
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri)
            {
                Version = original.Version,
                Content = original.Content
            };

            // Copy headers
            foreach (var header in original.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Copy properties
            foreach (var property in original.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object>(property.Key), property.Value);
            }

            return clone;
        }

        /// <summary>
        /// Gets hedging metrics
        /// </summary>
        public HedgingMetrics GetMetrics()
        {
            return new HedgingMetrics
            {
                TotalRequests = _totalRequests,
                HedgedRequests = _hedgedRequests,
                PrimaryWins = _primaryWins,
                HedgedWins = _hedgedWins,
                HedgingRate = _totalRequests > 0 ? (double)_hedgedRequests / _totalRequests : 0,
                HedgedWinRate = _hedgedRequests > 0 ? (double)_hedgedWins / _hedgedRequests : 0
            };
        }
    }

    /// <summary>
    /// Hedging metrics for monitoring
    /// </summary>
    public class HedgingMetrics
    {
        /// <summary>
        /// Total number of requests processed
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Number of requests that were hedged
        /// </summary>
        public long HedgedRequests { get; set; }

        /// <summary>
        /// Number of times the primary request won
        /// </summary>
        public long PrimaryWins { get; set; }

        /// <summary>
        /// Number of times a hedged request won
        /// </summary>
        public long HedgedWins { get; set; }

        /// <summary>
        /// Percentage of requests that were hedged
        /// </summary>
        public double HedgingRate { get; set; }

        /// <summary>
        /// Percentage of hedged requests where the hedged request won
        /// </summary>
        public double HedgedWinRate { get; set; }

        public override string ToString()
        {
            return $"Hedging Metrics: Total={TotalRequests}, Hedged={HedgedRequests} ({HedgingRate:P1}), " +
                   $"Primary Wins={PrimaryWins}, Hedged Wins={HedgedWins} ({HedgedWinRate:P1})";
        }
    }
}
