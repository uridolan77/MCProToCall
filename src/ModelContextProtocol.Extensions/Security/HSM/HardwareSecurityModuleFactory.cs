using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Security.HSM
{
    /// <summary>
    /// Factory for creating hardware security module instances
    /// </summary>
    public class HardwareSecurityModuleFactory : IHardwareSecurityModuleFactory
    {
        private readonly ILogger<HardwareSecurityModuleFactory> _logger;
        private readonly IOptionsMonitor<HsmOptions> _options;
        private readonly IServiceProvider _serviceProvider;

        public HardwareSecurityModuleFactory(
            ILogger<HardwareSecurityModuleFactory> logger,
            IOptionsMonitor<HsmOptions> options,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IHardwareSecurityModule Create(string providerType)
        {
            if (string.IsNullOrEmpty(providerType))
                throw new ArgumentException("Provider type cannot be null or empty", nameof(providerType));

            _logger.LogInformation("Creating HSM instance for provider type: {ProviderType}", providerType);

            return providerType.ToLowerInvariant() switch
            {
                "azurekeyvault" => new AzureKeyVaultHsm(
                    _serviceProvider.GetRequiredService<ILogger<AzureKeyVaultHsm>>(),
                    Microsoft.Extensions.Options.Options.Create(_options.CurrentValue)),
                "pkcs11" => throw new NotImplementedException("PKCS11 HSM provider not yet implemented"),
                "localcertstore" => throw new NotImplementedException("Local certificate store HSM provider not yet implemented"),
                _ => throw new NotSupportedException($"HSM provider '{providerType}' is not supported. Supported providers: AzureKeyVault")
            };
        }
    }
}
