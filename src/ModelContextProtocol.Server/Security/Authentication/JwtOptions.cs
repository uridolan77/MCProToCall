namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// JWT options
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Gets or sets the secret key
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Gets or sets the issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Gets or sets the audience
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Gets or sets the access token expiration in minutes
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the refresh token expiration in days
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
