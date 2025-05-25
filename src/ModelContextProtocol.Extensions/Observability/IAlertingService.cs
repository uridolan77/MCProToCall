using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Interface for alerting service
    /// </summary>
    public interface IAlertingService
    {
        /// <summary>
        /// Triggers an alert
        /// </summary>
        Task TriggerAlertAsync(string alertType, object data, AlertSeverity severity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an alert should be triggered based on metric value
        /// </summary>
        Task<bool> ShouldTriggerAlertAsync(string metricName, double value, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers an alert rule
        /// </summary>
        Task RegisterAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an alert rule
        /// </summary>
        Task RemoveAlertRuleAsync(string ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active alerts
        /// </summary>
        Task<Alert[]> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Acknowledges an alert
        /// </summary>
        Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves an alert
        /// </summary>
        Task ResolveAlertAsync(string alertId, string resolvedBy, string? resolution = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Alert status
    /// </summary>
    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved,
        Suppressed
    }

    /// <summary>
    /// Alert rule definition
    /// </summary>
    public class AlertRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public AlertCondition Condition { get; set; } = new();
        public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
        public TimeSpan EvaluationWindow { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(15);
        public bool IsEnabled { get; set; } = true;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> Labels { get; set; } = new();
        public AlertAction[] Actions { get; set; } = Array.Empty<AlertAction>();
    }

    /// <summary>
    /// Alert condition
    /// </summary>
    public class AlertCondition
    {
        public AlertOperator Operator { get; set; } = AlertOperator.GreaterThan;
        public double Threshold { get; set; }
        public int ConsecutiveBreaches { get; set; } = 1;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Alert operators
    /// </summary>
    public enum AlertOperator
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual
    }

    /// <summary>
    /// Alert action
    /// </summary>
    public class AlertAction
    {
        public string Type { get; set; } = string.Empty; // email, webhook, sms, etc.
        public Dictionary<string, string> Parameters { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Alert instance
    /// </summary>
    public class Alert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public AlertStatus Status { get; set; } = AlertStatus.Active;
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Resolution { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Alert notification channel
    /// </summary>
    public interface IAlertNotificationChannel
    {
        string ChannelType { get; }
        Task SendNotificationAsync(Alert alert, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Email notification channel
    /// </summary>
    public class EmailNotificationChannel : IAlertNotificationChannel
    {
        public string ChannelType => "email";

        public async Task SendNotificationAsync(Alert alert, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            // Implementation would send email notification
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Webhook notification channel
    /// </summary>
    public class WebhookNotificationChannel : IAlertNotificationChannel
    {
        public string ChannelType => "webhook";

        public async Task SendNotificationAsync(Alert alert, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            // Implementation would send webhook notification
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Slack notification channel
    /// </summary>
    public class SlackNotificationChannel : IAlertNotificationChannel
    {
        public string ChannelType => "slack";

        public async Task SendNotificationAsync(Alert alert, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
        {
            // Implementation would send Slack notification
            await Task.CompletedTask;
        }
    }
}
