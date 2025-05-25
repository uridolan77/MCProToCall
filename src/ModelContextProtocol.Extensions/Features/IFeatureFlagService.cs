using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Features
{
    /// <summary>
    /// Service for managing feature flags and gradual rollouts
    /// </summary>
    public interface IFeatureFlagService
    {
        /// <summary>
        /// Checks if a feature flag is enabled for the given context
        /// </summary>
        /// <param name="flagName">Feature flag name</param>
        /// <param name="context">Feature context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if feature is enabled</returns>
        Task<bool> IsEnabledAsync(string flagName, FeatureContext context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a feature flag variation value
        /// </summary>
        /// <typeparam name="T">Variation type</typeparam>
        /// <param name="flagName">Feature flag name</param>
        /// <param name="defaultValue">Default value if flag not found</param>
        /// <param name="context">Feature context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Feature variation value</returns>
        Task<T> GetVariationAsync<T>(string flagName, T defaultValue, FeatureContext context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a feature flag provider
        /// </summary>
        /// <param name="provider">Feature flag provider</param>
        void RegisterFlagProvider(IFeatureFlagProvider provider);

        /// <summary>
        /// Gets all available feature flags
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Available feature flags</returns>
        Task<FeatureFlag[]> GetAllFlagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a feature flag
        /// </summary>
        /// <param name="flag">Feature flag definition</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<FeatureFlagOperationResult> CreateOrUpdateFlagAsync(FeatureFlag flag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a feature flag
        /// </summary>
        /// <param name="flagName">Feature flag name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<FeatureFlagOperationResult> DeleteFlagAsync(string flagName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets feature flag usage analytics
        /// </summary>
        /// <param name="flagName">Feature flag name</param>
        /// <param name="timeRange">Time range for analytics</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Usage analytics</returns>
        Task<FeatureFlagAnalytics> GetAnalyticsAsync(string flagName, TimeSpan? timeRange = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provider for feature flag data
    /// </summary>
    public interface IFeatureFlagProvider
    {
        /// <summary>
        /// Gets the provider name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the provider priority (higher numbers = higher priority)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Evaluates a feature flag
        /// </summary>
        /// <param name="flagName">Feature flag name</param>
        /// <param name="context">Feature context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Feature flag evaluation result</returns>
        Task<FeatureFlagEvaluation> EvaluateFlagAsync(string flagName, FeatureContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all flags from this provider
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Feature flags</returns>
        Task<FeatureFlag[]> GetFlagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if this provider can handle the specified flag
        /// </summary>
        /// <param name="flagName">Feature flag name</param>
        /// <returns>True if provider can handle the flag</returns>
        bool CanHandle(string flagName);
    }

    /// <summary>
    /// Manages gradual rollouts of features
    /// </summary>
    public interface IGradualRolloutManager
    {
        /// <summary>
        /// Gets the current rollout status for a feature
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rollout status</returns>
        Task<RolloutStatus> GetRolloutStatusAsync(string feature, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the rollout percentage for a feature
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <param name="percentage">Rollout percentage (0-100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Update result</returns>
        Task<RolloutUpdateResult> UpdateRolloutPercentageAsync(string feature, int percentage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a gradual rollout for a feature
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <param name="rolloutPlan">Rollout plan</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rollout result</returns>
        Task<RolloutResult> StartRolloutAsync(string feature, RolloutPlan rolloutPlan, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops a rollout and optionally rolls back
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <param name="rollback">Whether to rollback to previous state</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stop result</returns>
        Task<RolloutResult> StopRolloutAsync(string feature, bool rollback = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets rollout history for a feature
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Rollout history</returns>
        Task<RolloutHistoryEntry[]> GetRolloutHistoryAsync(string feature, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Context for feature flag evaluation
    /// </summary>
    public class FeatureContext
    {
        /// <summary>
        /// Gets or sets the user ID
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the session ID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets user attributes
        /// </summary>
        public Dictionary<string, object> UserAttributes { get; set; } = new();

        /// <summary>
        /// Gets or sets custom attributes
        /// </summary>
        public Dictionary<string, object> CustomAttributes { get; set; } = new();

        /// <summary>
        /// Gets or sets the request timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the client IP address
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the user agent
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the geographic location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the application version
        /// </summary>
        public string AppVersion { get; set; }
    }

    /// <summary>
    /// Feature flag definition
    /// </summary>
    public class FeatureFlag
    {
        /// <summary>
        /// Gets or sets the flag name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the flag description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets whether the flag is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the flag type
        /// </summary>
        public FeatureFlagType Type { get; set; } = FeatureFlagType.Boolean;

        /// <summary>
        /// Gets or sets the default value
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets flag variations
        /// </summary>
        public Dictionary<string, object> Variations { get; set; } = new();

        /// <summary>
        /// Gets or sets targeting rules
        /// </summary>
        public List<TargetingRule> TargetingRules { get; set; } = new();

        /// <summary>
        /// Gets or sets the rollout percentage (0-100)
        /// </summary>
        public int RolloutPercentage { get; set; } = 0;

        /// <summary>
        /// Gets or sets flag tags
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets when the flag was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when the flag was last modified
        /// </summary>
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// Gets or sets the flag owner
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets flag metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Types of feature flags
    /// </summary>
    public enum FeatureFlagType
    {
        Boolean,
        String,
        Number,
        Json
    }

    /// <summary>
    /// Targeting rule for feature flags
    /// </summary>
    public class TargetingRule
    {
        /// <summary>
        /// Gets or sets the rule ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the rule description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the rule conditions
        /// </summary>
        public List<RuleCondition> Conditions { get; set; } = new();

        /// <summary>
        /// Gets or sets the variation to serve when rule matches
        /// </summary>
        public string Variation { get; set; }

        /// <summary>
        /// Gets or sets the rule priority
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the rule is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Condition for targeting rules
    /// </summary>
    public class RuleCondition
    {
        /// <summary>
        /// Gets or sets the attribute to evaluate
        /// </summary>
        public string Attribute { get; set; }

        /// <summary>
        /// Gets or sets the operator
        /// </summary>
        public ConditionOperator Operator { get; set; }

        /// <summary>
        /// Gets or sets the values to compare against
        /// </summary>
        public object[] Values { get; set; } = Array.Empty<object>();
    }

    /// <summary>
    /// Operators for rule conditions
    /// </summary>
    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        In,
        NotIn,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Matches,
        NotMatches
    }

    /// <summary>
    /// Result of feature flag evaluation
    /// </summary>
    public class FeatureFlagEvaluation
    {
        /// <summary>
        /// Gets or sets the flag name
        /// </summary>
        public string FlagName { get; set; }

        /// <summary>
        /// Gets or sets whether the flag is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the variation value
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the variation key
        /// </summary>
        public string VariationKey { get; set; }

        /// <summary>
        /// Gets or sets the reason for this evaluation
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the matched rule ID (if any)
        /// </summary>
        public string MatchedRuleId { get; set; }

        /// <summary>
        /// Gets or sets evaluation metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when the evaluation occurred
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of feature flag operations
    /// </summary>
    public class FeatureFlagOperationResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the operation message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets operation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets the affected flag
        /// </summary>
        public FeatureFlag Flag { get; set; }
    }

    /// <summary>
    /// Analytics data for feature flags
    /// </summary>
    public class FeatureFlagAnalytics
    {
        /// <summary>
        /// Gets or sets the flag name
        /// </summary>
        public string FlagName { get; set; }

        /// <summary>
        /// Gets or sets total evaluations
        /// </summary>
        public long TotalEvaluations { get; set; }

        /// <summary>
        /// Gets or sets enabled evaluations
        /// </summary>
        public long EnabledEvaluations { get; set; }

        /// <summary>
        /// Gets or sets disabled evaluations
        /// </summary>
        public long DisabledEvaluations { get; set; }

        /// <summary>
        /// Gets or sets variation statistics
        /// </summary>
        public Dictionary<string, long> VariationStats { get; set; } = new();

        /// <summary>
        /// Gets or sets unique users who saw this flag
        /// </summary>
        public long UniqueUsers { get; set; }

        /// <summary>
        /// Gets or sets the time range for these analytics
        /// </summary>
        public TimeSpan TimeRange { get; set; }

        /// <summary>
        /// Gets or sets when analytics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Rollout status information
    /// </summary>
    public class RolloutStatus
    {
        /// <summary>
        /// Gets or sets the feature name
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Gets or sets the current rollout percentage
        /// </summary>
        public int CurrentPercentage { get; set; }

        /// <summary>
        /// Gets or sets the target percentage
        /// </summary>
        public int TargetPercentage { get; set; }

        /// <summary>
        /// Gets or sets the rollout state
        /// </summary>
        public RolloutState State { get; set; }

        /// <summary>
        /// Gets or sets when the rollout started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the rollout will complete
        /// </summary>
        public DateTime? EstimatedCompletionAt { get; set; }

        /// <summary>
        /// Gets or sets rollout metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Rollout states
    /// </summary>
    public enum RolloutState
    {
        NotStarted,
        InProgress,
        Paused,
        Completed,
        Failed,
        RolledBack
    }

    /// <summary>
    /// Plan for gradual rollout
    /// </summary>
    public class RolloutPlan
    {
        /// <summary>
        /// Gets or sets rollout stages
        /// </summary>
        public List<RolloutStage> Stages { get; set; } = new();

        /// <summary>
        /// Gets or sets success criteria
        /// </summary>
        public List<SuccessCriterion> SuccessCriteria { get; set; } = new();

        /// <summary>
        /// Gets or sets rollback criteria
        /// </summary>
        public List<RollbackCriterion> RollbackCriteria { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to auto-advance stages
        /// </summary>
        public bool AutoAdvance { get; set; } = true;

        /// <summary>
        /// Gets or sets the monitoring window between stages
        /// </summary>
        public TimeSpan MonitoringWindow { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Stage in a rollout plan
    /// </summary>
    public class RolloutStage
    {
        /// <summary>
        /// Gets or sets the stage name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the target percentage for this stage
        /// </summary>
        public int TargetPercentage { get; set; }

        /// <summary>
        /// Gets or sets the duration to hold this stage
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets stage-specific criteria
        /// </summary>
        public List<SuccessCriterion> Criteria { get; set; } = new();
    }

    /// <summary>
    /// Success criterion for rollouts
    /// </summary>
    public class SuccessCriterion
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string Metric { get; set; }

        /// <summary>
        /// Gets or sets the threshold value
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets or sets the comparison operator
        /// </summary>
        public ComparisonOperator Operator { get; set; }
    }

    /// <summary>
    /// Rollback criterion for rollouts
    /// </summary>
    public class RollbackCriterion
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string Metric { get; set; }

        /// <summary>
        /// Gets or sets the threshold value
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets or sets the comparison operator
        /// </summary>
        public ComparisonOperator Operator { get; set; }
    }

    /// <summary>
    /// Comparison operators for criteria
    /// </summary>
    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equals,
        NotEquals
    }

    /// <summary>
    /// Result of rollout operations
    /// </summary>
    public class RolloutResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the operation message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the new rollout status
        /// </summary>
        public RolloutStatus Status { get; set; }

        /// <summary>
        /// Gets or sets operation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Result of rollout updates
    /// </summary>
    public class RolloutUpdateResult
    {
        /// <summary>
        /// Gets or sets whether the update was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the previous percentage
        /// </summary>
        public int PreviousPercentage { get; set; }

        /// <summary>
        /// Gets or sets the new percentage
        /// </summary>
        public int NewPercentage { get; set; }

        /// <summary>
        /// Gets or sets update errors
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Rollout history entry
    /// </summary>
    public class RolloutHistoryEntry
    {
        /// <summary>
        /// Gets or sets when the change occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the action taken
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the previous percentage
        /// </summary>
        public int? PreviousPercentage { get; set; }

        /// <summary>
        /// Gets or sets the new percentage
        /// </summary>
        public int? NewPercentage { get; set; }

        /// <summary>
        /// Gets or sets who made the change
        /// </summary>
        public string ChangedBy { get; set; }

        /// <summary>
        /// Gets or sets the reason for the change
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
