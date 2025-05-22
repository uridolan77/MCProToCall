using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Configuration validator service
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ILogger<ConfigurationValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationValidator"/> class
        /// </summary>
        /// <param name="logger">Logger</param>
        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates a configuration object
        /// </summary>
        /// <typeparam name="T">Configuration type</typeparam>
        /// <param name="configuration">Configuration object</param>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateConfiguration<T>(T configuration) where T : class
        {
            var context = new ValidationContext(configuration);
            var results = new List<ValidationResult>();

            // Validate data annotations
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!Validator.TryValidateObject(configuration, context, validationResults, true))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = validationResults.Select(r => new ValidationError
                    {
                        Message = r.ErrorMessage,
                        MemberNames = r.MemberNames
                    }).ToList()
                };
            }

            // If object implements IValidatableObject, run custom validation
            if (configuration is IValidatableObject validatable)
            {
                var customResults = validatable.Validate(context);
                if (customResults.Any())
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Errors = customResults.Select(r => new ValidationError
                        {
                            Message = r.ErrorMessage,
                            MemberNames = r.MemberNames
                        }).ToList()
                    };
                }
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Validation result
        /// </summary>
        public class ValidationResult
        {
            /// <summary>
            /// Gets or sets a value indicating whether the validation was successful
            /// </summary>
            public bool IsValid { get; set; }

            /// <summary>
            /// Gets or sets the validation errors
            /// </summary>
            public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

            /// <summary>
            /// Logs the validation errors
            /// </summary>
            /// <param name="logger">Logger</param>
            public void LogErrors(ILogger logger)
            {
                foreach (var error in Errors)
                {
                    var members = error.MemberNames.Any()
                        ? string.Join(", ", error.MemberNames)
                        : "General";

                    logger.LogError("Configuration validation error [{Members}]: {Message}",
                        members, error.Message);
                }
            }
        }

        /// <summary>
        /// Validation error
        /// </summary>
        public class ValidationError
        {
            /// <summary>
            /// Gets or sets the error message
            /// </summary>
            public string Message { get; set; }

            /// <summary>
            /// Gets or sets the member names
            /// </summary>
            public IEnumerable<string> MemberNames { get; set; } = Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Configuration exception
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationException"/> class
        /// </summary>
        /// <param name="message">Exception message</param>
        public ConfigurationException(string message) : base(message) { }
    }
}
