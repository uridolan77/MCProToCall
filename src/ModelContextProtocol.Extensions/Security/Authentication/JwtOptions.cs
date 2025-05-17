namespace ModelContextProtocol.Extensions.Security.Authentication
{
    /// <summary>
    /// JWT configuration options
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Secret key used for signing tokens
        /// </summary>
        public string SecretKey { get; set; }
        
        /// <summary>
        /// Token issuer
        /// </summary>
        public string Issuer { get; set; }
        
        /// <summary>
        /// Token audience
        /// </summary>
        public string Audience { get; set; }
        
        /// <summary>
        /// Access token expiration time in minutes (default: 15 minutes)
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        
        /// <summary>
        /// Refresh token expiration time in days (default: 7 days)
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
