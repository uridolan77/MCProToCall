using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Zero-trust security validation pipeline
    /// </summary>
    public class ZeroTrustSecurityPipeline
    {
        private readonly List<ISecurityValidator> _validators;
        private readonly ILogger<ZeroTrustSecurityPipeline> _logger;
        private readonly ConcurrentDictionary<string, SecurityMetrics> _metrics;

        public ZeroTrustSecurityPipeline(
            IEnumerable<ISecurityValidator> validators,
            ILogger<ZeroTrustSecurityPipeline> logger)
        {
            _validators = validators.OrderBy(v => v.Priority).ToList();
            _logger = logger;
            _metrics = new ConcurrentDictionary<string, SecurityMetrics>();
        }

        /// <summary>
        /// Validates a request through the security pipeline
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Security validation result</returns>
        public async Task<SecurityValidationResult> ValidateRequestAsync(
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var result = new SecurityValidationResult
            {
                RequestId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                foreach (var validator in _validators)
                {
                    var validatorName = validator.GetType().Name;
                    var validatorStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        var validationResult = await validator.ValidateAsync(context, cancellationToken);

                        validatorStopwatch.Stop();
                        UpdateMetrics(validatorName, validatorStopwatch.ElapsedMilliseconds, true);

                        if (!validationResult.IsValid)
                        {
                            result.Violations.Add(validationResult.Violation);
                            result.FailedValidators.Add(validatorName);

                            _logger.LogWarning(
                                "Security validation failed for {Validator}: {Violation} (Request: {RequestId})",
                                validatorName, validationResult.Violation.Description, context.TraceIdentifier);

                            if (validationResult.IsCritical)
                            {
                                result.ShouldBlock = true;
                                result.BlockReason = $"Critical security violation in {validatorName}: {validationResult.Violation.Description}";
                                break;
                            }
                        }
                        else
                        {
                            result.PassedValidators.Add(validatorName);
                        }
                    }
                    catch (Exception ex)
                    {
                        validatorStopwatch.Stop();
                        UpdateMetrics(validatorName, validatorStopwatch.ElapsedMilliseconds, false);

                        _logger.LogError(ex,
                            "Security validator {Validator} threw an exception (Request: {RequestId})",
                            validatorName, context.TraceIdentifier);

                        result.Violations.Add(new SecurityViolation
                        {
                            Type = SecurityViolationType.ValidationError,
                            Severity = SecurityViolationSeverity.High,
                            Description = $"Validator {validatorName} failed with exception: {ex.Message}",
                            Source = validatorName
                        });

                        // Treat validator exceptions as critical failures
                        result.ShouldBlock = true;
                        result.BlockReason = $"Security validator {validatorName} failed";
                        break;
                    }
                }

                stopwatch.Stop();
                result.ValidationDurationMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation(
                    "Security validation completed for request {RequestId}: {Result} in {Duration}ms",
                    context.TraceIdentifier,
                    result.ShouldBlock ? "BLOCKED" : "ALLOWED",
                    result.ValidationDurationMs);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Security pipeline failed for request {RequestId}", context.TraceIdentifier);

                return new SecurityValidationResult
                {
                    RequestId = context.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    ShouldBlock = true,
                    BlockReason = "Security pipeline failure",
                    ValidationDurationMs = stopwatch.ElapsedMilliseconds,
                    Violations = { new SecurityViolation
                    {
                        Type = SecurityViolationType.SystemError,
                        Severity = SecurityViolationSeverity.Critical,
                        Description = $"Security pipeline failed: {ex.Message}",
                        Source = "SecurityPipeline"
                    }}
                };
            }
        }

        /// <summary>
        /// Gets security metrics for monitoring
        /// </summary>
        /// <returns>Security metrics</returns>
        public SecurityPipelineMetrics GetMetrics()
        {
            return new SecurityPipelineMetrics
            {
                ValidatorMetrics = _metrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SecurityValidatorMetrics
                    {
                        TotalRequests = kvp.Value.TotalRequests,
                        SuccessfulRequests = kvp.Value.SuccessfulRequests,
                        FailedRequests = kvp.Value.FailedRequests,
                        AverageResponseTimeMs = kvp.Value.TotalResponseTimeMs / Math.Max(1, kvp.Value.TotalRequests),
                        SuccessRate = (double)kvp.Value.SuccessfulRequests / Math.Max(1, kvp.Value.TotalRequests)
                    })
            };
        }

        private void UpdateMetrics(string validatorName, long responseTimeMs, bool success)
        {
            _metrics.AddOrUpdate(validatorName,
                new SecurityMetrics
                {
                    TotalRequests = 1,
                    SuccessfulRequests = success ? 1 : 0,
                    FailedRequests = success ? 0 : 1,
                    TotalResponseTimeMs = responseTimeMs
                },
                (key, existing) => new SecurityMetrics
                {
                    TotalRequests = existing.TotalRequests + 1,
                    SuccessfulRequests = existing.SuccessfulRequests + (success ? 1 : 0),
                    FailedRequests = existing.FailedRequests + (success ? 0 : 1),
                    TotalResponseTimeMs = existing.TotalResponseTimeMs + responseTimeMs
                });
        }

        private class SecurityMetrics
        {
            public long TotalRequests { get; set; }
            public long SuccessfulRequests { get; set; }
            public long FailedRequests { get; set; }
            public long TotalResponseTimeMs { get; set; }
        }
    }

    /// <summary>
    /// Interface for security validators
    /// </summary>
    public interface ISecurityValidator
    {
        /// <summary>
        /// Priority of the validator (lower numbers run first)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Validates the security of a request
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        Task<SecurityValidationResult> ValidateAsync(HttpContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Security validation result
    /// </summary>
    public class SecurityValidationResult
    {
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsValid { get; set; } = true;
        public bool ShouldBlock { get; set; }
        public string BlockReason { get; set; } = string.Empty;
        public long ValidationDurationMs { get; set; }
        public List<SecurityViolation> Violations { get; set; } = new();
        public List<string> PassedValidators { get; set; } = new();
        public List<string> FailedValidators { get; set; } = new();
        public bool IsCritical { get; set; }
        public SecurityViolation Violation { get; set; } = new();
    }

    /// <summary>
    /// Security violation details
    /// </summary>
    public class SecurityViolation
    {
        public SecurityViolationType Type { get; set; }
        public SecurityViolationSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of security violations
    /// </summary>
    public enum SecurityViolationType
    {
        Authentication,
        Authorization,
        RateLimiting,
        InputValidation,
        CertificateValidation,
        IpBlocking,
        ValidationError,
        SystemError
    }

    /// <summary>
    /// Severity levels for security violations
    /// </summary>
    public enum SecurityViolationSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Security pipeline metrics
    /// </summary>
    public class SecurityPipelineMetrics
    {
        public Dictionary<string, SecurityValidatorMetrics> ValidatorMetrics { get; set; } = new();
    }

    /// <summary>
    /// Metrics for individual security validators
    /// </summary>
    public class SecurityValidatorMetrics
    {
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
    }
}
