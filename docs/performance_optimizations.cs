// 1. Object Pool for Frequent Allocations
public class DiagnosticMetricsPool
{
    private static readonly ObjectPool<RequestMetric> _requestMetricPool = 
        new DefaultObjectPool<RequestMetric>(new RequestMetricPoolPolicy());
    
    public static RequestMetric GetRequestMetric() => _requestMetricPool.Get();
    public static void ReturnRequestMetric(RequestMetric metric) => _requestMetricPool.Return(metric);
}

// 2. Span<T> and Memory<T> for Buffer Operations
public static class StringUtilitiesOptimized
{
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
            return new string(buffer[..connectionString.Length]);
        }
        finally
        {
            if (connectionString.Length > 256)
                ArrayPool<char>.Shared.Return(buffer.ToArray());
        }
    }
}

// 3. Cached Delegates and Expression Trees
public class CachedValidationExpressions
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
            // Build validation logic...
            return Expression.Lambda<Func<object, ValidationResult>>(body, parameter).Compile();
        });
    }
}

// 4. Async Enumerable for Large Collections
public static async IAsyncEnumerable<CertificateValidationResult> ValidateCertificatesAsync(
    IAsyncEnumerable<X509Certificate2> certificates,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var cert in certificates.WithCancellation(cancellationToken))
    {
        yield return await ValidateCertificateAsync(cert, cancellationToken);
    }
}