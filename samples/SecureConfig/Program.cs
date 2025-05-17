using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Resilience;
using ModelContextProtocol.Extensions.Security.Credentials;
using ModelContextProtocol.Extensions.Utilities;

namespace SecureConfig
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Set up configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            // Set up dependency injection
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add security services
            services.AddSecurityServices(configuration);

            // Add resilience services
            services.AddResilienceServices(configuration);

            // Add resilient HTTP client
            services.AddResilientHttpClient("resilient-client", (sp, client) =>
            {
                client.BaseAddress = new Uri("https://api.example.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "ModelContextProtocol-Sample");
            });

            // Add rate-limited HTTP client
            services.AddRateLimitedHttpClient("rate-limited-client", (sp, client) =>
            {
                client.BaseAddress = new Uri("https://api.example.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "ModelContextProtocol-Sample");
            });

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get services
            var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
            var secretManager = serviceProvider.GetRequiredService<ISecretManager>();
            var rateLimiter = serviceProvider.GetRequiredService<IRateLimiter>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                // Example: Get a connection string with secure error handling
                logger.LogInformation("Getting connection string...");
                try
                {
                    var connectionString = await connectionStringProvider.GetConnectionStringAsync("DefaultConnection");

                    // Sanitize the connection string for logging
                    var sanitizedConnectionString = StringUtilities.SanitizeConnectionString(connectionString);
                    logger.LogInformation("Retrieved connection string: {ConnectionString}", sanitizedConnectionString);
                }
                catch (ConnectionStringNotFoundException ex)
                {
                    logger.LogError(ex, "Connection string not found");
                }
                catch (ConnectionStringException ex)
                {
                    logger.LogError(ex, "Error retrieving connection string");
                }

                // Example: Get a secret with secure error handling
                logger.LogInformation("Getting secret...");
                try
                {
                    var secret = await secretManager.GetSecretAsync("ExampleSecret");
                    logger.LogInformation("Retrieved secret (length: {Length})", secret?.Length ?? 0);

                    // Example: Check if a secret needs rotation
                    logger.LogInformation("Checking if secret needs rotation...");
                    var needsRotation = await secretManager.IsRotationNeededAsync("ExampleSecret");
                    logger.LogInformation("Secret needs rotation: {NeedsRotation}", needsRotation);

                    if (needsRotation)
                    {
                        logger.LogInformation("Rotating secret...");
                        var newSecret = await secretManager.RotateSecretAsync("ExampleSecret");
                        logger.LogInformation("Secret rotated successfully (new length: {Length})", newSecret?.Length ?? 0);
                    }
                }
                catch (SecretNotFoundException ex)
                {
                    logger.LogError(ex, "Secret not found");
                }
                catch (SecretAccessException ex)
                {
                    logger.LogError(ex, "Error accessing secret");
                }

                // Example: Use rate limiter
                logger.LogInformation("Testing rate limiter...");
                logger.LogInformation("Available permits: {AvailablePermits}/{MaxPermits}",
                    rateLimiter.AvailablePermits, rateLimiter.MaxPermits);

                for (int i = 0; i < 5; i++)
                {
                    var acquired = await rateLimiter.TryAcquireAsync();
                    logger.LogInformation("Attempt {Attempt}: Acquired permit: {Acquired}, Available permits: {AvailablePermits}",
                        i + 1, acquired, rateLimiter.AvailablePermits);
                }

                // Example: Use resilient HTTP client
                logger.LogInformation("Testing resilient HTTP client...");
                try
                {
                    var client = httpClientFactory.CreateClient("resilient-client");
                    var response = await client.GetAsync("https://httpstat.us/200");

                    logger.LogInformation("Response status code: {StatusCode}", response.StatusCode);

                    // Test retry policy with a 429 response
                    logger.LogInformation("Testing retry policy with 429 Too Many Requests...");
                    response = await client.GetAsync("https://httpstat.us/429");

                    logger.LogInformation("Response status code after retries: {StatusCode}", response.StatusCode);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error making HTTP request");
                }

                // Example: Use rate-limited HTTP client
                logger.LogInformation("Testing rate-limited HTTP client...");
                try
                {
                    var client = httpClientFactory.CreateClient("rate-limited-client");

                    // Make multiple requests to demonstrate rate limiting
                    for (int i = 0; i < 3; i++)
                    {
                        logger.LogInformation("Making request {RequestNumber}...", i + 1);
                        var response = await client.GetAsync("https://httpstat.us/200");
                        logger.LogInformation("Response status code: {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error making HTTP request");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error");
            }
        }
    }
}
