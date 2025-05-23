using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Security;

namespace ModelContextProtocol.Extensions.Utilities
{
    /// <summary>
    /// Command line tool for generating and configuring TLS certificates for MCP
    /// </summary>
    public class TlsSetupTool
    {
        private readonly ILogger _logger;

        public TlsSetupTool(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates certificates for server and clients
        /// </summary>
        /// <param name="outputDir">Directory to save certificates</param>
        /// <param name="serverName">Server name/subject</param>
        /// <param name="clientNames">List of client names/subjects</param>
        /// <param name="password">Password for certificates</param>
        /// <param name="validityDays">Number of days certificates are valid</param>
        public void GenerateCertificates(
            string outputDir,
            string serverName,
            IEnumerable<string> clientNames,
            string password,
            int validityDays)
        {
            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            _logger.LogInformation("Generating TLS certificates in {OutputDir}", outputDir);

            // Generate server certificate
            string serverCertPath = Path.Combine(outputDir, "server.pfx");
            var serverCert = CertificateHelper.CreateSelfSignedCertificate(
                serverName,
                validityDays,
                password,
                serverCertPath,
                _logger);

            _logger.LogInformation("Generated server certificate: Subject={Subject}, Thumbprint={Thumbprint}",
                serverCert.Subject, serverCert.Thumbprint);

            // Generate client certificates
            List<X509Certificate2> clientCerts = new List<X509Certificate2>();
            List<string> clientThumbprints = new List<string>();

            foreach (var clientName in clientNames)
            {
                string safeName = MakeSafeFilename(clientName);
                string clientCertPath = Path.Combine(outputDir, $"client-{safeName}.pfx");

                var clientCert = CertificateHelper.CreateSelfSignedCertificate(
                    clientName,
                    validityDays,
                    password,
                    clientCertPath,
                    _logger);

                clientCerts.Add(clientCert);
                clientThumbprints.Add(clientCert.Thumbprint);

                _logger.LogInformation("Generated client certificate: Subject={Subject}, Thumbprint={Thumbprint}",
                    clientCert.Subject, clientCert.Thumbprint);
            }

            // Generate configuration templates
            GenerateServerConfig(outputDir, serverCertPath, password, clientThumbprints);
            GenerateClientConfig(outputDir, clientCerts, password);
        }

        private void GenerateServerConfig(
            string outputDir,
            string certPath,
            string password,
            List<string> clientThumbprints)
        {
            string configPath = Path.Combine(outputDir, "server-config.json");
            string config = @"{
  ""McpServer"": {
    ""UseTls"": true,
    ""Tls"": {
      ""CertificatePath"": """ + certPath.Replace("\\", "\\\\") + @""",
      ""CertificatePassword"": """ + password + @""",
      ""RequireClientCertificate"": true,
      ""AllowedClientCertificateThumbprints"": [
        " + string.Join(",\r\n        ", clientThumbprints.ConvertAll(t => $"\"{t}\"")) + @"
      ],
      ""CheckCertificateRevocation"": true
    }
  }
}";

            File.WriteAllText(configPath, config);
            _logger.LogInformation("Generated server configuration template: {ConfigPath}", configPath);
        }

        private void GenerateClientConfig(
            string outputDir,
            List<X509Certificate2> clientCerts,
            string password)
        {
            foreach (var cert in clientCerts)
            {
                string safeName = MakeSafeFilename(cert.Subject);
                string configPath = Path.Combine(outputDir, $"client-{safeName}-config.json");
                string certPath = Path.Combine(outputDir, $"client-{safeName}.pfx");

                string config = @"{
  ""McpClient"": {
    ""UseTls"": true,
    ""ClientCertificatePath"": """ + certPath.Replace("\\", "\\\\") + @""",
    ""ClientCertificatePassword"": """ + password + @"""
  }
}";

                File.WriteAllText(configPath, config);
                _logger.LogInformation("Generated client configuration template: {ConfigPath}", configPath);
            }
        }

        private string MakeSafeFilename(string input)
        {
            string safe = input.Replace("CN=", "");
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '-');
            }
            return safe;
        }

        /// <summary>
        /// Provides command line instructions for the tool
        /// </summary>
        public static void PrintUsage()
        {
            Console.WriteLine("TLS Setup Tool for Model Context Protocol");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  TlsSetupTool.exe <output-dir> <server-name> <client-names> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  output-dir    Directory to save certificates");
            Console.WriteLine("  server-name   Server name/subject");
            Console.WriteLine("  client-names  Comma-separated list of client names/subjects");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --password    Password for certificates (default: \"password\")");
            Console.WriteLine("  --validity    Validity period in days (default: 365)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  TlsSetupTool.exe ./certs MyServer \"Client1,Client2,Client3\" --password=\"securePass\" --validity=730");
        }

        /// <summary>
        /// Run the TLS setup tool with the given arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static int Run(string[] args)
        {
            // Set up logger
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<TlsSetupTool>();

            try
            {
                // Parse arguments
                if (args.Length < 3)
                {
                    PrintUsage();
                    return 1;
                }

                string outputDir = args[0];
                string serverName = args[1];
                string[] clientNames = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries);

                // Parse options
                string password = "password";
                int validityDays = 365;

                for (int i = 3; i < args.Length; i++)
                {
                    if (args[i].StartsWith("--password="))
                    {
                        password = args[i].Substring("--password=".Length);
                    }
                    else if (args[i].StartsWith("--validity="))
                    {
                        if (int.TryParse(args[i].Substring("--validity=".Length), out int days))
                        {
                            validityDays = days;
                        }
                    }
                }

                // Generate certificates
                var tool = new TlsSetupTool(logger);
                tool.GenerateCertificates(outputDir, serverName, clientNames, password, validityDays);

                logger.LogInformation("TLS setup completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during TLS setup");
                return 1;
            }
        }
    }
}
