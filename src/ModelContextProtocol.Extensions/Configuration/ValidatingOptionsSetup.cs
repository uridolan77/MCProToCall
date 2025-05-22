using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Options setup with validation
    /// </summary>
    /// <typeparam name="TOptions">Options type</typeparam>
    public class ValidatingOptionsSetup<TOptions> : IConfigureOptions<TOptions>, IPostConfigureOptions<TOptions>
        where TOptions : class
    {
        private readonly IConfiguration _configuration;
        private readonly string _configSection;
        private readonly ConfigurationValidator _validator;
        private readonly ILogger<ValidatingOptionsSetup<TOptions>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatingOptionsSetup{TOptions}"/> class
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="configSection">Configuration section</param>
        /// <param name="validator">Configuration validator</param>
        /// <param name="logger">Logger</param>
        public ValidatingOptionsSetup(
            IConfiguration configuration,
            string configSection,
            ConfigurationValidator validator,
            ILogger<ValidatingOptionsSetup<TOptions>> logger)
        {
            _configuration = configuration;
            _configSection = configSection;
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Configures the options
        /// </summary>
        /// <param name="options">Options to configure</param>
        public void Configure(TOptions options)
        {
            _configuration.GetSection(_configSection).Bind(options);
        }

        /// <summary>
        /// Post-configures the options and validates them
        /// </summary>
        /// <param name="name">Options name</param>
        /// <param name="options">Options to validate</param>
        public void PostConfigure(string name, TOptions options)
        {
            var result = _validator.ValidateConfiguration(options);
            
            if (!result.IsValid)
            {
                _logger.LogError("Configuration validation failed for {OptionsType}", typeof(TOptions).Name);
                result.LogErrors(_logger);
                
                throw new OptionsValidationException(
                    typeof(TOptions).Name,
                    typeof(TOptions),
                    result.Errors.Select(e => e.Message));
            }
        }
    }
}
