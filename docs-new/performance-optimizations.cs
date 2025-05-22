using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// JSON serialization context for source generation
    /// </summary>
    [JsonSerializable(typeof(JsonRpcRequest))]
    [JsonSerializable(typeof(JsonRpcResponse))]
    [JsonSerializable(typeof(JsonRpcErrorResponse))]
    [JsonSerializable(typeof(JsonRpcError))]
    [JsonSerializable(typeof(McpCapabilities))]
    [JsonSerializable(typeof(McpResource))]
    [JsonSerializable(typeof(McpTool))]
    [JsonSerializable(typeof(McpPrompt))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false)]
    public partial class McpJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// High-performance JSON serializer using source generators
    /// </summary>
    public class HighPerformanceJsonSerializer
    {
        private static readonly McpJsonContext _context = new McpJsonContext();
        private static readonly JsonSerializerOptions _options;

        static HighPerformanceJsonSerializer()
        {
            _options = new JsonSerializerOptions
            {
                TypeInfoResolver = _context,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, typeof(T), _context);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _context);
        }

        public static T Deserialize<T>(ReadOnlySpan<byte> utf8Json)
        {
            return JsonSerializer.Deserialize<T>(utf8Json, _context);
        }

        public static async Task SerializeAsync<T>(Stream stream, T value)
        {
            await JsonSerializer.SerializeAsync(stream, value, typeof(T), _context);
        }

        public static async ValueTask<T> DeserializeAsync<T>(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, _context);
        }
    }

    /// <summary>
    /// Object pool for reducing allocations
    /// </summary>
    public class McpObjectPoolProvider
    {
        private readonly ObjectPoolProvider _provider;
        private readonly ConcurrentDictionary<Type, object> _pools;

        public McpObjectPoolProvider()
        {
            _provider = new DefaultObjectPoolProvider();
            _pools = new ConcurrentDictionary<Type, object>();
        }

        public ObjectPool<T> GetPool<T>() where T : class, new()
        {
            return (ObjectPool<T>)_pools.GetOrAdd(typeof(T), 
                _ => _provider.Create(new DefaultPooledObjectPolicy<T>()));
        }
    }

    /// <summary>
    /// Buffer pool for reducing byte array allocations
    /// </summary>
    public static class BufferPool
    {
        private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        public static byte[] Rent(int minimumLength)
        {
            return _arrayPool.Rent(minimumLength);
        }

        public static void Return(byte[] array, bool clearArray = false)
        {
            _arrayPool.Return(array, clearArray);
        }

        public static IMemoryOwner<byte> RentMemory(int minimumLength)
        {
            return new PooledMemory(_arrayPool, minimumLength);
        }

        private class PooledMemory : IMemoryOwner<byte>
        {
            private readonly ArrayPool<byte> _pool;
            private byte[] _array;
            private bool _disposed;

            public PooledMemory(ArrayPool<byte> pool, int minimumLength)
            {
                _pool = pool;
                _array = pool.Rent(minimumLength);
            }

            public Memory<byte> Memory => _array.AsMemory();

            public void Dispose()
            {
                if (!_disposed)
                {
                    _pool.Return(_array);
                    _array = null;
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// High-performance request processor with pooling
    /// </summary>
    public class HighPerformanceRequestProcessor
    {
        private readonly McpObjectPoolProvider _objectPoolProvider;
        private readonly ObjectPool<JsonRpcRequest> _requestPool;
        private readonly ObjectPool<JsonRpcResponse> _responsePool;

        public HighPerformanceRequestProcessor(McpObjectPoolProvider objectPoolProvider)
        {
            _objectPoolProvider = objectPoolProvider;
            _requestPool = _objectPoolProvider.GetPool<JsonRpcRequest>();
            _responsePool = _objectPoolProvider.GetPool<JsonRpcResponse>();
        }

        public async Task<byte[]> ProcessRequestAsync(ReadOnlyMemory<byte> requestData)
        {
            // Rent objects from pool
            var request = _requestPool.Get();
            JsonRpcResponse response = null;

            try
            {
                // Deserialize using source-generated serializer
                request = HighPerformanceJsonSerializer.Deserialize<JsonRpcRequest>(requestData.Span);

                // Process request (simplified)
                response = _responsePool.Get();
                response.Id = request.Id;
                response.Result = new { success = true };

                // Serialize response to buffer
                using var buffer = BufferPool.RentMemory(4096);
                using var stream = new MemoryStream(buffer.Memory.ToArray());
                
                await HighPerformanceJsonSerializer.SerializeAsync(stream, response);
                
                // Return the used portion of the buffer
                var result = new byte[stream.Position];
                Array.Copy(buffer.Memory.ToArray(), result, stream.Position);
                
                return result;
            }
            finally
            {
                // Return objects to pool
                if (request != null)
                {
                    request.Id = null;
                    request.Method = null;
                    request.Params = default;
                    _requestPool.Return(request);
                }

                if (response != null)
                {
                    response.Id = null;
                    response.Result = null;
                    _responsePool.Return(response);
                }
            }
        }
    }

    /// <summary>
    /// Response cache for frequently accessed resources
    /// </summary>
    public class ResponseCache
    {
        private readonly MemoryCache<string, CachedResponse> _cache;
        private readonly TimeSpan _defaultExpiration;

        public ResponseCache(TimeSpan defaultExpiration)
        {
            _defaultExpiration = defaultExpiration;
            _cache = new MemoryCache<string, CachedResponse>(new MemoryCacheOptions
            {
                SizeLimit = 1000, // Maximum number of cached entries
                CompactionPercentage = 0.25
            });
        }

        public async Task<T> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return (T)cached.Data;
            }

            var result = await factory();
            
            var cacheEntry = new CachedResponse
            {
                Data = result,
                CreatedAt = DateTime.UtcNow
            };

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = expiration ?? _defaultExpiration,
                Size = 1
            };

            _cache.Set(key, cacheEntry, cacheEntryOptions);

            return result;
        }

        public void Invalidate(string key)
        {
            _cache.Remove(key);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private class CachedResponse
        {
            public object Data { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }

    /// <summary>
    /// Connection pool for HTTP clients
    /// </summary>
    public class McpConnectionPool
    {
        private readonly ConcurrentDictionary<string, HttpClient> _clients;
        private readonly HttpClientHandler _handler;
        private readonly TimeSpan _connectionLifetime;

        public McpConnectionPool(TimeSpan connectionLifetime)
        {
            _clients = new ConcurrentDictionary<string, HttpClient>();
            _connectionLifetime = connectionLifetime;
            
            _handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 50,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
        }

        public HttpClient GetClient(string endpoint)
        {
            return _clients.GetOrAdd(endpoint, key =>
            {
                var client = new HttpClient(_handler, disposeHandler: false)
                {
                    BaseAddress = new Uri(key),
                    Timeout = TimeSpan.FromSeconds(30)
                };

                // Set connection lifetime
                client.DefaultRequestHeaders.ConnectionClose = false;
                client.DefaultRequestHeaders.Add("Keep-Alive", "timeout=600");

                return client;
            });
        }

        public void Dispose()
        {
            foreach (var client in _clients.Values)
            {
                client?.Dispose();
            }
            
            _handler?.Dispose();
        }
    }
}