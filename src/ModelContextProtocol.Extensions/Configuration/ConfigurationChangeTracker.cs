using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Tracks configuration changes for auditing and rollback purposes
    /// </summary>
    public class ConfigurationChangeTracker
    {
        private readonly ConcurrentDictionary<string, ConfigurationSnapshot> _snapshots;
        private readonly ConcurrentQueue<ConfigurationChange> _changeHistory;
        private readonly ILogger<ConfigurationChangeTracker> _logger;
        private readonly ConfigurationChangeTrackerOptions _options;

        public event Action<ConfigurationChange>? OnConfigurationChanged;

        public ConfigurationChangeTracker(
            ConfigurationChangeTrackerOptions options,
            ILogger<ConfigurationChangeTracker> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _snapshots = new ConcurrentDictionary<string, ConfigurationSnapshot>();
            _changeHistory = new ConcurrentQueue<ConfigurationChange>();
        }

        /// <summary>
        /// Takes a snapshot of the current configuration
        /// </summary>
        /// <param name="configPath">Configuration path</param>
        /// <param name="value">Current configuration value</param>
        public void TakeSnapshot(string configPath, object value)
        {
            if (string.IsNullOrEmpty(configPath))
                throw new ArgumentException("Configuration path cannot be null or empty", nameof(configPath));

            var snapshot = new ConfigurationSnapshot
            {
                Path = configPath,
                Value = SerializeValue(value),
                Timestamp = DateTime.UtcNow,
                Source = GetChangeSource()
            };

            _snapshots.AddOrUpdate(configPath, snapshot, (key, existing) => snapshot);

            _logger.LogDebug("Configuration snapshot taken for path {ConfigPath}", configPath);
        }

        /// <summary>
        /// Tracks a configuration change
        /// </summary>
        /// <param name="configPath">Configuration path</param>
        /// <param name="oldValue">Previous value</param>
        /// <param name="newValue">New value</param>
        /// <param name="source">Source of the change</param>
        public void TrackChange(string configPath, object? oldValue, object? newValue, string? source = null)
        {
            if (string.IsNullOrEmpty(configPath))
                throw new ArgumentException("Configuration path cannot be null or empty", nameof(configPath));

            var change = new ConfigurationChange
            {
                Id = Guid.NewGuid().ToString("N"),
                Path = configPath,
                OldValue = SerializeValue(oldValue),
                NewValue = SerializeValue(newValue),
                Timestamp = DateTime.UtcNow,
                Source = source ?? GetChangeSource(),
                ChangeType = DetermineChangeType(oldValue, newValue)
            };

            RecordChange(change);

            // Update snapshot
            TakeSnapshot(configPath, newValue);

            // Notify listeners
            try
            {
                OnConfigurationChanged?.Invoke(change);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying configuration change listeners");
            }

            _logger.LogInformation(
                "Configuration changed: {ConfigPath} from {OldValue} to {NewValue} by {Source}",
                configPath, change.OldValue, change.NewValue, change.Source);
        }

        /// <summary>
        /// Gets the change history for a specific configuration path
        /// </summary>
        /// <param name="configPath">Configuration path</param>
        /// <param name="limit">Maximum number of changes to return</param>
        /// <returns>Configuration history</returns>
        public ConfigurationHistory GetHistory(string configPath, int limit = 100)
        {
            if (string.IsNullOrEmpty(configPath))
                throw new ArgumentException("Configuration path cannot be null or empty", nameof(configPath));

            var changes = _changeHistory
                .Where(c => c.Path.Equals(configPath, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Timestamp)
                .Take(limit)
                .ToList();

            var currentSnapshot = _snapshots.TryGetValue(configPath, out var snapshot) ? snapshot : null;

            return new ConfigurationHistory
            {
                Path = configPath,
                CurrentSnapshot = currentSnapshot,
                Changes = changes,
                TotalChanges = changes.Count
            };
        }

        /// <summary>
        /// Gets all configuration changes within a time window
        /// </summary>
        /// <param name="timeWindow">Time window to search</param>
        /// <param name="limit">Maximum number of changes to return</param>
        /// <returns>List of configuration changes</returns>
        public List<ConfigurationChange> GetChangesInTimeWindow(TimeSpan timeWindow, int limit = 1000)
        {
            var cutoffTime = DateTime.UtcNow - timeWindow;

            return _changeHistory
                .Where(c => c.Timestamp >= cutoffTime)
                .OrderByDescending(c => c.Timestamp)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Gets configuration changes by source
        /// </summary>
        /// <param name="source">Source to filter by</param>
        /// <param name="limit">Maximum number of changes to return</param>
        /// <returns>List of configuration changes</returns>
        public List<ConfigurationChange> GetChangesBySource(string source, int limit = 100)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Source cannot be null or empty", nameof(source));

            return _changeHistory
                .Where(c => c.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Timestamp)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Generates a configuration audit report
        /// </summary>
        /// <param name="timeWindow">Time window for the report</param>
        /// <returns>Configuration audit report</returns>
        public ConfigurationAuditReport GenerateAuditReport(TimeSpan? timeWindow = null)
        {
            var cutoffTime = timeWindow.HasValue ? DateTime.UtcNow - timeWindow.Value : DateTime.MinValue;
            var relevantChanges = _changeHistory
                .Where(c => c.Timestamp >= cutoffTime)
                .ToList();

            var changesByPath = relevantChanges
                .GroupBy(c => c.Path)
                .ToDictionary(g => g.Key, g => g.Count());

            var changesBySource = relevantChanges
                .GroupBy(c => c.Source)
                .ToDictionary(g => g.Key, g => g.Count());

            var changesByType = relevantChanges
                .GroupBy(c => c.ChangeType)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ConfigurationAuditReport
            {
                GeneratedAt = DateTime.UtcNow,
                TimeWindow = timeWindow,
                TotalChanges = relevantChanges.Count,
                UniqueConfigPaths = changesByPath.Count,
                UniqueSources = changesBySource.Count,
                ChangesByPath = changesByPath,
                ChangesBySource = changesBySource,
                ChangesByType = changesByType,
                RecentChanges = relevantChanges
                    .OrderByDescending(c => c.Timestamp)
                    .Take(50)
                    .ToList()
            };
        }

        /// <summary>
        /// Clears old change history to prevent memory issues
        /// </summary>
        /// <param name="olderThan">Clear changes older than this timespan</param>
        public void CleanupOldChanges(TimeSpan olderThan)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var changesToKeep = new List<ConfigurationChange>();

            // Collect changes to keep
            while (_changeHistory.TryDequeue(out var change))
            {
                if (change.Timestamp >= cutoffTime)
                {
                    changesToKeep.Add(change);
                }
            }

            // Re-enqueue the changes to keep
            foreach (var change in changesToKeep)
            {
                _changeHistory.Enqueue(change);
            }

            _logger.LogInformation("Cleaned up configuration changes older than {TimeSpan}", olderThan);
        }

        private void RecordChange(ConfigurationChange change)
        {
            _changeHistory.Enqueue(change);

            // Limit the number of changes to prevent memory issues
            while (_changeHistory.Count > _options.MaxChangeHistory)
            {
                _changeHistory.TryDequeue(out _);
            }
        }

        private string SerializeValue(object? value)
        {
            if (value == null) return "null";

            try
            {
                return JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize configuration value, using ToString()");
                return value.ToString() ?? "null";
            }
        }

        private ConfigurationChangeType DetermineChangeType(object? oldValue, object? newValue)
        {
            if (oldValue == null && newValue != null)
                return ConfigurationChangeType.Added;

            if (oldValue != null && newValue == null)
                return ConfigurationChangeType.Removed;

            if (oldValue != null && newValue != null)
                return ConfigurationChangeType.Modified;

            return ConfigurationChangeType.Unknown;
        }

        private string GetChangeSource()
        {
            // Try to determine the source of the change
            var stackTrace = Environment.StackTrace;

            if (stackTrace.Contains("Microsoft.Extensions.Configuration"))
                return "ConfigurationProvider";

            if (stackTrace.Contains("Microsoft.Extensions.Options"))
                return "OptionsMonitor";

            return "Unknown";
        }
    }

    /// <summary>
    /// Configuration options for the change tracker
    /// </summary>
    public class ConfigurationChangeTrackerOptions
    {
        /// <summary>
        /// Maximum number of changes to keep in memory
        /// </summary>
        public int MaxChangeHistory { get; set; } = 10000;

        /// <summary>
        /// Whether to enable automatic cleanup of old changes
        /// </summary>
        public bool EnableAutoCleanup { get; set; } = true;

        /// <summary>
        /// How long to keep change history
        /// </summary>
        public TimeSpan ChangeHistoryRetention { get; set; } = TimeSpan.FromDays(30);
    }

    /// <summary>
    /// Represents a configuration snapshot at a point in time
    /// </summary>
    public class ConfigurationSnapshot
    {
        public string Path { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a configuration change event
    /// </summary>
    public class ConfigurationChange
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
        public ConfigurationChangeType ChangeType { get; set; }
    }

    /// <summary>
    /// Configuration change history for a specific path
    /// </summary>
    public class ConfigurationHistory
    {
        public string Path { get; set; } = string.Empty;
        public ConfigurationSnapshot? CurrentSnapshot { get; set; }
        public List<ConfigurationChange> Changes { get; set; } = new();
        public int TotalChanges { get; set; }
    }

    /// <summary>
    /// Configuration audit report
    /// </summary>
    public class ConfigurationAuditReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan? TimeWindow { get; set; }
        public int TotalChanges { get; set; }
        public int UniqueConfigPaths { get; set; }
        public int UniqueSources { get; set; }
        public Dictionary<string, int> ChangesByPath { get; set; } = new();
        public Dictionary<string, int> ChangesBySource { get; set; } = new();
        public Dictionary<ConfigurationChangeType, int> ChangesByType { get; set; } = new();
        public List<ConfigurationChange> RecentChanges { get; set; } = new();
    }

    /// <summary>
    /// Types of configuration changes
    /// </summary>
    public enum ConfigurationChangeType
    {
        Unknown,
        Added,
        Modified,
        Removed
    }
}
