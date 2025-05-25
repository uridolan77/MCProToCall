using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// Adaptive resource pool that automatically adjusts size based on demand and performance metrics
    /// </summary>
    /// <typeparam name="T">Type of resource to pool</typeparam>
    public interface IAdaptiveResourcePool<T> : IResourcePool<T> where T : class
    {
        /// <summary>
        /// Acquires a resource with specified priority
        /// </summary>
        /// <param name="priority">Resource priority</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Acquired resource</returns>
        Task<PooledResource<T>> AcquireAsync(ResourcePriority priority, CancellationToken cancellationToken = default);

        /// <summary>
        /// Optimizes pool size based on current usage patterns
        /// </summary>
        /// <returns>Optimization report</returns>
        Task<PoolOptimizationReport> OptimizePoolSizeAsync();

        /// <summary>
        /// Enables predictive scaling using a machine learning model
        /// </summary>
        /// <param name="model">Predictive model for scaling decisions</param>
        void EnablePredictiveScaling(IPredictiveModel model);

        /// <summary>
        /// Gets current pool metrics
        /// </summary>
        /// <returns>Pool metrics</returns>
        Task<ResourcePoolMetrics> GetMetricsAsync();

        /// <summary>
        /// Sets pool scaling policies
        /// </summary>
        /// <param name="policies">Scaling policies</param>
        Task SetScalingPoliciesAsync(ResourceScalingPolicies policies);

        /// <summary>
        /// Monitors pool health and performance
        /// </summary>
        /// <param name="onHealthChanged">Callback when health status changes</param>
        Task StartHealthMonitoringAsync(Func<PoolHealthStatus, Task> onHealthChanged);
    }

    /// <summary>
    /// Basic resource pool interface
    /// </summary>
    /// <typeparam name="T">Type of resource to pool</typeparam>
    public interface IResourcePool<T> : IDisposable where T : class
    {
        /// <summary>
        /// Acquires a resource from the pool
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Acquired resource</returns>
        Task<PooledResource<T>> AcquireAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a resource to the pool
        /// </summary>
        /// <param name="resource">Resource to return</param>
        Task ReturnAsync(PooledResource<T> resource);

        /// <summary>
        /// Gets the current pool size
        /// </summary>
        int CurrentSize { get; }

        /// <summary>
        /// Gets the number of available resources
        /// </summary>
        int AvailableCount { get; }

        /// <summary>
        /// Gets the number of resources in use
        /// </summary>
        int InUseCount { get; }
    }

    /// <summary>
    /// Manages resource quotas and limits
    /// </summary>
    public interface IResourceQuotaManager
    {
        /// <summary>
        /// Checks if a quota allows the requested resource amount
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="clientId">Client requesting the resource</param>
        /// <param name="requestedAmount">Amount of resource requested</param>
        /// <returns>True if quota allows the request</returns>
        Task<bool> CheckQuotaAsync(string resourceType, string clientId, int requestedAmount);

        /// <summary>
        /// Reserves resources against a quota
        /// </summary>
        /// <param name="resourceType">Type of resource</param>
        /// <param name="clientId">Client ID</param>
        /// <param name="amount">Amount to reserve</param>
        /// <returns>Reservation token</returns>
        Task<ResourceReservation> ReserveResourcesAsync(string resourceType, string clientId, int amount);

        /// <summary>
        /// Releases reserved resources
        /// </summary>
        /// <param name="reservation">Reservation to release</param>
        Task ReleaseReservationAsync(ResourceReservation reservation);

        /// <summary>
        /// Gets usage report for a client
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <returns>Usage report</returns>
        Task<QuotaUsageReport> GetUsageReportAsync(string clientId);

        /// <summary>
        /// Registers a quota policy for a resource type
        /// </summary>
        /// <param name="resourceType">Resource type</param>
        /// <param name="policy">Quota policy</param>
        void RegisterQuotaPolicy(string resourceType, IQuotaPolicy policy);

        /// <summary>
        /// Gets all active quotas
        /// </summary>
        /// <returns>Active quotas</returns>
        Task<ResourceQuota[]> GetActiveQuotasAsync();
    }

    /// <summary>
    /// Predictive model for resource scaling decisions
    /// </summary>
    public interface IPredictiveModel
    {
        /// <summary>
        /// Predicts resource demand for a future time window
        /// </summary>
        /// <param name="timeWindow">Time window to predict</param>
        /// <param name="historicalData">Historical usage data</param>
        /// <returns>Predicted demand</returns>
        Task<ResourceDemandPrediction> PredictDemandAsync(TimeSpan timeWindow, ResourceUsageData[] historicalData);

        /// <summary>
        /// Updates the model with new usage data
        /// </summary>
        /// <param name="usageData">New usage data</param>
        Task UpdateModelAsync(ResourceUsageData usageData);

        /// <summary>
        /// Gets the model's confidence in its predictions
        /// </summary>
        /// <returns>Confidence score (0-1)</returns>
        Task<double> GetConfidenceScoreAsync();
    }

    /// <summary>
    /// Quota policy for resource management
    /// </summary>
    public interface IQuotaPolicy
    {
        /// <summary>
        /// Gets the quota limit for a client
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <param name="context">Request context</param>
        /// <returns>Quota limit</returns>
        Task<ResourceQuota> GetQuotaAsync(string clientId, QuotaContext context);

        /// <summary>
        /// Checks if a request violates the policy
        /// </summary>
        /// <param name="clientId">Client ID</param>
        /// <param name="requestedAmount">Requested amount</param>
        /// <param name="currentUsage">Current usage</param>
        /// <returns>True if request is allowed</returns>
        Task<bool> IsRequestAllowedAsync(string clientId, int requestedAmount, int currentUsage);

        /// <summary>
        /// Gets the policy name
        /// </summary>
        string PolicyName { get; }
    }

    /// <summary>
    /// Represents a pooled resource
    /// </summary>
    /// <typeparam name="T">Type of resource</typeparam>
    public class PooledResource<T> : IDisposable where T : class
    {
        /// <summary>
        /// Gets the actual resource
        /// </summary>
        public T Resource { get; }

        /// <summary>
        /// Gets the resource ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets when the resource was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets when the resource was last used
        /// </summary>
        public DateTime LastUsedAt { get; set; }

        /// <summary>
        /// Gets the number of times this resource has been used
        /// </summary>
        public int UseCount { get; set; }

        /// <summary>
        /// Gets whether the resource is healthy
        /// </summary>
        public bool IsHealthy { get; set; } = true;

        /// <summary>
        /// Gets the resource priority
        /// </summary>
        public ResourcePriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the return action
        /// </summary>
        public Func<PooledResource<T>, Task> ReturnAction { get; set; }

        /// <summary>
        /// Initializes a new pooled resource
        /// </summary>
        public PooledResource(T resource, string id = null)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Id = id ?? Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            LastUsedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns the resource to the pool
        /// </summary>
        public async void Dispose()
        {
            if (ReturnAction != null)
            {
                await ReturnAction(this);
            }
        }
    }

    /// <summary>
    /// Resource priority levels
    /// </summary>
    public enum ResourcePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Pool optimization report
    /// </summary>
    public class PoolOptimizationReport
    {
        /// <summary>
        /// Gets or sets the current pool size
        /// </summary>
        public int CurrentPoolSize { get; set; }

        /// <summary>
        /// Gets or sets the recommended pool size
        /// </summary>
        public int RecommendedPoolSize { get; set; }

        /// <summary>
        /// Gets or sets optimization recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>
        /// Gets or sets the efficiency score (0-1)
        /// </summary>
        public double EfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets performance metrics
        /// </summary>
        public Dictionary<string, double> Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets when the optimization was performed
        /// </summary>
        public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resource pool metrics
    /// </summary>
    public class ResourcePoolMetrics
    {
        /// <summary>
        /// Gets or sets the total pool size
        /// </summary>
        public int TotalSize { get; set; }

        /// <summary>
        /// Gets or sets available resources
        /// </summary>
        public int Available { get; set; }

        /// <summary>
        /// Gets or sets resources in use
        /// </summary>
        public int InUse { get; set; }

        /// <summary>
        /// Gets or sets the utilization rate (0-1)
        /// </summary>
        public double UtilizationRate { get; set; }

        /// <summary>
        /// Gets or sets average wait time for resource acquisition
        /// </summary>
        public TimeSpan AverageWaitTime { get; set; }

        /// <summary>
        /// Gets or sets average resource lifetime
        /// </summary>
        public TimeSpan AverageResourceLifetime { get; set; }

        /// <summary>
        /// Gets or sets the number of resource acquisitions
        /// </summary>
        public long TotalAcquisitions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed acquisitions
        /// </summary>
        public long FailedAcquisitions { get; set; }

        /// <summary>
        /// Gets or sets when metrics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resource scaling policies
    /// </summary>
    public class ResourceScalingPolicies
    {
        /// <summary>
        /// Gets or sets the minimum pool size
        /// </summary>
        public int MinPoolSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum pool size
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the target utilization rate (0-1)
        /// </summary>
        public double TargetUtilization { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets the scale-up threshold
        /// </summary>
        public double ScaleUpThreshold { get; set; } = 0.9;

        /// <summary>
        /// Gets or sets the scale-down threshold
        /// </summary>
        public double ScaleDownThreshold { get; set; } = 0.3;

        /// <summary>
        /// Gets or sets the scale-up increment
        /// </summary>
        public int ScaleUpIncrement { get; set; } = 2;

        /// <summary>
        /// Gets or sets the scale-down decrement
        /// </summary>
        public int ScaleDownDecrement { get; set; } = 1;

        /// <summary>
        /// Gets or sets the cooldown period between scaling operations
        /// </summary>
        public TimeSpan ScalingCooldown { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Pool health status
    /// </summary>
    public class PoolHealthStatus
    {
        /// <summary>
        /// Gets or sets the overall health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the health score (0-1)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Gets or sets health issues
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /// <summary>
        /// Gets or sets health metrics
        /// </summary>
        public Dictionary<string, double> Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets when health was last checked
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Health status enumeration
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Unhealthy
    }

    /// <summary>
    /// Resource demand prediction
    /// </summary>
    public class ResourceDemandPrediction
    {
        /// <summary>
        /// Gets or sets the predicted demand
        /// </summary>
        public int PredictedDemand { get; set; }

        /// <summary>
        /// Gets or sets the confidence level (0-1)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the prediction time window
        /// </summary>
        public TimeSpan TimeWindow { get; set; }

        /// <summary>
        /// Gets or sets when the prediction was made
        /// </summary>
        public DateTime PredictedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets additional prediction metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Resource usage data for training predictive models
    /// </summary>
    public class ResourceUsageData
    {
        /// <summary>
        /// Gets or sets when the usage was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the resource demand at this time
        /// </summary>
        public int Demand { get; set; }

        /// <summary>
        /// Gets or sets the available resources
        /// </summary>
        public int Available { get; set; }

        /// <summary>
        /// Gets or sets the utilization rate
        /// </summary>
        public double Utilization { get; set; }

        /// <summary>
        /// Gets or sets contextual factors
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Resource reservation
    /// </summary>
    public class ResourceReservation
    {
        /// <summary>
        /// Gets or sets the reservation ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the reserved amount
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Gets or sets when the reservation was made
        /// </summary>
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when the reservation expires
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Quota usage report
    /// </summary>
    public class QuotaUsageReport
    {
        /// <summary>
        /// Gets or sets the client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets usage by resource type
        /// </summary>
        public Dictionary<string, ResourceUsage> Usage { get; set; } = new();

        /// <summary>
        /// Gets or sets when the report was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resource usage information
    /// </summary>
    public class ResourceUsage
    {
        /// <summary>
        /// Gets or sets the current usage
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Gets or sets the quota limit
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the usage percentage
        /// </summary>
        public double Percentage => Limit > 0 ? (double)Current / Limit : 0;
    }

    /// <summary>
    /// Resource quota definition
    /// </summary>
    public class ResourceQuota
    {
        /// <summary>
        /// Gets or sets the resource type
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// Gets or sets the quota limit
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the time window for the quota
        /// </summary>
        public TimeSpan? TimeWindow { get; set; }

        /// <summary>
        /// Gets or sets quota metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Context for quota requests
    /// </summary>
    public class QuotaContext
    {
        /// <summary>
        /// Gets or sets the request timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets request properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
