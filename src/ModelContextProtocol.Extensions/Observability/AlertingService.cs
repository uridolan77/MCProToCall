using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Implementation of alerting service with rule-based alerting
    /// </summary>
    public class AlertingService : IAlertingService, IDisposable
    {
        private readonly ILogger<AlertingService> _logger;
        private readonly AlertingServiceOptions _options;
        private readonly ConcurrentDictionary<string, AlertRule> _alertRules = new();
        private readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
        private readonly Dictionary<string, IAlertNotificationChannel> _notificationChannels = new();
        private readonly Timer _cleanupTimer;

        public AlertingService(
            ILogger<AlertingService> logger,
            IOptions<AlertingServiceOptions> options,
            IEnumerable<IAlertNotificationChannel> notificationChannels)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Register notification channels
            foreach (var channel in notificationChannels)
            {
                _notificationChannels[channel.ChannelType] = channel;
            }

            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredAlerts, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task TriggerAlertAsync(string alertType, object data, AlertSeverity severity, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(alertType))
                throw new ArgumentException("Alert type cannot be null or empty", nameof(alertType));

            try
            {
                var alert = new Alert
                {
                    RuleName = alertType,
                    Severity = severity,
                    Message = GenerateAlertMessage(alertType, data, severity),
                    Data = data,
                    TriggeredAt = DateTime.UtcNow
                };

                // Check cooldown period
                var cooldownKey = $"{alertType}:{severity}";
                if (_lastAlertTimes.TryGetValue(cooldownKey, out var lastTime))
                {
                    if (DateTime.UtcNow - lastTime < _options.DefaultCooldownPeriod)
                    {
                        _logger.LogDebug("Alert suppressed due to cooldown: {AlertType}", alertType);
                        return;
                    }
                }

                _activeAlerts[alert.Id] = alert;
                _lastAlertTimes[cooldownKey] = DateTime.UtcNow;

                // Send notifications
                await SendNotificationsAsync(alert, cancellationToken);

                _logger.LogWarning("Alert triggered: {AlertType} - {Message}", alertType, alert.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering alert: {AlertType}", alertType);
                throw;
            }
        }

        public async Task<bool> ShouldTriggerAlertAsync(string metricName, double value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(metricName))
                return false;

            try
            {
                // Find matching alert rules
                var matchingRules = _alertRules.Values
                    .Where(rule => rule.IsEnabled && rule.MetricName == metricName)
                    .ToArray();

                foreach (var rule in matchingRules)
                {
                    if (EvaluateCondition(rule.Condition, value))
                    {
                        // Check cooldown
                        var cooldownKey = $"{rule.Id}:{metricName}";
                        if (_lastAlertTimes.TryGetValue(cooldownKey, out var lastTime))
                        {
                            if (DateTime.UtcNow - lastTime < rule.CooldownPeriod)
                            {
                                continue;
                            }
                        }

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating alert conditions for metric: {MetricName}", metricName);
                return false;
            }
        }

        public Task RegisterAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _alertRules[rule.Id] = rule;
            _logger.LogInformation("Registered alert rule: {RuleId} - {RuleName}", rule.Id, rule.Name);
            return Task.CompletedTask;
        }

        public Task RemoveAlertRuleAsync(string ruleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ruleId))
                throw new ArgumentException("Rule ID cannot be null or empty", nameof(ruleId));

            if (_alertRules.TryRemove(ruleId, out var removedRule))
            {
                _logger.LogInformation("Removed alert rule: {RuleId} - {RuleName}", ruleId, removedRule.Name);
            }

            return Task.CompletedTask;
        }

        public Task<Alert[]> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            var activeAlerts = _activeAlerts.Values
                .Where(alert => alert.Status == AlertStatus.Active)
                .OrderByDescending(alert => alert.TriggeredAt)
                .ToArray();

            return Task.FromResult(activeAlerts);
        }

        public Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(alertId))
                throw new ArgumentException("Alert ID cannot be null or empty", nameof(alertId));

            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = AlertStatus.Acknowledged;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgedBy = acknowledgedBy;

                _logger.LogInformation("Alert acknowledged: {AlertId} by {AcknowledgedBy}", alertId, acknowledgedBy);
            }

            return Task.CompletedTask;
        }

        public Task ResolveAlertAsync(string alertId, string resolvedBy, string? resolution = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(alertId))
                throw new ArgumentException("Alert ID cannot be null or empty", nameof(alertId));

            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = AlertStatus.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.ResolvedBy = resolvedBy;
                alert.Resolution = resolution;

                _logger.LogInformation("Alert resolved: {AlertId} by {ResolvedBy}", alertId, resolvedBy);
            }

            return Task.CompletedTask;
        }

        private bool EvaluateCondition(AlertCondition condition, double value)
        {
            return condition.Operator switch
            {
                AlertOperator.GreaterThan => value > condition.Threshold,
                AlertOperator.GreaterThanOrEqual => value >= condition.Threshold,
                AlertOperator.LessThan => value < condition.Threshold,
                AlertOperator.LessThanOrEqual => value <= condition.Threshold,
                AlertOperator.Equal => Math.Abs(value - condition.Threshold) < 0.001,
                AlertOperator.NotEqual => Math.Abs(value - condition.Threshold) >= 0.001,
                _ => false
            };
        }

        private string GenerateAlertMessage(string alertType, object data, AlertSeverity severity)
        {
            return $"[{severity}] {alertType}: {System.Text.Json.JsonSerializer.Serialize(data)}";
        }

        private async Task SendNotificationsAsync(Alert alert, CancellationToken cancellationToken)
        {
            // Find matching rule for notification actions
            var rule = _alertRules.Values.FirstOrDefault(r => r.Name == alert.RuleName);
            if (rule?.Actions == null || rule.Actions.Length == 0)
            {
                // Use default notification if no specific actions defined
                await SendDefaultNotificationAsync(alert, cancellationToken);
                return;
            }

            var tasks = new List<Task>();
            foreach (var action in rule.Actions.Where(a => a.IsEnabled))
            {
                if (_notificationChannels.TryGetValue(action.Type, out var channel))
                {
                    tasks.Add(channel.SendNotificationAsync(alert, action.Parameters, cancellationToken));
                }
            }

            if (tasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending alert notifications for alert: {AlertId}", alert.Id);
                }
            }
        }

        private async Task SendDefaultNotificationAsync(Alert alert, CancellationToken cancellationToken)
        {
            // Send to default notification channel if configured
            if (!string.IsNullOrEmpty(_options.DefaultNotificationChannel) &&
                _notificationChannels.TryGetValue(_options.DefaultNotificationChannel, out var channel))
            {
                try
                {
                    await channel.SendNotificationAsync(alert, new Dictionary<string, string>(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending default notification for alert: {AlertId}", alert.Id);
                }
            }
        }

        private void CleanupExpiredAlerts(object? state)
        {
            try
            {
                var expiredAlerts = _activeAlerts.Values
                    .Where(alert => alert.Status == AlertStatus.Resolved && 
                                   alert.ResolvedAt.HasValue && 
                                   DateTime.UtcNow - alert.ResolvedAt.Value > _options.AlertRetentionPeriod)
                    .ToArray();

                foreach (var alert in expiredAlerts)
                {
                    _activeAlerts.TryRemove(alert.Id, out _);
                }

                if (expiredAlerts.Length > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired alerts", expiredAlerts.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// Configuration options for alerting service
    /// </summary>
    public class AlertingServiceOptions
    {
        public TimeSpan DefaultCooldownPeriod { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan AlertRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
        public string? DefaultNotificationChannel { get; set; }
        public int MaxActiveAlerts { get; set; } = 1000;
        public bool EnableAlertAggregation { get; set; } = true;
    }
}
