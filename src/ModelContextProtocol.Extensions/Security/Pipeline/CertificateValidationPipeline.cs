using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security.Pipeline
{
    /// <summary>
    /// Pipeline for certificate validation with multiple steps
    /// </summary>
    public class CertificateValidationPipeline : ICertificateValidationPipeline
    {
        private readonly IEnumerable<ICertificateValidationStep> _steps;
        private readonly ILogger<CertificateValidationPipeline> _logger;
        private readonly TlsOptions _tlsOptions;

        public CertificateValidationPipeline(
            IEnumerable<ICertificateValidationStep> steps,
            ILogger<CertificateValidationPipeline> logger,
            IOptions<TlsOptions> tlsOptions)
        {
            _steps = steps?.OrderBy(s => s.Order) ?? throw new ArgumentNullException(nameof(steps));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tlsOptions = tlsOptions?.Value ?? throw new ArgumentNullException(nameof(tlsOptions));
        }

        /// <summary>
        /// Validates a certificate through the pipeline
        /// </summary>
        public async Task<CertificateValidationResult> ValidateAsync(
            X509Certificate2 certificate,
            CertificateValidationContext context,
            CancellationToken cancellationToken = default)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var result = new CertificateValidationResult
            {
                Certificate = certificate,
                Context = context,
                ValidationStartTime = DateTime.UtcNow
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("Starting certificate validation pipeline for certificate {Thumbprint}", 
                    certificate.Thumbprint);

                foreach (var step in _steps)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Validation cancelled";
                        break;
                    }

                    var stepStopwatch = Stopwatch.StartNew();
                    
                    try
                    {
                        _logger.LogDebug("Executing validation step: {StepName}", step.StepName);
                        
                        var stepResult = await step.ValidateAsync(certificate, context, cancellationToken);
                        stepResult.Duration = stepStopwatch.Elapsed;
                        
                        result.StepResults.Add(step.StepName, stepResult);

                        if (!stepResult.IsValid)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Validation failed at step '{step.StepName}': {stepResult.ErrorMessage}";
                            
                            _logger.LogWarning("Certificate validation failed at step {StepName}: {ErrorMessage}", 
                                step.StepName, stepResult.ErrorMessage);
                            break;
                        }

                        if (!string.IsNullOrEmpty(stepResult.WarningMessage))
                        {
                            result.Warnings.Add($"{step.StepName}: {stepResult.WarningMessage}");
                            _logger.LogWarning("Certificate validation warning at step {StepName}: {WarningMessage}", 
                                step.StepName, stepResult.WarningMessage);
                        }

                        if (stepResult.StopPipeline)
                        {
                            _logger.LogDebug("Pipeline stopped at step {StepName} as requested", step.StepName);
                            break;
                        }

                        _logger.LogDebug("Validation step {StepName} completed successfully in {Duration}ms", 
                            step.StepName, stepResult.Duration.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        stepStopwatch.Stop();
                        
                        var stepResult = ValidationStepResult.Failure($"Exception in validation step: {ex.Message}");
                        stepResult.Duration = stepStopwatch.Elapsed;
                        result.StepResults.Add(step.StepName, stepResult);

                        result.IsValid = false;
                        result.ErrorMessage = $"Exception in validation step '{step.StepName}': {ex.Message}";
                        
                        _logger.LogError(ex, "Exception in certificate validation step {StepName}", step.StepName);
                        break;
                    }
                    finally
                    {
                        stepStopwatch.Stop();
                    }
                }

                // If we completed all steps without failure, validation is successful
                if (result.IsValid == null && !result.StepResults.Values.Any(r => !r.IsValid))
                {
                    result.IsValid = true;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Pipeline execution failed: {ex.Message}";
                _logger.LogError(ex, "Certificate validation pipeline failed");
            }
            finally
            {
                stopwatch.Stop();
                result.TotalDuration = stopwatch.Elapsed;
                result.ValidationEndTime = DateTime.UtcNow;

                _logger.LogDebug("Certificate validation pipeline completed in {Duration}ms. Result: {IsValid}", 
                    result.TotalDuration.TotalMilliseconds, result.IsValid);
            }

            return result;
        }
    }

    /// <summary>
    /// Interface for certificate validation pipeline
    /// </summary>
    public interface ICertificateValidationPipeline
    {
        /// <summary>
        /// Validates a certificate through the pipeline
        /// </summary>
        Task<CertificateValidationResult> ValidateAsync(
            X509Certificate2 certificate,
            CertificateValidationContext context,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of certificate validation pipeline
    /// </summary>
    public class CertificateValidationResult
    {
        /// <summary>
        /// The certificate that was validated
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// The validation context
        /// </summary>
        public CertificateValidationContext Context { get; set; }

        /// <summary>
        /// Whether the certificate is valid
        /// </summary>
        public bool? IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// List of warnings from validation steps
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Results from individual validation steps
        /// </summary>
        public Dictionary<string, ValidationStepResult> StepResults { get; set; } = new Dictionary<string, ValidationStepResult>();

        /// <summary>
        /// Total time taken for validation
        /// </summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>
        /// When validation started
        /// </summary>
        public DateTime ValidationStartTime { get; set; }

        /// <summary>
        /// When validation ended
        /// </summary>
        public DateTime ValidationEndTime { get; set; }
    }
}
