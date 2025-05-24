using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Pipeline.Steps
{
    /// <summary>
    /// Validates certificate pinning
    /// </summary>
    public class PinningValidationStep : ICertificateValidationStep
    {
        private readonly ICertificatePinningService _pinningService;
        private readonly ILogger<PinningValidationStep> _logger;

        public string StepName => "PinningValidation";
        public int Order => 5; // Run after CT validation

        public PinningValidationStep(
            ICertificatePinningService pinningService,
            ILogger<PinningValidationStep> logger)
        {
            _pinningService = pinningService ?? throw new ArgumentNullException(nameof(pinningService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate, 
            CertificateValidationContext context, 
            CancellationToken cancellationToken)
        {
            // Skip pinning validation if disabled
            if (!context.TlsOptions.CertificatePinning.Enabled)
            {
                _logger.LogDebug("Certificate pinning is disabled");
                return ValidationStepResult.Success("Certificate pinning disabled", new Dictionary<string, object>
                {
                    ["PinningEnabled"] = false,
                    ["Status"] = "Skipped"
                });
            }

            try
            {
                _logger.LogDebug("Validating certificate pinning for {Thumbprint}", certificate.Thumbprint);

                var startTime = DateTime.UtcNow;
                var metadata = new Dictionary<string, object>
                {
                    ["PinningEnabled"] = true,
                    ["AutoPinFirstCertificate"] = context.TlsOptions.CertificatePinning.AutoPinFirstCertificate,
                    ["AllowSelfSignedIfPinned"] = context.TlsOptions.CertificatePinning.AllowSelfSignedIfPinned
                };

                // Check if certificate matches pinned certificates
                var isPinned = await _pinningService.ValidatePinAsync(certificate);
                var checkDuration = DateTime.UtcNow - startTime;
                metadata["CheckDurationMs"] = checkDuration.TotalMilliseconds;

                if (!isPinned)
                {
                    // Check if we should auto-pin the first certificate
                    if (context.TlsOptions.CertificatePinning.AutoPinFirstCertificate)
                    {
                        try
                        {
                            // Check if there are any existing pinned certificates
                            var hasPinnedCertificates = context.TlsOptions.CertificatePinning.PinnedCertificates?.Count > 0;
                            
                            if (!hasPinnedCertificates)
                            {
                                _logger.LogInformation("Auto-pinning first certificate {Thumbprint}", certificate.Thumbprint);
                                
                                // Auto-pin this certificate
                                await _pinningService.PinCertificateAsync(certificate);
                                
                                metadata["Status"] = "AutoPinned";
                                return ValidationStepResult.Success("Certificate auto-pinned as first certificate", metadata);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to auto-pin certificate {Thumbprint}", certificate.Thumbprint);
                            // Continue with normal pinning validation failure
                        }
                    }

                    var message = "Certificate does not match any pinned certificates";
                    _logger.LogWarning("Certificate pinning validation failed: {Message}", message);
                    
                    metadata["Status"] = "NotPinned";
                    metadata["PinnedCertificatesCount"] = context.TlsOptions.CertificatePinning.PinnedCertificates?.Count ?? 0;
                    
                    return ValidationStepResult.Failure(message, metadata);
                }

                _logger.LogDebug("Certificate pinning validation passed in {Duration}ms", checkDuration.TotalMilliseconds);

                metadata["Status"] = "Pinned";
                metadata["PinnedCertificatesCount"] = context.TlsOptions.CertificatePinning.PinnedCertificates?.Count ?? 0;

                return ValidationStepResult.Success(null, metadata);
            }
            catch (Exception ex)
            {
                var message = $"Error during certificate pinning validation: {ex.Message}";
                _logger.LogError(ex, "Certificate pinning validation failed: {Message}", message);

                return ValidationStepResult.Failure(message, new Dictionary<string, object>
                {
                    ["PinningEnabled"] = true,
                    ["Status"] = "Error",
                    ["ErrorType"] = ex.GetType().Name
                });
            }
        }
    }
}
