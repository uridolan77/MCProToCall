namespace ModelContextProtocol.Server.Security.Authentication
{
    /// <summary>
    /// Result of an authorization check
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// Whether the request is authorized
        /// </summary>
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// The error code if not authorized
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// The error message if not authorized
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful authorization result
        /// </summary>
        public static AuthorizationResult Success()
        {
            return new AuthorizationResult
            {
                IsAuthorized = true
            };
        }

        /// <summary>
        /// Creates a failed authorization result
        /// </summary>
        public static AuthorizationResult Fail(int errorCode, string errorMessage)
        {
            return new AuthorizationResult
            {
                IsAuthorized = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
