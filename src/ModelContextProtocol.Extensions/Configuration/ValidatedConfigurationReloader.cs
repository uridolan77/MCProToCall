using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Configuration reloader with validation support
    /// </summary>
    /// <typeparam name="T">The options type</typeparam>
    public class ValidatedConfigurationReloader<T> : IOptionsMonitor<T> where T : class, IValidatableObject, new()
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ValidatedConfigurationReloader<T>> _logger;
        private readonly string _sectionName;
        private T _currentValue;
        private readonly List<IDisposable> _changeTokenRegistrations = new();
        private readonly object _lock = new object();

        public ValidatedConfigurationReloader(
            IConfiguration configuration,
            ILogger<ValidatedConfigurationReloader<T>> logger,
            string sectionName = null)
        {
            _configuration = configuration;
            _logger = logger;
            _sectionName = sectionName ?? typeof(T).Name;
        }

        /// <summary>
        /// Gets the current configuration value
        /// </summary>
        public T CurrentValue
        {
            get
            {
                lock (_lock)
                {
                    return _currentValue ??= LoadAndValidate();
                }
            }
        }

        /// <summary>
        /// Gets the configuration value by name (not used in this implementation)
        /// </summary>
        public T Get(string name) => CurrentValue;

        /// <summary>
        /// Registers a callback for configuration changes
        /// </summary>
        public IDisposable OnChange(Action<T, string> listener)
        {
            var registration = ChangeToken.OnChange(_configuration.GetReloadToken, () =>
            {
                try
                {
                    T newValue;
                    lock (_lock)
                    {
                        newValue = LoadAndValidate();
                        var oldValue = _currentValue;
                        _currentValue = newValue;
                    }

                    listener(newValue, null);
                    _logger.LogInformation("Configuration reloaded successfully for {Type}", typeof(T).Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload configuration for {Type}", typeof(T).Name);
                    // Don't update the current value if validation fails
                }
            });

            lock (_lock)
            {
                _changeTokenRegistrations.Add(registration);
            }

            return registration;
        }

        /// <summary>
        /// Loads and validates the configuration
        /// </summary>
        private T LoadAndValidate()
        {
            var instance = new T();
            var section = _configuration.GetSection(_sectionName);

            if (!section.Exists())
            {
                _logger.LogWarning("Configuration section '{SectionName}' not found, using default values", _sectionName);
                return instance;
            }

            try
            {
                section.Bind(instance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind configuration section '{SectionName}'", _sectionName);
                throw new InvalidOperationException($"Configuration binding failed for {typeof(T).Name}: {ex.Message}", ex);
            }

            // Validate using data annotations
            var validationContext = new ValidationContext(instance);
            var dataAnnotationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(instance, validationContext, dataAnnotationResults, true))
            {
                var errors = string.Join("; ", dataAnnotationResults.Select(r => r.ErrorMessage));
                _logger.LogError("Data annotation validation failed for {Type}: {Errors}", typeof(T).Name, errors);
                throw new InvalidOperationException($"Data annotation validation failed for {typeof(T).Name}: {errors}");
            }

            // Validate using IValidatableObject
            var customValidationResults = instance.Validate(validationContext).ToList();
            if (customValidationResults.Count > 0)
            {
                var errors = string.Join("; ", customValidationResults.Select(r => r.ErrorMessage));
                _logger.LogError("Custom validation failed for {Type}: {Errors}", typeof(T).Name, errors);
                throw new InvalidOperationException($"Custom validation failed for {typeof(T).Name}: {errors}");
            }

            _logger.LogDebug("Configuration loaded and validated successfully for {Type}", typeof(T).Name);
            return instance;
        }

        /// <summary>
        /// Disposes all change token registrations
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var registration in _changeTokenRegistrations)
                {
                    registration?.Dispose();
                }
                _changeTokenRegistrations.Clear();
            }
        }
    }

    /// <summary>
    /// Distributed configuration provider that achieves consensus across multiple sources
    /// </summary>
    public class DistributedConfigurationProvider : IConfigurationProvider, IDisposable
    {
        private readonly Dictionary<string, string> _data = new();
        private readonly ILogger<DistributedConfigurationProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly string[] _configurationEndpoints;
        private readonly TimeSpan _consensusTimeout;
        private bool _disposed;

        public DistributedConfigurationProvider(
            ILogger<DistributedConfigurationProvider> logger,
            HttpClient httpClient,
            string[] configurationEndpoints,
            TimeSpan consensusTimeout = default)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configurationEndpoints = configurationEndpoints ?? Array.Empty<string>();
            _consensusTimeout = consensusTimeout == default ? TimeSpan.FromSeconds(30) : consensusTimeout;
        }

        /// <summary>
        /// Loads configuration from multiple sources and achieves consensus
        /// </summary>
        public void Load()
        {
            if (_configurationEndpoints.Length == 0)
            {
                _logger.LogWarning("No configuration endpoints specified for distributed configuration");
                return;
            }

            try
            {
                var configurations = LoadFromMultipleSources();
                var consensusConfig = AchieveConsensus(configurations);

                lock (_data)
                {
                    _data.Clear();
                    foreach (var kvp in consensusConfig)
                    {
                        _data[kvp.Key] = kvp.Value;
                    }
                }

                _logger.LogInformation("Distributed configuration loaded with consensus from {EndpointCount} endpoints",
                    _configurationEndpoints.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load distributed configuration");
                throw;
            }
        }

        /// <summary>
        /// Loads configuration from multiple sources
        /// </summary>
        private List<Dictionary<string, string>> LoadFromMultipleSources()
        {
            var configurations = new List<Dictionary<string, string>>();
            var tasks = _configurationEndpoints.Select(LoadFromEndpoint).ToArray();

            try
            {
                Task.WaitAll(tasks, _consensusTimeout);
            }
            catch (AggregateException ex)
            {
                _logger.LogWarning("Some configuration endpoints failed to respond: {Errors}",
                    string.Join("; ", ex.InnerExceptions.Select(e => e.Message)));
            }

            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully && task.Result != null)
                {
                    configurations.Add(task.Result);
                }
            }

            if (configurations.Count == 0)
            {
                throw new InvalidOperationException("No configuration sources were available");
            }

            return configurations;
        }

        /// <summary>
        /// Loads configuration from a single endpoint
        /// </summary>
        private async Task<Dictionary<string, string>> LoadFromEndpoint(string endpoint)
        {
            try
            {
                _logger.LogDebug("Loading configuration from endpoint: {Endpoint}", endpoint);

                var response = await _httpClient.GetStringAsync(endpoint);
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(response);

                _logger.LogDebug("Successfully loaded configuration from endpoint: {Endpoint}", endpoint);
                return config ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load configuration from endpoint: {Endpoint}", endpoint);
                return null;
            }
        }

        /// <summary>
        /// Achieves consensus across multiple configuration sources
        /// </summary>
        private Dictionary<string, string> AchieveConsensus(List<Dictionary<string, string>> configurations)
        {
            var result = new Dictionary<string, string>();
            var allKeys = configurations.SelectMany(c => c.Keys).Distinct();

            foreach (var key in allKeys)
            {
                var values = configurations
                    .Where(c => c.ContainsKey(key))
                    .Select(c => c[key])
                    .GroupBy(v => v)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                if (values.Count > 0)
                {
                    var mostCommonValue = values.First();
                    result[key] = mostCommonValue.Key;

                    // Log if there's disagreement
                    if (values.Count > 1)
                    {
                        _logger.LogWarning("Configuration disagreement for key '{Key}': {Values}. Using most common value: '{Value}'",
                            key, string.Join(", ", values.Select(g => $"{g.Key}({g.Count()})")), mostCommonValue.Key);
                    }
                }
            }

            return result;
        }

        public bool TryGet(string key, out string value) => _data.TryGetValue(key, out value);

        public void Set(string key, string value)
        {
            lock (_data)
            {
                _data[key] = value;
            }
        }

        public IChangeToken GetReloadToken()
        {
            // For simplicity, return a change token that never changes
            // In a real implementation, this would monitor the configuration sources
            return new CancellationChangeToken(CancellationToken.None);
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath)
        {
            var prefix = parentPath == null ? string.Empty : parentPath + ConfigurationPath.KeyDelimiter;

            return _data.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => Segment(k, prefix.Length))
                .Concat(earlierKeys)
                .OrderBy(k => k, ConfigurationKeyComparer.Instance);
        }

        private static string Segment(string key, int prefixLength)
        {
            var indexOf = key.IndexOf(ConfigurationPath.KeyDelimiter, prefixLength);
            return indexOf < 0 ? key.Substring(prefixLength) : key.Substring(prefixLength, indexOf - prefixLength);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
