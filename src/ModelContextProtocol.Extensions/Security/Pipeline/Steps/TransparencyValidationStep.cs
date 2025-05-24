using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Pipeline.Steps
{
    /// <summary>
    /// Validates certificate transparency
    /// </summary>
    public class TransparencyValidationStep : ICertificateValidationStep
    {
        private readonly ICertificateTransparencyVerifier _transparencyVerifier;
        private readonly ILogger<TransparencyValidationStep> _logger;

        public string StepName => "TransparencyValidation";
        public int Order => 4; // Run after revocation check

        public TransparencyValidationStep(
            ICertificateTransparencyVerifier transparencyVerifier,
            ILogger<TransparencyValidationStep> logger)
        {
            _transparencyVerifier = transparencyVerifier ?? throw new ArgumentNullException(nameof(transparencyVerifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate, 
            CertificateValidationContext context, 
            CancellationToken cancellationToken)
        {
            // Skip CT validation if disabled
            if (!context.TlsOptions.CertificateTransparencyOptions.VerifyCertificateTransparency)
            {
                _logger.LogDebug("Certificate Transparency validation is disabled");
                return ValidationStepResult.Success("Certificate Transparency validation disabled", new Dictionary<string, object>
                {
                    ["CtValidationEnabled"] = false,
                    ["Status"] = "Skipped"
                });
            }

            try
            {
                _logger.LogDebug("Validating Certificate Transparency for {Thumbprint}", certificate.Thumbprint);

                var startTime = DateTime.UtcNow;
                var metadata = new Dictionary<string, object>
                {
                    ["CtValidationEnabled"] = true
                };

                // Check for embedded SCTs if required
                if (context.TlsOptions.CertificateTransparencyOptions.RequireEmbeddedScts)
                {
                    var hasEmbeddedScts = _transparencyVerifier.HasEmbeddedScts(certificate);
                    metadata["HasEmbeddedScts"] = hasEmbeddedScts;

                    if (!hasEmbeddedScts)
                    {
                        var message = "Certificate does not contain required embedded SCTs";
                        _logger.LogWarning("Certificate Transparency validation failed: {Message}", message);
                        
                        metadata["Status"] = "NoEmbeddedScts";
                        return ValidationStepResult.Failure(message, metadata);
                    }

                    _logger.LogDebug("Certificate contains embedded SCTs");
                }

                // Verify certificate in CT logs
                bool isInCtLogs;
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(context.TlsOptions.CertificateTransparencyOptions.CtQueryTimeoutSeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    isInCtLogs = await _transparencyVerifier.VerifyCertificateInCtLogsAsync(certificate);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // Re-throw if it's the main cancellation token
                }
                catch (OperationCanceledException)
                {
                    var message = $"Certificate Transparency verification timed out after {context.TlsOptions.CertificateTransparencyOptions.CtQueryTimeoutSeconds} seconds";
                    _logger.LogWarning("Certificate Transparency verification timed out: {Message}", message);

                    metadata["Status"] = "Timeout";
                    metadata["TimeoutSeconds"] = context.TlsOptions.CertificateTransparencyOptions.CtQueryTimeoutSeconds;

                    if (context.TlsOptions.CertificateTransparencyOptions.AllowWhenCtUnavailable)
                    {
                        return ValidationStepResult.Warning($"CT verification timed out, allowing due to policy: {message}", metadata);
                    }
                    else
                    {
                        return ValidationStepResult.Failure(message, metadata);
                    }
                }

                var checkDuration = DateTime.UtcNow - startTime;
                metadata["CheckDurationMs"] = checkDuration.TotalMilliseconds;

                if (!isInCtLogs)
                {
                    var message = "Certificate not found in Certificate Transparency logs";
                    _logger.LogWarning("Certificate Transparency validation failed: {Message}", message);
                    
                    metadata["Status"] = "NotInCtLogs";

                    if (context.TlsOptions.CertificateTransparencyOptions.AllowWhenCtUnavailable)
                    {
                        return ValidationStepResult.Warning($"Certificate not in CT logs, allowing due to policy: {message}", metadata);
                    }
                    else
                    {
                        return ValidationStepResult.Failure(message, metadata);
                    }
                }

                _logger.LogDebug("Certificate Transparency validation passed in {Duration}ms", checkDuration.TotalMilliseconds);

                metadata["Status"] = "Valid";
                metadata["MinimumCtLogCount"] = context.TlsOptions.CertificateTransparencyOptions.MinimumCtLogCount;
                metadata["MinimumSctCount"] = context.TlsOptions.CertificateTransparencyOptions.MinimumSctCount;

                return ValidationStepResult.Success(null, metadata);
            }
            catch (Exception ex)
            {
                var message = $"Error during Certificate Transparency validation: {ex.Message}";
                _logger.LogError(ex, "Certificate Transparency validation failed: {Message}", message);

                var metadata = new Dictionary<string, object>
                {
                    ["CtValidationEnabled"] = true,
                    ["Status"] = "Error",
                    ["ErrorType"] = ex.GetType().Name
                };

                if (context.TlsOptions.CertificateTransparencyOptions.AllowWhenCtUnavailable)
                {
                    return ValidationStepResult.Warning($"CT validation failed, allowing due to policy: {message}", metadata);
                }
                else
                {
                    return ValidationStepResult.Failure(message, metadata);
                }
            }
        }
    }
}
