using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security
{
    /// <summary>
    /// Validates authentication tokens and credentials
    /// </summary>
    public class AuthenticationValidator : ISecurityValidator
    {
        private readonly ILogger<AuthenticationValidator> _logger;

        public int Priority => 10;

        public AuthenticationValidator(ILogger<AuthenticationValidator> logger)
        {
            _logger = logger;
        }

        public async Task<SecurityValidationResult> ValidateAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // Check for authentication header
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    IsCritical = true,
                    Violation = new SecurityViolation
                    {
                        Type = SecurityViolationType.Authentication,
                        Severity = SecurityViolationSeverity.High,
                        Description = "Missing Authorization header",
                        Source = nameof(AuthenticationValidator)
                    }
                };
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    IsCritical = true,
                    Violation = new SecurityViolation
                    {
                        Type = SecurityViolationType.Authentication,
                        Severity = SecurityViolationSeverity.High,
                        Description = "Empty Authorization header",
                        Source = nameof(AuthenticationValidator)
                    }
                };
            }

            // Basic validation - in real implementation, validate JWT or other tokens
            if (!authHeader.StartsWith("Bearer ") && !authHeader.StartsWith("Basic "))
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    IsCritical = true,
                    Violation = new SecurityViolation
                    {
                        Type = SecurityViolationType.Authentication,
                        Severity = SecurityViolationSeverity.High,
                        Description = "Invalid Authorization header format",
                        Source = nameof(AuthenticationValidator)
                    }
                };
            }

            _logger.LogDebug("Authentication validation passed for request {RequestId}", context.TraceIdentifier);

            return new SecurityValidationResult
            {
                IsValid = true,
                IsCritical = false
            };
        }
    }

    /// <summary>
    /// Validates user authorization and permissions
    /// </summary>
    public class AuthorizationValidator : ISecurityValidator
    {
        private readonly ILogger<AuthorizationValidator> _logger;

        public int Priority => 20;

        public AuthorizationValidator(ILogger<AuthorizationValidator> logger)
        {
            _logger = logger;
        }

        public async Task<SecurityValidationResult> ValidateAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // Check if user is authenticated first
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                return new SecurityValidationResult
                {
                    IsValid = false,
                    IsCritical = true,
                    Violation = new SecurityViolation
                    {
                        Type = SecurityViolationType.Authorization,
                        Severity = SecurityViolationSeverity.High,
                        Description = "User is not authenticated",
                        Source = nameof(AuthorizationValidator)
                    }
                };
            }

            // Basic authorization check - in real implementation, check roles/claims
            var path = context.Request.Path.Value?.ToLowerInvariant();
            if (path?.Contains("/admin/") == true)
            {
                var hasAdminRole = context.User.IsInRole("Admin");
                if (!hasAdminRole)
                {
                    return new SecurityValidationResult
                    {
                        IsValid = false,
                        IsCritical = true,
                        Violation = new SecurityViolation
                        {
                            Type = SecurityViolationType.Authorization,
                            Severity = SecurityViolationSeverity.High,
                            Description = "Insufficient permissions for admin endpoint",
                            Source = nameof(AuthorizationValidator)
                        }
                    };
                }
            }

            _logger.LogDebug("Authorization validation passed for request {RequestId}", context.TraceIdentifier);

            return new SecurityValidationResult
            {
                IsValid = true,
                IsCritical = false
            };
        }
    }

    /// <summary>
    /// Validates rate limiting constraints
    /// </summary>
    public class RateLimitValidator : ISecurityValidator
    {
        private readonly ILogger<RateLimitValidator> _logger;
        private static readonly Dictionary<string, DateTime> _lastRequestTimes = new();
        private static readonly object _lock = new object();

        public int Priority => 30;

        public RateLimitValidator(ILogger<RateLimitValidator> logger)
        {
            _logger = logger;
        }

        public async Task<SecurityValidationResult> ValidateAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_lastRequestTimes.TryGetValue(clientIp, out var lastRequest))
                {
                    var timeSinceLastRequest = now - lastRequest;
                    if (timeSinceLastRequest < TimeSpan.FromSeconds(1)) // Simple rate limit: 1 request per second
                    {
                        return new SecurityValidationResult
                        {
                            IsValid = false,
                            IsCritical = false,
                            Violation = new SecurityViolation
                            {
                                Type = SecurityViolationType.RateLimiting,
                                Severity = SecurityViolationSeverity.Medium,
                                Description = $"Rate limit exceeded for IP {clientIp}",
                                Source = nameof(RateLimitValidator),
                                Details = new Dictionary<string, object>
                                {
                                    ["ClientIp"] = clientIp,
                                    ["TimeSinceLastRequest"] = timeSinceLastRequest.TotalMilliseconds
                                }
                            }
                        };
                    }
                }

                _lastRequestTimes[clientIp] = now;
            }

            _logger.LogDebug("Rate limit validation passed for IP {ClientIp}", clientIp);

            return new SecurityValidationResult
            {
                IsValid = true,
                IsCritical = false
            };
        }
    }

    /// <summary>
    /// Validates input data for security threats
    /// </summary>
    public class InputValidationValidator : ISecurityValidator
    {
        private readonly ILogger<InputValidationValidator> _logger;
        private static readonly string[] _suspiciousPatterns = 
        {
            "<script", "javascript:", "vbscript:", "onload=", "onerror=",
            "SELECT * FROM", "DROP TABLE", "INSERT INTO", "DELETE FROM",
            "../", "..\\", "/etc/passwd", "cmd.exe", "powershell"
        };

        public int Priority => 40;

        public InputValidationValidator(ILogger<InputValidationValidator> logger)
        {
            _logger = logger;
        }

        public async Task<SecurityValidationResult> ValidateAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // Check query parameters
            foreach (var param in context.Request.Query)
            {
                if (ContainsSuspiciousContent(param.Value))
                {
                    return CreateViolationResult($"Suspicious content in query parameter '{param.Key}'");
                }
            }

            // Check headers
            foreach (var header in context.Request.Headers)
            {
                if (ContainsSuspiciousContent(header.Value))
                {
                    return CreateViolationResult($"Suspicious content in header '{header.Key}'");
                }
            }

            // Check form data if present
            if (context.Request.HasFormContentType)
            {
                try
                {
                    var form = await context.Request.ReadFormAsync(cancellationToken);
                    foreach (var field in form)
                    {
                        if (ContainsSuspiciousContent(field.Value))
                        {
                            return CreateViolationResult($"Suspicious content in form field '{field.Key}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read form data for validation");
                }
            }

            _logger.LogDebug("Input validation passed for request {RequestId}", context.TraceIdentifier);

            return new SecurityValidationResult
            {
                IsValid = true,
                IsCritical = false
            };
        }

        private bool ContainsSuspiciousContent(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            var lowerContent = content.ToLowerInvariant();
            return _suspiciousPatterns.Any(pattern => lowerContent.Contains(pattern.ToLowerInvariant()));
        }

        private SecurityValidationResult CreateViolationResult(string description)
        {
            return new SecurityValidationResult
            {
                IsValid = false,
                IsCritical = false,
                Violation = new SecurityViolation
                {
                    Type = SecurityViolationType.InputValidation,
                    Severity = SecurityViolationSeverity.High,
                    Description = description,
                    Source = nameof(InputValidationValidator)
                }
            };
        }
    }
}
