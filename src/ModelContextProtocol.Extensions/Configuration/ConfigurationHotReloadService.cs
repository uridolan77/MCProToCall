using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Configuration options for hot reload functionality
    /// </summary>
    public class HotReloadOptions
    {
        /// <summary>
        /// Whether hot reload is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Polling interval for configuration changes in seconds
        /// </summary>
        [Range(1, 300)]
        public int PollingIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Maximum number of reload attempts on failure
        /// </summary>
        [Range(1, 10)]
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in seconds
        /// </summary>
        [Range(1, 60)]
        public int RetryDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Configuration files to monitor for changes
        /// </summary>
        public List<string> MonitoredFiles { get; set; } = new()
        {
            "appsettings.json",
            "appsettings.Development.json",
            "appsettings.Production.json"
        };

        /// <summary>
        /// Whether to validate configuration after reload
        /// </summary>
        public bool ValidateAfterReload { get; set; } = true;
    }

    /// <summary>
    /// Event arguments for configuration change events
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ConfigurationKey { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangeTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service for monitoring and hot-reloading configuration changes
    /// </summary>
    public class ConfigurationHotReloadService : BackgroundService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationHotReloadService> _logger;
        private readonly HotReloadOptions _options;
        private readonly Dictionary<string, FileSystemWatcher> _fileWatchers;
        private readonly Dictionary<string, DateTime> _lastConfigValues;
        private readonly SemaphoreSlim _reloadSemaphore;
        private volatile bool _isDisposed;

        /// <summary>
        /// Event fired when configuration changes are detected
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        /// <summary>
        /// Event fired when configuration reload fails
        /// </summary>
        public event EventHandler<Exception> ReloadFailed;

        /// <summary>
        /// Event fired when configuration is successfully reloaded
        /// </summary>
        public event EventHandler ReloadSucceeded;

        public ConfigurationHotReloadService(
            IConfiguration configuration,
            IOptions<HotReloadOptions> options,
            ILogger<ConfigurationHotReloadService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _fileWatchers = new Dictionary<string, FileSystemWatcher>();
            _lastConfigValues = new Dictionary<string, DateTime>();
            _reloadSemaphore = new SemaphoreSlim(1, 1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Configuration hot reload is disabled");
                return;
            }

            _logger.LogInformation("Starting configuration hot reload service with polling interval: {Interval}s",
                _options.PollingIntervalSeconds);

            try
            {
                SetupFileWatchers();
                await MonitorConfigurationChanges(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in configuration hot reload service");
                throw;
            }
            finally
            {
                CleanupFileWatchers();
            }
        }

        private void SetupFileWatchers()
        {
            foreach (var fileName in _options.MonitoredFiles)
            {
                try
                {
                    var fullPath = Path.GetFullPath(fileName);
                    if (!File.Exists(fullPath))
                    {
                        _logger.LogWarning("Configuration file {FileName} not found, skipping monitoring", fileName);
                        continue;
                    }

                    var directory = Path.GetDirectoryName(fullPath);
                    var fileNameOnly = Path.GetFileName(fullPath);

                    var watcher = new FileSystemWatcher(directory, fileNameOnly)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += async (sender, e) => await OnFileChanged(e.FullPath);
                    _fileWatchers[fileName] = watcher;

                    _logger.LogDebug("Setup file watcher for {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to setup file watcher for {FileName}", fileName);
                }
            }
        }

        private async Task OnFileChanged(string filePath)
        {
            if (_isDisposed)
                return;

            await _reloadSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Configuration file {FilePath} changed, reloading...", filePath);
                await ReloadConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file change for {FilePath}", filePath);
                ReloadFailed?.Invoke(this, ex);
            }
            finally
            {
                _reloadSemaphore.Release();
            }
        }

        private async Task MonitorConfigurationChanges(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_isDisposed)
            {
                try
                {
                    await CheckForConfigurationChanges();
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring configuration changes");
                    await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
                }
            }
        }

        private Task CheckForConfigurationChanges()
        {
            // This is a simplified check - in a real implementation, you might want to
            // compare configuration values or file timestamps
            var configRoot = _configuration as IConfigurationRoot;
            if (configRoot != null)
            {
                try
                {
                    configRoot.Reload();
                    _logger.LogDebug("Configuration reloaded successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading configuration");
                    ReloadFailed?.Invoke(this, ex);
                }
            }

            return Task.CompletedTask;
        }

        private async Task ReloadConfigurationAsync()
        {
            var retryCount = 0;
            while (retryCount < _options.MaxRetryAttempts)
            {
                try
                {
                    // Reload configuration
                    var configRoot = _configuration as IConfigurationRoot;
                    configRoot?.Reload();

                    if (_options.ValidateAfterReload)
                    {
                        await ValidateConfigurationAsync();
                    }

                    _logger.LogInformation("Configuration reloaded successfully");
                    ReloadSucceeded?.Invoke(this, EventArgs.Empty);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Configuration reload attempt {Attempt}/{MaxAttempts} failed",
                        retryCount, _options.MaxRetryAttempts);

                    if (retryCount >= _options.MaxRetryAttempts)
                    {
                        _logger.LogError("Configuration reload failed after {MaxAttempts} attempts", _options.MaxRetryAttempts);
                        ReloadFailed?.Invoke(this, ex);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds));
                }
            }
        }

        private async Task ValidateConfigurationAsync()
        {
            // Example validation - check critical configuration sections
            var criticalSections = new[] { "ConnectionStrings", "Logging", "McpServer" };
            
            foreach (var section in criticalSections)
            {
                var configSection = _configuration.GetSection(section);
                if (!configSection.Exists())
                {
                    throw new InvalidOperationException($"Critical configuration section '{section}' is missing");
                }
            }

            // Additional validation can be added here
            await Task.CompletedTask;
        }

        private void CleanupFileWatchers()
        {
            foreach (var watcher in _fileWatchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing file watcher");
                }
            }
            _fileWatchers.Clear();
        }

        public override void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            CleanupFileWatchers();
            _reloadSemaphore?.Dispose();
            base.Dispose();
            
            _logger.LogInformation("Configuration hot reload service disposed");
        }
    }

    /// <summary>
    /// Configuration validator service
    /// </summary>
    public class ConfigurationValidationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationValidationService> _logger;

        public ConfigurationValidationService(
            IConfiguration configuration,
            ILogger<ConfigurationValidationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates the entire configuration
        /// </summary>
        public async Task<ConfigurationValidationResult> ValidateAsync()
        {
            var result = new ConfigurationValidationResult();
            
            try
            {
                // Validate critical sections
                await ValidateCriticalSections(result);
                
                // Validate connection strings
                await ValidateConnectionStrings(result);
                
                // Validate server options
                await ValidateServerOptions(result);
                
                // Validate security settings
                await ValidateSecuritySettings(result);

                result.IsValid = result.Errors.Count == 0;
                
                if (result.IsValid)
                {
                    _logger.LogInformation("Configuration validation passed");
                }
                else
                {
                    _logger.LogWarning("Configuration validation failed with {ErrorCount} errors", result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation failed with exception: {ex.Message}");
                _logger.LogError(ex, "Configuration validation failed");
            }

            return result;
        }

        private async Task ValidateCriticalSections(ConfigurationValidationResult result)
        {
            var criticalSections = new[] { "ConnectionStrings", "Logging", "McpServer" };
            
            foreach (var section in criticalSections)
            {
                if (!_configuration.GetSection(section).Exists())
                {
                    result.Errors.Add($"Critical configuration section '{section}' is missing");
                }
            }

            await Task.CompletedTask;
        }

        private async Task ValidateConnectionStrings(ConfigurationValidationResult result)
        {
            var connectionStrings = _configuration.GetSection("ConnectionStrings");
            if (connectionStrings.Exists())
            {
                foreach (var child in connectionStrings.GetChildren())
                {
                    if (string.IsNullOrEmpty(child.Value))
                    {
                        result.Errors.Add($"Connection string '{child.Key}' is empty");
                    }
                }
            }

            await Task.CompletedTask;
        }

        private async Task ValidateServerOptions(ConfigurationValidationResult result)
        {
            var serverSection = _configuration.GetSection("McpServer");
            if (serverSection.Exists())
            {
                var host = serverSection["Host"];
                var port = serverSection["Port"];

                if (string.IsNullOrEmpty(host))
                {
                    result.Errors.Add("McpServer:Host is required");
                }

                if (!int.TryParse(port, out var portNumber) || portNumber <= 0 || portNumber > 65535)
                {
                    result.Errors.Add("McpServer:Port must be a valid port number (1-65535)");
                }
            }

            await Task.CompletedTask;
        }

        private async Task ValidateSecuritySettings(ConfigurationValidationResult result)
        {
            var securitySection = _configuration.GetSection("Security");
            if (securitySection.Exists())
            {
                var enableTls = securitySection.GetValue<bool>("EnableTls");
                var certificatePath = securitySection["CertificatePath"];

                if (enableTls && string.IsNullOrEmpty(certificatePath))
                {
                    result.Errors.Add("Security:CertificatePath is required when TLS is enabled");
                }

                if (!string.IsNullOrEmpty(certificatePath) && !File.Exists(certificatePath))
                {
                    result.Errors.Add($"Certificate file not found: {certificatePath}");
                }
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Result of configuration validation
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
    }
}
