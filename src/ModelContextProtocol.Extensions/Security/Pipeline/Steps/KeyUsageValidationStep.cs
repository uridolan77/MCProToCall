using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Pipeline.Steps
{
    /// <summary>
    /// Validates certificate key usage and extended key usage
    /// </summary>
    public class KeyUsageValidationStep : ICertificateValidationStep
    {
        private readonly ILogger<KeyUsageValidationStep> _logger;

        public string StepName => "KeyUsageValidation";
        public int Order => 2; // Run early, after expiry check

        public KeyUsageValidationStep(ILogger<KeyUsageValidationStep> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate,
            CertificateValidationContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Validating key usage for certificate {Thumbprint}", certificate.Thumbprint);

                var metadata = new Dictionary<string, object>
                {
                    ["CertificateType"] = context.CertificateType.ToString()
                };

                var warnings = new List<string>();

                // Check Key Usage extension
                var keyUsageExtension = certificate.Extensions["2.5.29.15"] as X509KeyUsageExtension;
                if (keyUsageExtension != null)
                {
                    var keyUsages = keyUsageExtension.KeyUsages;
                    metadata["KeyUsages"] = keyUsages.ToString();

                    // Validate key usage based on certificate type
                    switch (context.CertificateType)
                    {
                        case CertificateType.Server:
                            if (!keyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment) &&
                                !keyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                            {
                                warnings.Add("Server certificate should have KeyEncipherment or DigitalSignature key usage");
                            }
                            break;

                        case CertificateType.Client:
                            if (!keyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                            {
                                warnings.Add("Client certificate should have DigitalSignature key usage");
                            }
                            break;

                        case CertificateType.IntermediateCA:
                        case CertificateType.RootCA:
                            if (!keyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign))
                            {
                                var message = "CA certificate must have KeyCertSign key usage";
                                _logger.LogWarning("Key usage validation failed: {Message}", message);

                                metadata["Status"] = "InvalidCaKeyUsage";
                                return Task.FromResult(ValidationStepResult.Failure(message, metadata));
                            }
                            break;
                    }
                }
                else
                {
                    warnings.Add("Certificate does not contain Key Usage extension");
                }

                // Check Extended Key Usage extension
                var extendedKeyUsageExtension = certificate.Extensions["2.5.29.37"] as X509EnhancedKeyUsageExtension;
                if (extendedKeyUsageExtension != null)
                {
                    var enhancedKeyUsages = extendedKeyUsageExtension.EnhancedKeyUsages;
                    var ekuOids = new List<string>();

                    foreach (var eku in enhancedKeyUsages)
                    {
                        ekuOids.Add(eku.Value);
                    }

                    metadata["ExtendedKeyUsages"] = ekuOids;

                    // Validate extended key usage based on certificate type
                    switch (context.CertificateType)
                    {
                        case CertificateType.Server:
                            var hasServerAuth = enhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1"); // Server Authentication
                            if (!hasServerAuth)
                            {
                                warnings.Add("Server certificate should have Server Authentication (1.3.6.1.5.5.7.3.1) extended key usage");
                            }
                            break;

                        case CertificateType.Client:
                            var hasClientAuth = enhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2"); // Client Authentication
                            if (!hasClientAuth)
                            {
                                warnings.Add("Client certificate should have Client Authentication (1.3.6.1.5.5.7.3.2) extended key usage");
                            }
                            break;
                    }
                }
                else if (context.CertificateType == CertificateType.Server || context.CertificateType == CertificateType.Client)
                {
                    warnings.Add("Certificate does not contain Extended Key Usage extension");
                }

                // Check Basic Constraints for CA certificates
                var basicConstraintsExtension = certificate.Extensions["2.5.29.19"] as X509BasicConstraintsExtension;
                if (basicConstraintsExtension != null)
                {
                    metadata["IsCA"] = basicConstraintsExtension.CertificateAuthority;
                    metadata["HasPathLengthConstraint"] = basicConstraintsExtension.HasPathLengthConstraint;

                    if (basicConstraintsExtension.HasPathLengthConstraint)
                    {
                        metadata["PathLengthConstraint"] = basicConstraintsExtension.PathLengthConstraint;
                    }

                    // Validate basic constraints
                    switch (context.CertificateType)
                    {
                        case CertificateType.Server:
                        case CertificateType.Client:
                            if (basicConstraintsExtension.CertificateAuthority)
                            {
                                warnings.Add("End-entity certificate should not have CA flag set in Basic Constraints");
                            }
                            break;

                        case CertificateType.IntermediateCA:
                        case CertificateType.RootCA:
                            if (!basicConstraintsExtension.CertificateAuthority)
                            {
                                var message = "CA certificate must have CA flag set in Basic Constraints";
                                _logger.LogWarning("Key usage validation failed: {Message}", message);

                                metadata["Status"] = "InvalidCaBasicConstraints";
                                return Task.FromResult(ValidationStepResult.Failure(message, metadata));
                            }
                            break;
                    }
                }
                else if (context.CertificateType == CertificateType.IntermediateCA || context.CertificateType == CertificateType.RootCA)
                {
                    var message = "CA certificate must contain Basic Constraints extension";
                    _logger.LogWarning("Key usage validation failed: {Message}", message);

                    metadata["Status"] = "MissingBasicConstraints";
                    return Task.FromResult(ValidationStepResult.Failure(message, metadata));
                }

                _logger.LogDebug("Key usage validation completed for certificate {Thumbprint}", certificate.Thumbprint);

                metadata["Status"] = "Valid";
                metadata["WarningsCount"] = warnings.Count;

                if (warnings.Count > 0)
                {
                    var warningMessage = string.Join("; ", warnings);
                    return Task.FromResult(ValidationStepResult.Warning(warningMessage, metadata));
                }

                return Task.FromResult(ValidationStepResult.Success(null, metadata));
            }
            catch (Exception ex)
            {
                var message = $"Error during key usage validation: {ex.Message}";
                _logger.LogError(ex, "Key usage validation failed: {Message}", message);

                return Task.FromResult(ValidationStepResult.Failure(message, new Dictionary<string, object>
                {
                    ["Status"] = "Error",
                    ["ErrorType"] = ex.GetType().Name
                }));
            }
        }
    }
}
