// 1. Structured Error Handling with Result Pattern
public readonly struct Result<T>
{
    private readonly T _value;
    private readonly Exception _error;
    private readonly bool _isSuccess;
    
    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
    }
    
    private Result(Exception error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }
    
    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;
    public T Value => _isSuccess ? _value : throw new InvalidOperationException("Cannot access value of failed result");
    public Exception Error => !_isSuccess ? _error : throw new InvalidOperationException("Cannot access error of successful result");
    
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Exception error) => new(error);
    public static Result<T> Failure(string message) => new(new Exception(message));
    
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return _isSuccess ? Result<TNew>.Success(mapper(_value)) : Result<TNew>.Failure(_error);
    }
    
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        return _isSuccess ? Result<TNew>.Success(await mapper(_value)) : Result<TNew>.Failure(_error);
    }
}

// 2. Enhanced Certificate Validator with Result Pattern
public class EnhancedCertificateValidator : ICertificateValidator
{
    private readonly ICertificateValidationPipeline _pipeline;
    private readonly ILogger<EnhancedCertificateValidator> _logger;
    
    public async Task<Result<bool>> ValidateCertificateAsync(
        X509Certificate2 certificate, 
        CertificateValidationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _pipeline.ValidateAsync(certificate, context, cancellationToken);
            
            if (result.IsValid == true)
            {
                return Result<bool>.Success(true);
            }
            
            var combinedErrors = string.Join("; ", result.Warnings.Concat(new[] { result.ErrorMessage }));
            return Result<bool>.Failure(new CertificateValidationException(combinedErrors, result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate validation failed with exception");
            return Result<bool>.Failure(ex);
        }
    }
}

// 3. Centralized Exception Types
public class McpException : Exception
{
    public string ErrorCode { get; }
    public Dictionary<string, object> ErrorData { get; }
    
    public McpException(string errorCode, string message, Dictionary<string, object> errorData = null) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorData = errorData ?? new Dictionary<string, object>();
    }
}

public class CertificateValidationException : McpException
{
    public CertificateValidationResult ValidationResult { get; }
    
    public CertificateValidationException(string message, CertificateValidationResult result)
        : base("CERT_VALIDATION_FAILED", message)
    {
        ValidationResult = result;
    }
}

// 4. Global Exception Handler Middleware
public class McpExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpExceptionMiddleware> _logger;
    private readonly IMcpTelemetry _telemetry;
    
    public McpExceptionMiddleware(
        RequestDelegate next,
        ILogger<McpExceptionMiddleware> logger,
        IMcpTelemetry telemetry)
    {
        _next = next;
        _logger = logger;
        _telemetry = telemetry;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogError(exception, "Unhandled exception {ErrorId}: {Message}", errorId, exception.Message);
        _telemetry.RecordError("global_exception", exception.GetType().Name);
        
        var response = exception switch
        {
            McpException mcpEx => new ErrorResponse
            {
                ErrorId = errorId,
                ErrorCode = mcpEx.ErrorCode,
                Message = mcpEx.Message,
                Data = mcpEx.ErrorData
            },
            SecurityException secEx => new ErrorResponse
            {
                ErrorId = errorId,
                ErrorCode = "SECURITY_ERROR",
                Message = "A security error occurred"
            },
            _ => new ErrorResponse
            {
                ErrorId = errorId,
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred"
            }
        };
        
        context.Response.StatusCode = GetStatusCode(exception);
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
    
    private static int GetStatusCode(Exception exception) => exception switch
    {
        SecurityException => 403,
        UnauthorizedAccessException => 401,
        ArgumentException => 400,
        TimeoutException => 408,
        _ => 500
    };
}

// 5. Retry Policy with Exponential Backoff
public static class RetryPolicies
{
    public static async Task<Result<T>> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan baseDelay = default,
        CancellationToken cancellationToken = default)
    {
        baseDelay = baseDelay == default ? TimeSpan.FromSeconds(1) : baseDelay;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await operation();
                return Result<T>.Success(result);
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex);
            }
        }
        
        return Result<T>.Failure("Maximum retry attempts exceeded");
    }
    
    private static bool IsTransientException(Exception ex) => 
        ex is TimeoutException ||
        ex is HttpRequestException ||
        ex is TaskCanceledException ||
        (ex is McpException mcpEx && mcpEx.ErrorCode.StartsWith("TRANSIENT_"));
}