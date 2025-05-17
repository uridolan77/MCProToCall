using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server.Security.Authentication;

namespace ModelContextProtocol.Extensions.Security.Authentication
{
    /// <summary>
    /// Implementation of JWT token provider
    /// </summary>
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly JwtOptions _options;
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly ITokenStore _tokenStore;

        public JwtTokenProvider(IOptions<JwtOptions> options, ITokenStore tokenStore, ILogger<JwtTokenProvider> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<string> GenerateAccessTokenAsync(string userId, IEnumerable<string> roles, IDictionary<string, string> additionalClaims = null)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_options.SecretKey);

            var tokenId = Guid.NewGuid().ToString();
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, tokenId),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add roles
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // Add additional claims
            if (additionalClaims != null)
            {
                foreach (var claim in additionalClaims)
                {
                    claims.Add(new Claim(claim.Key, claim.Value));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <inheritdoc />
        public async Task<(string Token, DateTime Expiration)> GenerateRefreshTokenAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            // Generate a cryptographically strong random token
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            var refreshToken = Convert.ToBase64String(randomBytes);
            var expiration = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

            // Store the refresh token
            await _tokenStore.StoreRefreshTokenAsync(refreshToken, userId, expiration);

            return (refreshToken, expiration);
        }

        /// <inheritdoc />
        public async Task<bool> ValidateTokenAsync(string token)
        {
            var principal = await ValidateTokenAndGetPrincipalAsync(token);
            return principal != null;
        }

        /// <inheritdoc />
        public async Task<ClaimsPrincipal> ValidateTokenAndGetPrincipalAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_options.SecretKey);

                // Validate the token
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // Zero tolerance for token expiration
                }, out _);

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IDictionary<string, string>> GetClaimsFromTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token));

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                return jwtToken.Claims.ToDictionary(
                    claim => claim.Type,
                    claim => claim.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract claims from token");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<(string AccessToken, string RefreshToken, DateTime Expiration)> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));

            // Validate the refresh token
            var userId = await _tokenStore.ValidateRefreshTokenAsync(refreshToken);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Invalid refresh token attempt");
                throw new SecurityTokenException("Invalid refresh token");
            }

            // Get user roles (in a real implementation, you'd retrieve these from your user store)
            var roles = new List<string> { "User" }; // Replace with actual user roles

            // Generate new tokens
            var accessToken = await GenerateAccessTokenAsync(userId, roles);
            var (newRefreshToken, expiration) = await GenerateRefreshTokenAsync(userId);

            // Invalidate the old refresh token (token rotation)
            await _tokenStore.RevokeRefreshTokenAsync(refreshToken);

            return (accessToken, newRefreshToken, expiration);
        }

        /// <inheritdoc />
        public async Task RevokeTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));

            await _tokenStore.RevokeRefreshTokenAsync(refreshToken);
        }

        /// <inheritdoc />
        public async Task RevokeAllUserTokensAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));

            await _tokenStore.RevokeAllUserTokensAsync(userId);
        }
    }
}
