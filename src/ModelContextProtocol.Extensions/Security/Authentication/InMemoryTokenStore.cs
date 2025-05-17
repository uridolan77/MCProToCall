using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModelContextProtocol.Extensions.Security.Authentication
{
    /// <summary>
    /// In-memory implementation of ITokenStore
    /// Note: For production, use a distributed cache or database-backed implementation
    /// </summary>
    public class InMemoryTokenStore : ITokenStore
    {
        private class TokenInfo
        {
            public string UserId { get; set; }
            public DateTime Expiration { get; set; }
            public string TokenFamily { get; set; }
            public bool IsRevoked { get; set; }
        }

        private readonly ConcurrentDictionary<string, TokenInfo> _tokens = new ConcurrentDictionary<string, TokenInfo>();
        private readonly ConcurrentDictionary<string, HashSet<string>> _userTokens = new ConcurrentDictionary<string, HashSet<string>>();
        private readonly ILogger<InMemoryTokenStore> _logger;
        private Timer _cleanupTimer;

        public InMemoryTokenStore(ILogger<InMemoryTokenStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set up periodic cleanup of expired tokens
            _cleanupTimer = new Timer(
                async _ => await PurgeExpiredTokensAsync(),
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(1));
        }

        /// <inheritdoc />
        public Task StoreRefreshTokenAsync(string refreshToken, string userId, DateTime expiration)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            var tokenFamily = Guid.NewGuid().ToString();
            
            var tokenInfo = new TokenInfo
            {
                UserId = userId,
                Expiration = expiration,
                TokenFamily = tokenFamily,
                IsRevoked = false
            };
            
            _tokens[refreshToken] = tokenInfo;
            
            // Track tokens by user for easier revocation
            _userTokens.AddOrUpdate(
                userId,
                _ => new HashSet<string> { refreshToken },
                (_, tokens) =>
                {
                    tokens.Add(refreshToken);
                    return tokens;
                });
            
            _logger.LogDebug("Stored refresh token for user {UserId} with expiration {Expiration}", userId, expiration);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<string> ValidateRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            
            if (!_tokens.TryGetValue(refreshToken, out var tokenInfo))
            {
                _logger.LogWarning("Refresh token not found during validation");
                return Task.FromResult<string>(null);
            }
            
            if (tokenInfo.IsRevoked)
            {
                _logger.LogWarning("Attempted to use revoked refresh token");
                
                // Potential token theft attempt - revoke all tokens in this family
                RevokeTokenFamilyAsync(tokenInfo.TokenFamily).GetAwaiter().GetResult();
                
                return Task.FromResult<string>(null);
            }
            
            if (tokenInfo.Expiration < DateTime.UtcNow)
            {
                _logger.LogWarning("Attempted to use expired refresh token");
                // Remove the expired token
                _tokens.TryRemove(refreshToken, out _);
                return Task.FromResult<string>(null);
            }
            
            return Task.FromResult(tokenInfo.UserId);
        }

        /// <inheritdoc />
        public Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            
            if (_tokens.TryGetValue(refreshToken, out var tokenInfo))
            {
                tokenInfo.IsRevoked = true;
                _logger.LogInformation("Revoked refresh token for user {UserId}", tokenInfo.UserId);
            }
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RevokeAllUserTokensAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));
            
            if (_userTokens.TryGetValue(userId, out var tokens))
            {
                foreach (var token in tokens)
                {
                    if (_tokens.TryGetValue(token, out var tokenInfo))
                    {
                        tokenInfo.IsRevoked = true;
                    }
                }
                
                _logger.LogInformation("Revoked all refresh tokens for user {UserId}", userId);
            }
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task PurgeExpiredTokensAsync()
        {
            var now = DateTime.UtcNow;
            var expiredTokens = 0;
            
            foreach (var (token, info) in _tokens)
            {
                if (info.Expiration < now || info.IsRevoked)
                {
                    if (_tokens.TryRemove(token, out _))
                    {
                        expiredTokens++;
                        
                        // Remove from user tokens collection
                        if (_userTokens.TryGetValue(info.UserId, out var userTokens))
                        {
                            userTokens.Remove(token);
                            
                            // If no more tokens for this user, remove the entry
                            if (userTokens.Count == 0)
                            {
                                _userTokens.TryRemove(info.UserId, out _);
                            }
                        }
                    }
                }
            }
            
            if (expiredTokens > 0)
            {
                _logger.LogInformation("Purged {Count} expired or revoked tokens", expiredTokens);
            }
            
            return Task.CompletedTask;
        }

        private Task RevokeTokenFamilyAsync(string tokenFamily)
        {
            if (string.IsNullOrEmpty(tokenFamily))
                return Task.CompletedTask;
            
            foreach (var (token, info) in _tokens)
            {
                if (info.TokenFamily == tokenFamily)
                {
                    info.IsRevoked = true;
                    _logger.LogWarning("Revoked token from family {TokenFamily} due to potential token theft", tokenFamily);
                }
            }
            
            return Task.CompletedTask;
        }
    }
}
