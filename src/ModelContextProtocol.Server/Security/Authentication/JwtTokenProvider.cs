using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// JWT token provider implementation
    /// </summary>
    public class JwtTokenProvider : IJwtTokenProvider
    {
        private readonly ILogger<JwtTokenProvider> _logger;
        private readonly JwtOptions _options;
        private readonly ITokenStore _tokenStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenProvider"/> class
        /// </summary>
        /// <param name="options">JWT options</param>
        /// <param name="tokenStore">Token store</param>
        /// <param name="logger">Logger</param>
        public JwtTokenProvider(
            IOptions<JwtOptions> options,
            ITokenStore tokenStore,
            ILogger<JwtTokenProvider> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string> GenerateTokenAsync(IEnumerable<Claim> claims, DateTime expires)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_options.SecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Get username from claims
            var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(username))
            {
                // Store the token
                await _tokenStore.StoreTokenAsync(username, tokenString);
            }

            return tokenString;
        }

        /// <inheritdoc/>
        public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_options.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                // Check if the token is in the store
                var username = principal.Identity.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    var isValid = await _tokenStore.IsTokenValidAsync(username, token);
                    if (!isValid)
                    {
                        _logger.LogWarning("Token is not in the store for user {Username}", username);
                        return null;
                    }
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task RevokeTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var username = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;

                if (!string.IsNullOrEmpty(username))
                {
                    await _tokenStore.RevokeTokenAsync(username, token);
                    _logger.LogInformation("Token revoked for user {Username}", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke token");
            }
        }

        /// <inheritdoc/>
        public async Task<string> RefreshTokenAsync(string token, DateTime expires)
        {
            try
            {
                var principal = await ValidateTokenAsync(token);
                if (principal == null)
                {
                    _logger.LogWarning("Cannot refresh invalid token");
                    return null;
                }

                // Generate a new token with the same claims but a new expiration
                var newToken = await GenerateTokenAsync(principal.Claims, expires);

                // Revoke the old token
                await RevokeTokenAsync(token);

                return newToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh token");
                return null;
            }
        }

        /// <summary>
        /// Helper method to generate a token for a user with roles
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="roles">User roles</param>
        /// <returns>JWT token</returns>
        public async Task<string> GenerateTokenForUserAsync(string username, string[] roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username)
            };

            // Add roles as claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return await GenerateTokenAsync(claims, DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes));
        }
    }
}
