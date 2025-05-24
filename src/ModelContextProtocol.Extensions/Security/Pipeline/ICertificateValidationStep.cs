using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Security.Pipeline
{
    /// <summary>
    /// Represents a single step in the certificate validation pipeline
    /// </summary>
    public interface ICertificateValidationStep
    {
        /// <summary>
        /// The name of this validation step
        /// </summary>
        string StepName { get; }

        /// <summary>
        /// The order in which this step should be executed (lower values execute first)
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Validates a certificate as part of the pipeline
        /// </summary>
        /// <param name="certificate">The certificate to validate</param>
        /// <param name="context">The validation context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The validation result</returns>
        Task<ValidationStepResult> ValidateAsync(
            X509Certificate2 certificate, 
            CertificateValidationContext context,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Result of a certificate validation step
    /// </summary>
    public class ValidationStepResult
    {
        /// <summary>
        /// Whether the validation step passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Warning message (validation can still pass with warnings)
        /// </summary>
        public string WarningMessage { get; set; }

        /// <summary>
        /// Additional metadata from the validation step
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Time taken for this validation step
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Whether this step should stop the pipeline (even if valid)
        /// </summary>
        public bool StopPipeline { get; set; } = false;

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static ValidationStepResult Success(string message = null, Dictionary<string, object> metadata = null)
        {
            return new ValidationStepResult
            {
                IsValid = true,
                WarningMessage = message,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        public static ValidationStepResult Failure(string errorMessage, Dictionary<string, object> metadata = null)
        {
            return new ValidationStepResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Creates a successful validation result with a warning
        /// </summary>
        public static ValidationStepResult Warning(string warningMessage, Dictionary<string, object> metadata = null)
        {
            return new ValidationStepResult
            {
                IsValid = true,
                WarningMessage = warningMessage,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// Context for certificate validation
    /// </summary>
    public class CertificateValidationContext
    {
        /// <summary>
        /// The certificate chain
        /// </summary>
        public X509Chain Chain { get; set; }

        /// <summary>
        /// SSL policy errors
        /// </summary>
        public System.Net.Security.SslPolicyErrors SslPolicyErrors { get; set; }

        /// <summary>
        /// Remote endpoint information
        /// </summary>
        public string RemoteEndpoint { get; set; }

        /// <summary>
        /// Whether this is a client or server certificate
        /// </summary>
        public CertificateType CertificateType { get; set; }

        /// <summary>
        /// TLS options for validation
        /// </summary>
        public TlsOptions TlsOptions { get; set; }

        /// <summary>
        /// Additional context data
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Validation start time
        /// </summary>
        public DateTime ValidationStartTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Type of certificate being validated
    /// </summary>
    public enum CertificateType
    {
        /// <summary>
        /// Server certificate
        /// </summary>
        Server,

        /// <summary>
        /// Client certificate
        /// </summary>
        Client,

        /// <summary>
        /// Intermediate CA certificate
        /// </summary>
        IntermediateCA,

        /// <summary>
        /// Root CA certificate
        /// </summary>
        RootCA
    }
}
