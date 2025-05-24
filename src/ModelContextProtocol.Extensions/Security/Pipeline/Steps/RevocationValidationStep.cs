using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Pipeline.Steps
{
    /// <summary>
    /// Validates certificate revocation status
    /// </summary>
    public class RevocationValidationStep : ICertificateValidationStep
    {
        private readonly ICertificateRevocationChecker _revocationChecker;
        private readonly ILogger<RevocationValidationStep> _logger;

        public string StepName => "RevocationValidation";
        public int Order => 3; // Run after basic checks

        public RevocationValidationStep(
            ICertificateRevocationChecker revocationChecker,
            ILogger<RevocationValidationStep> logger)
        {
            _revocationChecker = revocationChecker ?? throw new ArgumentNullException(nameof(revocationChecker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate,
            CertificateValidationContext context,
            CancellationToken cancellationToken)
        {
            // Skip revocation checking if disabled
            if (!context.TlsOptions.RevocationOptions.CheckRevocation)
            {
                _logger.LogDebug("Certificate revocation checking is disabled");
                return ValidationStepResult.Success("Revocation checking disabled", new Dictionary<string, object>
                {
                    ["RevocationCheckEnabled"] = false,
                    ["Status"] = "Skipped"
                });
            }

            try
            {
                _logger.LogDebug("Checking certificate revocation status for {Thumbprint}", certificate.Thumbprint);

                var startTime = DateTime.UtcNow;
                bool isRevoked;

                // Use timeout for revocation checking
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(context.TlsOptions.RevocationOptions.RevocationCheckTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    var isNotRevoked = _revocationChecker.ValidateCertificateNotRevoked(certificate);
                    isRevoked = !isNotRevoked; // ValidateCertificateNotRevoked returns true if NOT revoked
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    var message = $"Revocation check timed out after {context.TlsOptions.RevocationOptions.RevocationCheckTimeoutSeconds} seconds";
                    _logger.LogWarning("Certificate revocation check timed out: {Message}", message);

                    // Fail closed or open based on configuration
                    if (context.TlsOptions.RevocationOptions.FailClosed)
                    {
                        return ValidationStepResult.Failure(message, new Dictionary<string, object>
                        {
                            ["RevocationCheckEnabled"] = true,
                            ["Status"] = "Timeout",
                            ["FailClosed"] = true,
                            ["TimeoutSeconds"] = context.TlsOptions.RevocationOptions.RevocationCheckTimeoutSeconds
                        });
                    }
                    else
                    {
                        return ValidationStepResult.Warning($"Revocation check timed out, allowing due to fail-open policy: {message}",
                            new Dictionary<string, object>
                            {
                                ["RevocationCheckEnabled"] = true,
                                ["Status"] = "TimeoutAllowed",
                                ["FailClosed"] = false,
                                ["TimeoutSeconds"] = context.TlsOptions.RevocationOptions.RevocationCheckTimeoutSeconds
                            });
                    }
                }

                var checkDuration = DateTime.UtcNow - startTime;

                if (isRevoked)
                {
                    var message = "Certificate has been revoked";
                    _logger.LogWarning("Certificate revocation validation failed: {Message}", message);

                    return ValidationStepResult.Failure(message, new Dictionary<string, object>
                    {
                        ["RevocationCheckEnabled"] = true,
                        ["Status"] = "Revoked",
                        ["CheckDurationMs"] = checkDuration.TotalMilliseconds
                    });
                }

                _logger.LogDebug("Certificate revocation validation passed in {Duration}ms", checkDuration.TotalMilliseconds);

                return ValidationStepResult.Success(null, new Dictionary<string, object>
                {
                    ["RevocationCheckEnabled"] = true,
                    ["Status"] = "NotRevoked",
                    ["CheckDurationMs"] = checkDuration.TotalMilliseconds,
                    ["UseOcsp"] = context.TlsOptions.RevocationOptions.UseOcsp,
                    ["UseCrl"] = context.TlsOptions.RevocationOptions.UseCrl,
                    ["UseOcspStapling"] = context.TlsOptions.RevocationOptions.UseOcspStapling
                });
            }
            catch (Exception ex)
            {
                var message = $"Error during revocation check: {ex.Message}";
                _logger.LogError(ex, "Certificate revocation check failed: {Message}", message);

                // Fail closed or open based on configuration
                if (context.TlsOptions.RevocationOptions.FailClosed)
                {
                    return ValidationStepResult.Failure(message, new Dictionary<string, object>
                    {
                        ["RevocationCheckEnabled"] = true,
                        ["Status"] = "Error",
                        ["FailClosed"] = true,
                        ["ErrorType"] = ex.GetType().Name
                    });
                }
                else
                {
                    return ValidationStepResult.Warning($"Revocation check failed, allowing due to fail-open policy: {message}",
                        new Dictionary<string, object>
                        {
                            ["RevocationCheckEnabled"] = true,
                            ["Status"] = "ErrorAllowed",
                            ["FailClosed"] = false,
                            ["ErrorType"] = ex.GetType().Name
                        });
                }
            }
        }
    }
}
