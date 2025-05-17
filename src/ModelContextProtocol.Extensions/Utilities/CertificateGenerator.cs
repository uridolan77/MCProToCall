using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Security;

namespace ModelContextProtocol.Extensions.Utilities
{
    /// <summary>
    /// Utility class for generating development certificates for MCP
    /// </summary>
    public static class CertificateGenerator
    {
        /// <summary>
        /// Generates a development certificate for testing secure MCP connections
        /// </summary>
        /// <param name="outputDirectory">Directory to save certificates</param>
        /// <param name="logger">Optional logger</param>
        public static void GenerateDevelopmentCertificates(string outputDirectory, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = ".";
            }
            
            // Make sure the directory exists
            Directory.CreateDirectory(outputDirectory);
            
            // Generate server certificate
            var serverCertPath = Path.Combine(outputDirectory, "server.pfx");
            var serverCert = CertificateHelper.CreateSelfSignedCertificate(
                "MCP-Development-Server",
                365,
                "password",
                serverCertPath,
                logger);
            
            // Generate client certificate
            var clientCertPath = Path.Combine(outputDirectory, "client.pfx");
            var clientCert = CertificateHelper.CreateSelfSignedCertificate(
                "MCP-Development-Client",
                365,
                "password",
                clientCertPath,
                logger);
            
            // Print certificate information
            logger?.LogInformation("Generated server certificate: {Thumbprint}", serverCert.Thumbprint);
            logger?.LogInformation("Generated client certificate: {Thumbprint}", clientCert.Thumbprint);
            logger?.LogInformation("Certificates saved to directory: {Directory}", outputDirectory);
            logger?.LogInformation("Certificate password: 'password'");
            
            // Print configuration instructions
            logger?.LogInformation("To use these certificates:");
            logger?.LogInformation("1. Update appsettings.json for the server:");
            logger?.LogInformation("   \"UseTls\": true,");
            logger?.LogInformation("   \"Tls\": {{");
            logger?.LogInformation("     \"CertificatePath\": \"{0}\",", serverCertPath);
            logger?.LogInformation("     \"CertificatePassword\": \"password\",");
            logger?.LogInformation("     \"RequireClientCertificate\": true,");
            logger?.LogInformation("     \"AllowedClientCertificateThumbprints\": [\"{0}\"]", clientCert.Thumbprint);
            logger?.LogInformation("   }}");
            
            logger?.LogInformation("2. Update appsettings.json for the client:");
            logger?.LogInformation("   \"UseTls\": true,");
            logger?.LogInformation("   \"ClientCertificatePath\": \"{0}\",", clientCertPath);
            logger?.LogInformation("   \"ClientCertificatePassword\": \"password\"");
        }
        
        /// <summary>
        /// Main entry point for the certificate generator tool
        /// </summary>
        public static void Main(string[] args)
        {
            string outputDirectory = args.Length > 0 ? args[0] : ".";
            
            // Set up console logger
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            
            var logger = loggerFactory.CreateLogger("CertificateGenerator");
            
            try
            {
                logger.LogInformation("Generating development certificates for MCP");
                GenerateDevelopmentCertificates(outputDirectory, logger);
                logger.LogInformation("Certificate generation completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating certificates");
            }
        }
    }
}
