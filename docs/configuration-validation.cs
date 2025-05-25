using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Configuration validation attributes
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidPortAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is int port)
            {
                if (port < 1 || port > 65535)
                {
                    return new ValidationResult($"Port must be between 1 and 65535, but was {port}");
                }
            }
            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ValidHostAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string host && !string.IsNullOrWhiteSpace(host))
            {
                // Validate IP address or hostname
                if (!IPAddress.TryParse(host, out _) && 
                    !Uri.CheckHostName(host).Equals(UriHostNameType.Dns))
                {
                    return new ValidationResult($"'{host}' is not a valid IP address or hostname");
                }
            }
            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FileExistsAttribute : ValidationAttribute
    {
        public bool Required { get; set; } = true;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string path)
            {
                if (string.IsNullOrWhiteSpace(path) && !Required)
                    return ValidationResult.Success;

                if (!File.Exists(path))
                {
                    return new ValidationResult($"File not found: {path}");
                }
            }
            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DirectoryExistsAttribute : ValidationAttribute
    {
        public bool CreateIfMissing { get; set; } = false;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                if (!Directory.Exists(path))
                {
                    if (CreateIfMissing)
                    {
                        try
                        {
                            Directory.CreateDirectory(path);
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to create directory {path}: {ex.Message}");
                        }
                    }
                    else
                    {
                        return new ValidationResult($"Directory not found: {path}");
                    }
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Enhanced server options with validation
    /// </summary>
    public class ValidatedMcpServerOptions : McpServerOptions, IValidatableObject
    {
        [Required(ErrorMessage = "Host is required")]
        [ValidHost]
        public new string Host { get; set; } = "127.0.0.1";

        [Required]
        [ValidPort]
        public new int Port { get; set; } = 8080;

        [FileExists(Required = false)]
        public new string CertificatePath { get; set; }

        [DirectoryExists(CreateIfMissing = true)]
        public new string RevocationCachePath { get; set; } = "./certs/revocation";

        [DirectoryExists(CreateIfMissing = true)]
        public new string CertificatePinStoragePath { get; set; } = "./certs/pins";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // TLS-specific validation
            if (UseTls)
            {
                if (string.IsNullOrWhiteSpace(CertificatePath) && 
                    string.IsNullOrWhiteSpace(CertificateThumbprint))
                {
                    results.Add(new ValidationResult(
                        "When TLS is enabled, either CertificatePath or CertificateThumbprint must be specified",
                        new[] { nameof(CertificatePath), nameof(CertificateThumbprint) }));
                }

                if (!string.IsNullOrWhiteSpace(CertificatePath))
                {
                    // Validate certificate can be loaded
                    try
                    {
                        using var cert = new X509Certificate2(CertificatePath, CertificatePassword);
                        
                        // Check if certificate is valid
                        if (cert.NotAfter < DateTime.Now)
                        {
                            results.Add(new ValidationResult(
                                $"Certificate has expired on {cert.NotAfter}",
                                new[] { nameof(CertificatePath) }));
                        }
                        else if (cert.NotAfter < DateTime.Now.AddDays(30))
                        {
                            results.Add(new ValidationResult(
                                $"Warning: Certificate will expire soon on {cert.NotAfter}",
                                new[] { nameof(CertificatePath) }));
                        }

                        if (!cert.HasPrivateKey)
                        {
                            results.Add(new ValidationResult(
                                "Server certificate must have a private key",
                                new[] { nameof(CertificatePath) }));
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ValidationResult(
                            $"Failed to load certificate: {ex.Message}",
                            new[] { nameof(CertificatePath) }));
                    }
                }

                // Validate client certificates if required
                if (RequireClientCertificate && AllowedClientCertificateThumbprints.Count == 0)
                {
                    results.Add(new ValidationResult(
                        "When client certificates are required, at least one allowed thumbprint should be specified",
                        new[] { nameof(AllowedClientCertificateThumbprints) }));
                }
            }

            // Authentication validation
            if (EnableAuthentication)
            {
                if (string.IsNullOrWhiteSpace(JwtAuth?.SecretKey))
                {
                    results.Add(new ValidationResult(
                        "JWT secret key is required when authentication is enabled",
                        new[] { "JwtAuth.SecretKey" }));
                }

                if (JwtAuth?.SecretKey?.Length < 32)
                {
                    results.Add(new ValidationResult(
                        "JWT secret key should be at least 32 characters for security",
                        new[] { "JwtAuth.SecretKey" }));
                }
            }

            // Rate limiting validation
            if (RateLimit.Enabled)
            {
                if (RateLimit.RequestsPerMinute <= 0)
                {
                    results.Add(new ValidationResult(
                        "RequestsPerMinute must be greater than 0 when rate limiting is enabled",
                        new[] { "RateLimit.RequestsPerMinute" }));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Configuration validator service
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ILogger<ConfigurationValidator> _logger;

        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger;
        }

        public ValidationResult ValidateConfiguration<T>(T configuration) where T : class
        {
            var context = new ValidationContext(configuration);
            var results = new List<ValidationResult>();

            // Validate data annotations
            if (!Validator.TryValidateObject(configuration, context, results, true))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = results.Select(r => new ValidationError
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

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

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

        public class ValidationError
        {
            public string Message { get; set; }
            public IEnumerable<string> MemberNames { get; set; } = Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Options setup with validation
    /// </summary>
    public class ValidatingOptionsSetup<TOptions> : IConfigureOptions<TOptions>, IPostConfigureOptions<TOptions>
        where TOptions : class
    {
        private readonly IConfiguration _configuration;
        private readonly string _configSection;
        private readonly ConfigurationValidator _validator;
        private readonly ILogger<ValidatingOptionsSetup<TOptions>> _logger;

        public ValidatingOptionsSetup(
            IConfiguration configuration,
            string configSection,
            ConfigurationValidator validator,
            ILogger<ValidatingOptionsSetup<TOptions>> logger)
        {
            _configuration = configuration;
            _configSection = configSection;
            _validator = validator;
            _logger = logger;
        }

        public void Configure(TOptions options)
        {
            _configuration.GetSection(_configSection).Bind(options);
        }

        public void PostConfigure(string name, TOptions options)
        {
            var result = _validator.ValidateConfiguration(options);
            
            if (!result.IsValid)
            {
                _logger.LogError("Configuration validation failed for {OptionsType}", typeof(TOptions).Name);
                result.LogErrors(_logger);
                
                throw new OptionsValidationException(
                    typeof(TOptions).Name,
                    typeof(TOptions),
                    result.Errors.Select(e => e.Message));
            }
        }
    }

    /// <summary>
    /// Extension methods for configuration validation
    /// </summary>
    public static class ConfigurationValidationExtensions
    {
        public static IServiceCollection AddValidatedOptions<TOptions>(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSection) where TOptions : class
        {
            services.AddSingleton<ConfigurationValidator>();
            
            services.AddSingleton<IConfigureOptions<TOptions>>(sp =>
                new ValidatingOptionsSetup<TOptions>(
                    configuration,
                    configSection,
                    sp.GetRequiredService<ConfigurationValidator>(),
                    sp.GetRequiredService<ILogger<ValidatingOptionsSetup<TOptions>>>()));

            services.AddSingleton<IPostConfigureOptions<TOptions>>(sp =>
                new ValidatingOptionsSetup<TOptions>(
                    configuration,
                    configSection,
                    sp.GetRequiredService<ConfigurationValidator>(),
                    sp.GetRequiredService<ILogger<ValidatingOptionsSetup<TOptions>>>()));

            return services;
        }

        public static IServiceCollection AddMcpServerWithValidation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add validated options
            services.AddValidatedOptions<ValidatedMcpServerOptions>(configuration, "McpServer");

            // Add the server
            services.AddSingleton<IMcpServer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ValidatedMcpServerOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                
                return new McpServer(
                    Options.Create((McpServerOptions)options),
                    logger);
            });

            return services;
        }
    }

    /// <summary>
    /// Startup configuration validator
    /// </summary>
    public static class StartupConfigurationValidator
    {
        public static void ValidateConfiguration(IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Validating configuration...");

            var errors = new List<string>();

            // Check required sections
            if (!configuration.GetSection("McpServer").Exists())
            {
                errors.Add("Required configuration section 'McpServer' is missing");
            }

            // Validate environment
            var environment = configuration["Environment"];
            if (string.IsNullOrEmpty(environment))
            {
                logger.LogWarning("Environment not specified, defaulting to Production");
            }

            // Validate logging configuration
            var logLevel = configuration["Logging:LogLevel:Default"];
            if (string.IsNullOrEmpty(logLevel))
            {
                logger.LogWarning("Default log level not specified");
            }

            // Production-specific checks
            if (environment?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true)
            {
                var useTls = configuration.GetValue<bool>("McpServer:UseTls");
                if (!useTls)
                {
                    errors.Add("TLS must be enabled in Production environment");
                }

                var allowUntrusted = configuration.GetValue<bool>("McpServer:AllowUntrustedCertificates");
                if (allowUntrusted)
                {
                    errors.Add("Untrusted certificates must not be allowed in Production");
                }
            }

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    logger.LogError("Configuration error: {Error}", error);
                }
                
                throw new ConfigurationException("Configuration validation failed. See logs for details.");
            }

            logger.LogInformation("Configuration validation completed successfully");
        }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
    }
}