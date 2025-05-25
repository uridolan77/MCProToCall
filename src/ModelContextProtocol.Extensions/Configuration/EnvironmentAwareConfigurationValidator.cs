using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Extensions.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Environment-aware configuration validator that applies different rules based on the hosting environment
    /// </summary>
    /// <typeparam name="T">The options type to validate</typeparam>
    public class EnvironmentAwareConfigurationValidator<T> : IValidateOptions<T> where T : class
    {
        private readonly IHostEnvironment _environment;

        public EnvironmentAwareConfigurationValidator(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public ValidateOptionsResult Validate(string name, T options)
        {
            var failures = new List<string>();

            // Apply type-specific validation rules
            if (options is TlsOptions tlsOptions)
            {
                ValidateTlsOptions(tlsOptions, failures);
            }
            else if (options is SecurityOptions securityOptions)
            {
                ValidateSecurityOptions(securityOptions, failures);
            }
            else if (options is ObservabilityOptions observabilityOptions)
            {
                ValidateObservabilityOptions(observabilityOptions, failures);
            }

            // Apply generic validation using data annotations
            var validationContext = new ValidationContext(options);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
            {
                foreach (var result in validationResults)
                {
                    failures.Add(result.ErrorMessage);
                }
            }

            return failures.Count > 0
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }

        private void ValidateTlsOptions(TlsOptions options, List<string> failures)
        {
            // Environment-specific validation rules for TLS
            if (_environment.IsProduction())
            {
                if (options.AllowUntrustedCertificates)
                    failures.Add("Untrusted certificates must not be allowed in production");

                if (options.AllowSelfSignedCertificates)
                    failures.Add("Self-signed certificates must not be allowed in production");

                if (!options.UseTls)
                    failures.Add("TLS must be enabled in production");

                // Certificate pinning validation would go here if the property existed
            }

            if (_environment.IsDevelopment())
            {
                // Development-specific warnings (logged but not failed)
                if (!options.AllowUntrustedCertificates)
                    Console.WriteLine("Warning: Consider allowing untrusted certificates in development");

                // Certificate pinning warnings would go here if the property existed
            }

            if (_environment.IsStaging())
            {
                // Staging should be as close to production as possible
                if (options.AllowUntrustedCertificates)
                    failures.Add("Untrusted certificates should not be allowed in staging");

                if (!options.UseTls)
                    failures.Add("TLS should be enabled in staging");
            }
        }

        private void ValidateSecurityOptions(SecurityOptions options, List<string> failures)
        {
            if (_environment.IsProduction())
            {
                if (!options.EnableCertificateValidation)
                    failures.Add("Certificate validation must be enabled in production");

                if (!options.EnableRevocationChecking)
                    failures.Add("Certificate revocation checking must be enabled in production");

                if (options.EnableHsm && string.IsNullOrEmpty(options.HsmConnectionString))
                    failures.Add("HSM connection string is required when HSM is enabled in production");
            }

            if (_environment.IsDevelopment())
            {
                if (options.EnableHsm && string.IsNullOrEmpty(options.HsmConnectionString))
                    Console.WriteLine("Warning: HSM is enabled but no connection string provided");
            }
        }

        private void ValidateObservabilityOptions(ObservabilityOptions options, List<string> failures)
        {
            if (_environment.IsProduction())
            {
                if (!options.EnableMetrics)
                    failures.Add("Metrics must be enabled in production");

                if (!options.EnableTracing)
                    failures.Add("Tracing must be enabled in production");

                if (!options.EnableHealthChecks)
                    failures.Add("Health checks must be enabled in production");

                if (string.IsNullOrEmpty(options.ServiceName))
                    failures.Add("Service name must be specified in production");
            }

            if (_environment.IsStaging())
            {
                if (!options.EnableMetrics)
                    failures.Add("Metrics should be enabled in staging");

                if (!options.EnableHealthChecks)
                    failures.Add("Health checks should be enabled in staging");
            }
        }
    }

    /// <summary>
    /// Configuration options for observability features
    /// </summary>
    public class ObservabilityOptions
    {
        [Required]
        public string ServiceName { get; set; } = "MCP-Service";

        public bool EnableMetrics { get; set; } = true;
        public bool EnableTracing { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public bool EnableHealthChecks { get; set; } = true;

        [Range(1, 3600)]
        public int MetricsIntervalSeconds { get; set; } = 60;

        [Range(0.0, 1.0)]
        public double TracingSampleRate { get; set; } = 1.0;

        public string[] MetricsEndpoints { get; set; } = Array.Empty<string>();
        public string[] TracingEndpoints { get; set; } = Array.Empty<string>();

        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Enhanced security options with validation attributes
    /// </summary>
    public class SecurityOptions
    {
        public bool EnableCertificateValidation { get; set; } = true;
        public bool EnableCertificatePinning { get; set; } = false;
        public bool EnableRevocationChecking { get; set; } = true;
        public bool EnableHsm { get; set; } = false;

        [Url]
        public string HsmConnectionString { get; set; }

        [Required]
        public string HsmProviderType { get; set; } = "AzureKeyVault";

        [Range(1, 300)]
        public int CertificateValidationTimeoutSeconds { get; set; } = 30;

        [Range(1, 10)]
        public int MaxCertificateChainDepth { get; set; } = 5;

        public string[] TrustedCertificateThumbprints { get; set; } = Array.Empty<string>();
        public string[] BlockedCertificateThumbprints { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Environment-aware configuration validation extensions
    /// </summary>
    public static class EnvironmentAwareConfigurationExtensions
    {
        /// <summary>
        /// Adds environment-aware configuration validation
        /// </summary>
        public static IServiceCollection AddEnvironmentAwareValidation<T>(
            this IServiceCollection services) where T : class
        {
            services.AddSingleton<IValidateOptions<T>, EnvironmentAwareConfigurationValidator<T>>();
            return services;
        }

        /// <summary>
        /// Validates configuration at startup
        /// </summary>
        public static IServiceCollection ValidateConfigurationAtStartup<T>(
            this IServiceCollection services,
            string sectionName = null) where T : class
        {
            services.AddOptions<T>(sectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }

        /// <summary>
        /// Adds comprehensive configuration validation
        /// </summary>
        public static IServiceCollection AddComprehensiveConfigurationValidation(
            this IServiceCollection services)
        {
            services.AddEnvironmentAwareValidation<TlsOptions>();
            services.AddEnvironmentAwareValidation<SecurityOptions>();
            services.AddEnvironmentAwareValidation<ObservabilityOptions>();

            services.ValidateConfigurationAtStartup<TlsOptions>("TlsOptions");
            services.ValidateConfigurationAtStartup<SecurityOptions>("SecurityOptions");
            services.ValidateConfigurationAtStartup<ObservabilityOptions>("ObservabilityOptions");

            return services;
        }
    }
}
