using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ModelContextProtocol.Extensions.Validation
{
    /// <summary>
    /// Registry for configuration schemas and validation
    /// </summary>
    public static class ConfigurationSchemaRegistry
    {
        private static readonly ConcurrentDictionary<Type, IConfigurationSchema> _schemas = new();

        /// <summary>
        /// Registers a schema for a configuration type
        /// </summary>
        public static void RegisterSchema<T>(IConfigurationSchema schema)
        {
            _schemas[typeof(T)] = schema;
        }

        /// <summary>
        /// Gets the schema for a configuration type
        /// </summary>
        public static IConfigurationSchema GetSchema<T>() => _schemas.GetValueOrDefault(typeof(T));

        /// <summary>
        /// Gets the schema for a configuration type
        /// </summary>
        public static IConfigurationSchema GetSchema(Type type) => _schemas.GetValueOrDefault(type);

        /// <summary>
        /// Validates configuration using the registered schema
        /// </summary>
        public static ValidationResult ValidateConfiguration<T>(T configuration)
        {
            var schema = GetSchema<T>();
            if (schema == null)
                return ValidationResult.Success();

            return schema.Validate(configuration);
        }

        /// <summary>
        /// Validates configuration JSON using the registered schema
        /// </summary>
        public static ValidationResult ValidateConfigurationJson<T>(string json)
        {
            var schema = GetSchema<T>();
            if (schema == null)
                return ValidationResult.Success();

            try
            {
                var document = JsonDocument.Parse(json);
                return schema.ValidateJson(document.RootElement);
            }
            catch (JsonException ex)
            {
                return ValidationResult.Fail($"Invalid JSON: {ex.Message}", "JSON");
            }
        }

        /// <summary>
        /// Gets all registered schema types
        /// </summary>
        public static IEnumerable<Type> GetRegisteredTypes() => _schemas.Keys;

        /// <summary>
        /// Clears all registered schemas
        /// </summary>
        public static void Clear() => _schemas.Clear();
    }

    /// <summary>
    /// Interface for configuration schemas
    /// </summary>
    public interface IConfigurationSchema
    {
        /// <summary>
        /// Validates a configuration object
        /// </summary>
        ValidationResult Validate(object configuration);

        /// <summary>
        /// Validates a JSON element
        /// </summary>
        ValidationResult ValidateJson(JsonElement jsonElement);

        /// <summary>
        /// Gets the schema definition
        /// </summary>
        object GetSchemaDefinition();
    }

    /// <summary>
    /// Generic configuration schema implementation
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public class ConfigurationSchema<T> : IConfigurationSchema where T : class
    {
        private readonly List<IConfigurationRule<T>> _rules = new();
        private readonly Dictionary<string, object> _schemaDefinition = new();

        /// <summary>
        /// Adds a validation rule
        /// </summary>
        public ConfigurationSchema<T> AddRule(IConfigurationRule<T> rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// Adds a property constraint to the schema
        /// </summary>
        public ConfigurationSchema<T> AddProperty(string name, Type type, bool required = false, object defaultValue = null)
        {
            _schemaDefinition[name] = new
            {
                Type = type.Name,
                Required = required,
                DefaultValue = defaultValue
            };
            return this;
        }

        /// <summary>
        /// Validates a configuration object
        /// </summary>
        public ValidationResult Validate(object configuration)
        {
            if (configuration is not T typedConfig)
            {
                return ValidationResult.Fail($"Configuration must be of type {typeof(T).Name}", "Type");
            }

            var errors = new List<string>();

            foreach (var rule in _rules)
            {
                var result = rule.Validate(typedConfig);
                if (!result.IsValid)
                {
                    errors.Add(result.ErrorMessage);
                }
            }

            return errors.Count > 0
                ? ValidationResult.Fail(string.Join("; ", errors), "Configuration")
                : ValidationResult.Success();
        }

        /// <summary>
        /// Validates a JSON element
        /// </summary>
        public ValidationResult ValidateJson(JsonElement jsonElement)
        {
            try
            {
                var config = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                return Validate(config);
            }
            catch (JsonException ex)
            {
                return ValidationResult.Fail($"JSON deserialization failed: {ex.Message}", "JSON");
            }
        }

        /// <summary>
        /// Gets the schema definition
        /// </summary>
        public object GetSchemaDefinition() => _schemaDefinition;
    }

    /// <summary>
    /// Interface for configuration validation rules
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public interface IConfigurationRule<T>
    {
        /// <summary>
        /// Validates the configuration
        /// </summary>
        ValidationResult Validate(T configuration);
    }

    /// <summary>
    /// Rule that validates a property using a predicate
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public class PropertyRule<T> : IConfigurationRule<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly string _errorMessage;

        public PropertyRule(Func<T, bool> predicate, string errorMessage)
        {
            _predicate = predicate;
            _errorMessage = errorMessage;
        }

        public ValidationResult Validate(T configuration)
        {
            return _predicate(configuration)
                ? ValidationResult.Success()
                : ValidationResult.Fail(_errorMessage, "Property");
        }
    }

    /// <summary>
    /// Rule that validates a property is not null or empty
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public class RequiredPropertyRule<T> : IConfigurationRule<T>
    {
        private readonly Func<T, string> _propertySelector;
        private readonly string _propertyName;

        public RequiredPropertyRule(Func<T, string> propertySelector, string propertyName)
        {
            _propertySelector = propertySelector;
            _propertyName = propertyName;
        }

        public ValidationResult Validate(T configuration)
        {
            var value = _propertySelector(configuration);
            return string.IsNullOrWhiteSpace(value)
                ? ValidationResult.Fail($"Property '{_propertyName}' is required", _propertyName)
                : ValidationResult.Success();
        }
    }

    /// <summary>
    /// Rule that validates a numeric property is within a range
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public class RangeRule<T> : IConfigurationRule<T>
    {
        private readonly Func<T, double> _propertySelector;
        private readonly double _min;
        private readonly double _max;
        private readonly string _propertyName;

        public RangeRule(Func<T, double> propertySelector, double min, double max, string propertyName)
        {
            _propertySelector = propertySelector;
            _min = min;
            _max = max;
            _propertyName = propertyName;
        }

        public ValidationResult Validate(T configuration)
        {
            var value = _propertySelector(configuration);
            return value >= _min && value <= _max
                ? ValidationResult.Success()
                : ValidationResult.Fail($"Property '{_propertyName}' must be between {_min} and {_max}", _propertyName);
        }
    }

    /// <summary>
    /// Rule that validates a URL property
    /// </summary>
    /// <typeparam name="T">The configuration type</typeparam>
    public class UrlRule<T> : IConfigurationRule<T>
    {
        private readonly Func<T, string> _propertySelector;
        private readonly string _propertyName;
        private readonly bool _allowEmpty;

        public UrlRule(Func<T, string> propertySelector, string propertyName, bool allowEmpty = true)
        {
            _propertySelector = propertySelector;
            _propertyName = propertyName;
            _allowEmpty = allowEmpty;
        }

        public ValidationResult Validate(T configuration)
        {
            var value = _propertySelector(configuration);

            if (string.IsNullOrWhiteSpace(value))
            {
                return _allowEmpty
                    ? ValidationResult.Success()
                    : ValidationResult.Fail($"Property '{_propertyName}' cannot be empty", _propertyName);
            }

            return Uri.TryCreate(value, UriKind.Absolute, out _)
                ? ValidationResult.Success()
                : ValidationResult.Fail($"Property '{_propertyName}' must be a valid URL", _propertyName);
        }
    }

    /// <summary>
    /// Extension methods for building configuration schemas
    /// </summary>
    public static class ConfigurationSchemaExtensions
    {
        /// <summary>
        /// Adds a required property rule
        /// </summary>
        public static ConfigurationSchema<T> RequireProperty<T>(
            this ConfigurationSchema<T> schema,
            Func<T, string> propertySelector,
            string propertyName) where T : class
        {
            return schema.AddRule(new RequiredPropertyRule<T>(propertySelector, propertyName));
        }

        /// <summary>
        /// Adds a range validation rule
        /// </summary>
        public static ConfigurationSchema<T> ValidateRange<T>(
            this ConfigurationSchema<T> schema,
            Func<T, double> propertySelector,
            double min,
            double max,
            string propertyName) where T : class
        {
            return schema.AddRule(new RangeRule<T>(propertySelector, min, max, propertyName));
        }

        /// <summary>
        /// Adds a URL validation rule
        /// </summary>
        public static ConfigurationSchema<T> ValidateUrl<T>(
            this ConfigurationSchema<T> schema,
            Func<T, string> propertySelector,
            string propertyName,
            bool allowEmpty = true) where T : class
        {
            return schema.AddRule(new UrlRule<T>(propertySelector, propertyName, allowEmpty));
        }

        /// <summary>
        /// Adds a custom validation rule
        /// </summary>
        public static ConfigurationSchema<T> ValidateWith<T>(
            this ConfigurationSchema<T> schema,
            Func<T, bool> predicate,
            string errorMessage) where T : class
        {
            return schema.AddRule(new PropertyRule<T>(predicate, errorMessage));
        }
    }
}
