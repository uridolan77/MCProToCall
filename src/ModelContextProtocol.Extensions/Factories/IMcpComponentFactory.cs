using Microsoft.Extensions.Configuration;

namespace ModelContextProtocol.Extensions.Factories
{
    /// <summary>
    /// Generic factory interface for creating MCP components
    /// </summary>
    /// <typeparam name="T">The type of component to create</typeparam>
    public interface IMcpComponentFactory<T>
    {
        /// <summary>
        /// Creates a component with the specified name and configuration
        /// </summary>
        /// <param name="name">The name of the component</param>
        /// <param name="config">The configuration for the component</param>
        /// <returns>The created component</returns>
        T Create(string name, IConfiguration config);

        /// <summary>
        /// Creates a component with the specified name and configuration action
        /// </summary>
        /// <param name="name">The name of the component</param>
        /// <param name="configure">Action to configure the component</param>
        /// <returns>The created component</returns>
        T CreateWithOptions(string name, Action<T> configure);

        /// <summary>
        /// Gets the supported component types
        /// </summary>
        IEnumerable<string> SupportedTypes { get; }
    }

    /// <summary>
    /// Base factory implementation with common functionality
    /// </summary>
    /// <typeparam name="T">The type of component to create</typeparam>
    public abstract class McpComponentFactoryBase<T> : IMcpComponentFactory<T>
    {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly Dictionary<string, Func<IConfiguration, T>> _creators;

        protected McpComponentFactoryBase(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _creators = new Dictionary<string, Func<IConfiguration, T>>(StringComparer.OrdinalIgnoreCase);
            RegisterCreators();
        }

        public virtual T Create(string name, IConfiguration config)
        {
            if (!_creators.TryGetValue(name, out var creator))
            {
                throw new NotSupportedException($"Component type '{name}' is not supported. Supported types: {string.Join(", ", SupportedTypes)}");
            }

            return creator(config);
        }

        public virtual T CreateWithOptions(string name, Action<T> configure)
        {
            var component = Create(name, new ConfigurationBuilder().Build());
            configure?.Invoke(component);
            return component;
        }

        public IEnumerable<string> SupportedTypes => _creators.Keys;

        /// <summary>
        /// Register component creators in derived classes
        /// </summary>
        protected abstract void RegisterCreators();
    }
}
