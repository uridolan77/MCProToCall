using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Pipeline.Steps
{
    /// <summary>
    /// Validates certificate expiry dates
    /// </summary>
    public class ExpiryValidationStep : ICertificateValidationStep
    {
        private readonly ILogger<ExpiryValidationStep> _logger;

        public string StepName => "ExpiryValidation";
        public int Order => 1; // Run early in the pipeline

        public ExpiryValidationStep(ILogger<ExpiryValidationStep> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate, 
            CertificateValidationContext context, 
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var notBefore = certificate.NotBefore.ToUniversalTime();
            var notAfter = certificate.NotAfter.ToUniversalTime();

            // Check if certificate is not yet valid
            if (now < notBefore)
            {
                var message = $"Certificate is not yet valid. Valid from: {notBefore:yyyy-MM-dd HH:mm:ss} UTC, Current time: {now:yyyy-MM-dd HH:mm:ss} UTC";
                _logger.LogWarning("Certificate expiry validation failed: {Message}", message);
                
                return Task.FromResult(ValidationStepResult.Failure(message, new Dictionary<string, object>
                {
                    ["NotBefore"] = notBefore,
                    ["NotAfter"] = notAfter,
                    ["CurrentTime"] = now,
                    ["Status"] = "NotYetValid"
                }));
            }

            // Check if certificate is expired
            if (now > notAfter)
            {
                var message = $"Certificate has expired. Valid until: {notAfter:yyyy-MM-dd HH:mm:ss} UTC, Current time: {now:yyyy-MM-dd HH:mm:ss} UTC";
                _logger.LogWarning("Certificate expiry validation failed: {Message}", message);
                
                return Task.FromResult(ValidationStepResult.Failure(message, new Dictionary<string, object>
                {
                    ["NotBefore"] = notBefore,
                    ["NotAfter"] = notAfter,
                    ["CurrentTime"] = now,
                    ["Status"] = "Expired"
                }));
            }

            // Check if certificate is expiring soon (within 30 days)
            var daysUntilExpiry = (notAfter - now).TotalDays;
            string warningMessage = null;
            
            if (daysUntilExpiry <= 30)
            {
                warningMessage = $"Certificate expires in {daysUntilExpiry:F1} days on {notAfter:yyyy-MM-dd HH:mm:ss} UTC";
                _logger.LogWarning("Certificate expiring soon: {Message}", warningMessage);
            }

            _logger.LogDebug("Certificate expiry validation passed. Valid from {NotBefore} to {NotAfter}, expires in {DaysUntilExpiry} days", 
                notBefore, notAfter, daysUntilExpiry);

            var metadata = new Dictionary<string, object>
            {
                ["NotBefore"] = notBefore,
                ["NotAfter"] = notAfter,
                ["CurrentTime"] = now,
                ["DaysUntilExpiry"] = daysUntilExpiry,
                ["Status"] = "Valid"
            };

            return Task.FromResult(string.IsNullOrEmpty(warningMessage) 
                ? ValidationStepResult.Success(null, metadata)
                : ValidationStepResult.Warning(warningMessage, metadata));
        }
    }
}
