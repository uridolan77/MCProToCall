using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ModelContextProtocol.Extensions.Testing
{
    /// <summary>
    /// Builder for creating certificate validation contexts in tests
    /// </summary>
    public class CertificateValidationContextBuilder
    {
        private X509Chain _chain = new();
        private System.Net.Security.SslPolicyErrors _sslPolicyErrors = System.Net.Security.SslPolicyErrors.None;
        private string _remoteEndpoint = "test.example.com";
        private CertificateType _certificateType = CertificateType.Server;
        private TlsOptions _tlsOptions = new();

        /// <summary>
        /// Creates a new builder instance
        /// </summary>
        public static CertificateValidationContextBuilder Create() => new();

        /// <summary>
        /// Sets the certificate chain
        /// </summary>
        public CertificateValidationContextBuilder WithChain(X509Chain chain)
        {
            _chain = chain;
            return this;
        }

        /// <summary>
        /// Sets the SSL policy errors
        /// </summary>
        public CertificateValidationContextBuilder WithSslErrors(System.Net.Security.SslPolicyErrors errors)
        {
            _sslPolicyErrors = errors;
            return this;
        }

        /// <summary>
        /// Sets the remote endpoint
        /// </summary>
        public CertificateValidationContextBuilder WithRemoteEndpoint(string endpoint)
        {
            _remoteEndpoint = endpoint;
            return this;
        }

        /// <summary>
        /// Configures as a client certificate
        /// </summary>
        public CertificateValidationContextBuilder AsClientCertificate()
        {
            _certificateType = CertificateType.Client;
            return this;
        }

        /// <summary>
        /// Configures as a server certificate
        /// </summary>
        public CertificateValidationContextBuilder AsServerCertificate()
        {
            _certificateType = CertificateType.Server;
            return this;
        }

        /// <summary>
        /// Sets the TLS options
        /// </summary>
        public CertificateValidationContextBuilder WithTlsOptions(TlsOptions tlsOptions)
        {
            _tlsOptions = tlsOptions;
            return this;
        }

        /// <summary>
        /// Builds the certificate validation context
        /// </summary>
        public CertificateValidationContext Build() => new()
        {
            Chain = _chain,
            SslPolicyErrors = _sslPolicyErrors,
            RemoteEndpoint = _remoteEndpoint,
            CertificateType = _certificateType,
            TlsOptions = _tlsOptions
        };
    }

    /// <summary>
    /// Factory for creating test certificates
    /// </summary>
    public static class TestCertificateFactory
    {
        /// <summary>
        /// Creates a valid server certificate for testing
        /// </summary>
        public static X509Certificate2 CreateValidServerCertificate(
            string subject = "CN=test.example.com",
            TimeSpan? validity = null)
        {
            validity ??= TimeSpan.FromDays(365);

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add Server Authentication EKU
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));

            // Add Subject Alternative Name
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("test.example.com");
            sanBuilder.AddDnsName("*.test.example.com");
            request.CertificateExtensions.Add(sanBuilder.Build());

            var certificate = request.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.Add(validity.Value));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string)null, X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// Creates an expired certificate for testing
        /// </summary>
        public static X509Certificate2 CreateExpiredCertificate(string subject = "CN=expired.example.com")
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var notBefore = DateTime.UtcNow.AddDays(-30);
            var notAfter = DateTime.UtcNow.AddDays(-1);

            var certificate = request.CreateSelfSigned(notBefore, notAfter);
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        /// <summary>
        /// Creates a certificate with invalid key usage for testing
        /// </summary>
        public static X509Certificate2 CreateInvalidKeyUsageCertificate(string subject = "CN=invalid.example.com")
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add Client Authentication EKU instead of Server Authentication
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.2") }, true));

            var certificate = request.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.AddDays(365));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        /// <summary>
        /// Creates a self-signed certificate for testing
        /// </summary>
        public static X509Certificate2 CreateSelfSignedCertificate(string subject = "CN=selfsigned.example.com")
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add basic constraints to mark as CA certificate
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));

            var certificate = request.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.AddDays(365));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        /// <summary>
        /// Creates a certificate with a weak signature algorithm for testing
        /// </summary>
        public static X509Certificate2 CreateWeakSignatureCertificate(string subject = "CN=weak.example.com")
        {
            using var rsa = RSA.Create(1024); // Weak key size
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1); // Weak hash

            var certificate = request.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.AddDays(365));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        /// <summary>
        /// Creates a certificate chain for testing
        /// </summary>
        public static (X509Certificate2 rootCa, X509Certificate2 intermediateCa, X509Certificate2 leafCert) CreateCertificateChain()
        {
            // Create root CA
            using var rootRsa = RSA.Create(2048);
            var rootRequest = new CertificateRequest("CN=Test Root CA", rootRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            var rootCa = rootRequest.CreateSelfSigned(DateTime.UtcNow, DateTime.UtcNow.AddYears(10));

            // Create intermediate CA
            using var intermediateRsa = RSA.Create(2048);
            var intermediateRequest = new CertificateRequest("CN=Test Intermediate CA", intermediateRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            var intermediateCa = intermediateRequest.Create(rootCa, DateTime.UtcNow, DateTime.UtcNow.AddYears(5), new byte[] { 1, 2, 3, 4 });

            // Create leaf certificate
            using var leafRsa = RSA.Create(2048);
            var leafRequest = new CertificateRequest("CN=test.example.com", leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("test.example.com");
            leafRequest.CertificateExtensions.Add(sanBuilder.Build());
            var leafCert = leafRequest.Create(intermediateCa, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), new byte[] { 5, 6, 7, 8 });

            return (
                new X509Certificate2(rootCa.Export(X509ContentType.Pfx)),
                new X509Certificate2(intermediateCa.Export(X509ContentType.Pfx)),
                new X509Certificate2(leafCert.Export(X509ContentType.Pfx))
            );
        }
    }

    /// <summary>
    /// Builder for creating TLS options in tests
    /// </summary>
    public class TlsOptionsBuilder
    {
        private TlsOptions _options = new();

        public static TlsOptionsBuilder Create() => new();

        public TlsOptionsBuilder AllowUntrustedCertificates(bool allow = true)
        {
            _options.AllowUntrustedCertificates = allow;
            return this;
        }

        public TlsOptionsBuilder AllowSelfSignedCertificates(bool allow = true)
        {
            _options.AllowSelfSignedCertificates = allow;
            return this;
        }

        public TlsOptionsBuilder UseTls(bool use = true)
        {
            _options.UseTls = use;
            return this;
        }

        public TlsOptionsBuilder WithCertificatePinning(bool enable = true)
        {
            _options.CertificatePinning.Enabled = enable;
            return this;
        }

        public TlsOptions Build() => _options;
    }
}
