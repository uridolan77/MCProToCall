using Microsoft.Extensions.ObjectPool;
using ModelContextProtocol.Extensions.Diagnostics;
using ModelContextProtocol.Extensions.Security;
using ModelContextProtocol.Extensions.Security.Pipeline;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// Poolable request metric for performance optimization
    /// </summary>
    public class RequestMetric
    {
        public string RequestId { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsError { get; set; }
        public string Method { get; set; }
        public string Endpoint { get; set; }
        public int StatusCode { get; set; }
        public long RequestSize { get; set; }
        public long ResponseSize { get; set; }

        /// <summary>
        /// Resets the metric to its initial state for pooling
        /// </summary>
        public void Reset()
        {
            RequestId = null;
            Timestamp = default;
            Duration = default;
            IsError = false;
            Method = null;
            Endpoint = null;
            StatusCode = 0;
            RequestSize = 0;
            ResponseSize = 0;
        }
    }

    /// <summary>
    /// Object pool for diagnostic metrics to reduce allocations
    /// </summary>
    public static class DiagnosticMetricsPool
    {
        private static readonly ObjectPool<RequestMetric> _requestMetricPool =
            new DefaultObjectPool<RequestMetric>(new RequestMetricPoolPolicy());

        public static RequestMetric GetRequestMetric() => _requestMetricPool.Get();
        public static void ReturnRequestMetric(RequestMetric metric) => _requestMetricPool.Return(metric);
    }

    /// <summary>
    /// Pool policy for request metrics
    /// </summary>
    public class RequestMetricPoolPolicy : IPooledObjectPolicy<RequestMetric>
    {
        public RequestMetric Create()
        {
            return new RequestMetric();
        }

        public bool Return(RequestMetric obj)
        {
            if (obj == null) return false;

            // Reset the object to its initial state
            obj.Reset();
            return true;
        }
    }

    /// <summary>
    /// Optimized string utilities using Span<T> and Memory<T>
    /// </summary>
    public static class StringUtilitiesOptimized
    {
        /// <summary>
        /// Sanitizes connection string using span-based operations for better performance
        /// </summary>
        public static string SanitizeConnectionString(ReadOnlySpan<char> connectionString)
        {
            if (connectionString.IsEmpty) return string.Empty;

            // Use stackalloc for small strings, rent for larger ones
            Span<char> buffer = connectionString.Length <= 256
                ? stackalloc char[connectionString.Length]
                : ArrayPool<char>.Shared.Rent(connectionString.Length);

            try
            {
                connectionString.CopyTo(buffer);

                // Perform sanitization in-place
                var workingSpan = buffer[..connectionString.Length];
                SanitizeSpan(workingSpan);

                return new string(workingSpan);
            }
            finally
            {
                if (connectionString.Length > 256)
                    ArrayPool<char>.Shared.Return(buffer.ToArray());
            }
        }

        private static void SanitizeSpan(Span<char> span)
        {
            // Remove or mask sensitive information
            var sensitiveKeywords = new[] { "password", "pwd", "secret", "key" };

            for (int i = 0; i < span.Length; i++)
            {
                // Simple sanitization logic - replace sensitive values
                if (span[i] == '=' && i > 0)
                {
                    // Check if this is a sensitive parameter
                    var paramStart = FindParameterStart(span, i);
                    if (paramStart >= 0)
                    {
                        var paramName = span[paramStart..i];
                        if (IsSensitiveParameter(paramName))
                        {
                            // Mask the value after the '='
                            var valueStart = i + 1;
                            var valueEnd = FindParameterEnd(span, valueStart);
                            span[valueStart..valueEnd].Fill('*');
                        }
                    }
                }
            }
        }

        private static int FindParameterStart(ReadOnlySpan<char> span, int equalIndex)
        {
            for (int i = equalIndex - 1; i >= 0; i--)
            {
                if (span[i] == ';' || span[i] == ' ')
                    return i + 1;
            }
            return 0;
        }

        private static int FindParameterEnd(ReadOnlySpan<char> span, int startIndex)
        {
            for (int i = startIndex; i < span.Length; i++)
            {
                if (span[i] == ';')
                    return i;
            }
            return span.Length;
        }

        private static bool IsSensitiveParameter(ReadOnlySpan<char> paramName)
        {
            var lowerParam = paramName.ToString().ToLowerInvariant();
            return lowerParam.Contains("password") ||
                   lowerParam.Contains("pwd") ||
                   lowerParam.Contains("secret") ||
                   lowerParam.Contains("key");
        }
    }

    /// <summary>
    /// Cached validation expressions for improved performance
    /// </summary>
    public static class CachedValidationExpressions
    {
        private static readonly ConcurrentDictionary<Type, Func<object, ValidationResult>>
            _validationCache = new();

        public static Func<object, ValidationResult> GetValidator<T>()
        {
            return _validationCache.GetOrAdd(typeof(T), type =>
            {
                // Compile expression tree for fast validation
                var parameter = Expression.Parameter(typeof(object), "obj");
                var cast = Expression.Convert(parameter, type);

                // Build validation logic using expression trees
                var validationMethod = typeof(CachedValidationExpressions)
                    .GetMethod(nameof(ValidateObject), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.MakeGenericMethod(type);

                if (validationMethod != null)
                {
                    var call = Expression.Call(validationMethod, cast);
                    return Expression.Lambda<Func<object, ValidationResult>>(call, parameter).Compile();
                }

                // Fallback to simple validation
                return obj => ValidationResult.Success;
            });
        }

        private static ValidationResult ValidateObject<T>(T obj)
        {
            if (obj == null)
                return new ValidationResult("Object cannot be null");

            // Perform type-specific validation
            var validationContext = new ValidationContext(obj);
            var results = new List<ValidationResult>();

            if (Validator.TryValidateObject(obj, validationContext, results, true))
            {
                return ValidationResult.Success;
            }

            return results.FirstOrDefault() ?? new ValidationResult("Validation failed");
        }
    }

    /// <summary>
    /// Async enumerable extensions for large collections
    /// </summary>
    public static class AsyncEnumerableExtensions
    {
        /// <summary>
        /// Validates certificates asynchronously using async enumerable for better memory efficiency
        /// </summary>
        public static async IAsyncEnumerable<CertificateValidationResult> ValidateCertificatesAsync(
            this IAsyncEnumerable<System.Security.Cryptography.X509Certificates.X509Certificate2> certificates,
            ICertificateValidationPipeline validator,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var cert in certificates.WithCancellation(cancellationToken))
            {
                var context = new CertificateValidationContext
                {
                    CertificateType = CertificateType.Server,
                    RemoteEndpoint = "unknown"
                };

                var result = await validator.ValidateAsync(cert, context, cancellationToken);
                yield return result;
            }
        }

        /// <summary>
        /// Processes items in batches for better performance
        /// </summary>
        public static async IAsyncEnumerable<TResult[]> BatchAsync<T, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<TResult>> processor,
            int batchSize = 10,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var batch = new List<T>(batchSize);

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                batch.Add(item);

                if (batch.Count >= batchSize)
                {
                    var tasks = batch.Select(processor);
                    var results = await Task.WhenAll(tasks);
                    yield return results;
                    batch.Clear();
                }
            }

            // Process remaining items
            if (batch.Count > 0)
            {
                var tasks = batch.Select(processor);
                var results = await Task.WhenAll(tasks);
                yield return results;
            }
        }
    }

    /// <summary>
    /// Memory-efficient buffer management
    /// </summary>
    public static class BufferManager
    {
        private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
        private static readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;

        public static IDisposable RentBuffer(int minimumLength, out byte[] buffer)
        {
            buffer = _bytePool.Rent(minimumLength);
            return new BufferRental<byte>(_bytePool, buffer);
        }

        public static IDisposable RentCharBuffer(int minimumLength, out char[] buffer)
        {
            buffer = _charPool.Rent(minimumLength);
            return new BufferRental<char>(_charPool, buffer);
        }

        private class BufferRental<T> : IDisposable
        {
            private readonly ArrayPool<T> _pool;
            private readonly T[] _buffer;
            private bool _disposed;

            public BufferRental(ArrayPool<T> pool, T[] buffer)
            {
                _pool = pool;
                _buffer = buffer;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _pool.Return(_buffer);
                    _disposed = true;
                }
            }
        }
    }
}
