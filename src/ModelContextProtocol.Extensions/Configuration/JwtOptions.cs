namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// JWT configuration options
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Secret key for signing tokens
        /// </summary>
        public string SecretKey { get; set; } = "DefaultSecretKeyThatShouldBeChangedInProduction";

        /// <summary>
        /// Token issuer
        /// </summary>
        public string Issuer { get; set; } = "ModelContextProtocol";

        /// <summary>
        /// Token audience
        /// </summary>
        public string Audience { get; set; } = "ModelContextProtocolClients";

        /// <summary>
        /// Access token expiration in minutes
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Refresh token expiration in days
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
