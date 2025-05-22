using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Object pool for reducing allocations
    /// </summary>
    public class McpObjectPoolProvider
    {
        private readonly DefaultObjectPoolProvider _provider;
        private readonly ConcurrentDictionary<Type, object> _pools;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpObjectPoolProvider"/> class
        /// </summary>
        public McpObjectPoolProvider()
        {
            _provider = new DefaultObjectPoolProvider();
            _pools = new ConcurrentDictionary<Type, object>();
        }

        /// <summary>
        /// Gets an object pool for the specified type
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <returns>Object pool</returns>
        public ObjectPool<T> GetPool<T>() where T : class, new()
        {
            return (ObjectPool<T>)_pools.GetOrAdd(typeof(T),
                _ => _provider.Create(new DefaultPooledObjectPolicy<T>()));
        }
    }
}
