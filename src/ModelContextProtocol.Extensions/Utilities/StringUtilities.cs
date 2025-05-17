using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModelContextProtocol.Extensions.Utilities
{
    /// <summary>
    /// Utility methods for string operations
    /// </summary>
    public static class StringUtilities
    {
        private static readonly Regex ConnectionStringPasswordRegex = new Regex(@"(Password|PWD)=([^;]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringUserIdRegex = new Regex(@"(User ID|UID)=([^;]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringAccountKeyRegex = new Regex(@"(AccountKey|AccessKey)=([^;]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringSharedAccessSignatureRegex = new Regex(@"(SharedAccessSignature)=([^;]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringApiKeyRegex = new Regex(@"(ApiKey|API_KEY)=([^;]*)", RegexOptions.IgnoreCase);
        private static readonly Regex ConnectionStringTokenRegex = new Regex(@"(Token)=([^;]*)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Sanitizes a connection string by removing sensitive information
        /// </summary>
        /// <param name="connectionString">The connection string to sanitize</param>
        /// <returns>A sanitized connection string</returns>
        public static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return string.Empty;
            }

            // Replace sensitive information with placeholders
            string sanitized = ConnectionStringPasswordRegex.Replace(connectionString, "$1=*****");
            sanitized = ConnectionStringUserIdRegex.Replace(sanitized, "$1=*****");
            sanitized = ConnectionStringAccountKeyRegex.Replace(sanitized, "$1=*****");
            sanitized = ConnectionStringSharedAccessSignatureRegex.Replace(sanitized, "$1=*****");
            sanitized = ConnectionStringApiKeyRegex.Replace(sanitized, "$1=*****");
            sanitized = ConnectionStringTokenRegex.Replace(sanitized, "$1=*****");

            return sanitized;
        }

        /// <summary>
        /// Sanitizes a URL by removing sensitive information
        /// </summary>
        /// <param name="url">The URL to sanitize</param>
        /// <returns>A sanitized URL</returns>
        public static string SanitizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            try
            {
                var uri = new Uri(url);
                
                // Check if the URL contains credentials
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    // Replace credentials with a placeholder
                    var sanitizedUrl = url.Replace(uri.UserInfo, "*****");
                    return sanitizedUrl;
                }
                
                // Check for query parameters that might contain sensitive information
                if (!string.IsNullOrEmpty(uri.Query))
                {
                    var queryParams = uri.Query.TrimStart('?').Split('&');
                    var sanitizedParams = new List<string>();
                    
                    foreach (var param in queryParams)
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            var key = parts[0].ToLowerInvariant();
                            
                            // Check if the parameter name suggests it contains sensitive information
                            if (key.Contains("password") || key.Contains("pwd") || 
                                key.Contains("key") || key.Contains("token") || 
                                key.Contains("secret") || key.Contains("auth") ||
                                key.Contains("credential") || key.Contains("api"))
                            {
                                sanitizedParams.Add($"{parts[0]}=*****");
                            }
                            else
                            {
                                sanitizedParams.Add(param);
                            }
                        }
                        else
                        {
                            sanitizedParams.Add(param);
                        }
                    }
                    
                    var sanitizedQuery = string.Join("&", sanitizedParams);
                    var sanitizedUrl = url.Replace(uri.Query, $"?{sanitizedQuery}");
                    return sanitizedUrl;
                }
            }
            catch
            {
                // If we can't parse the URL, return it as is
                return url;
            }
            
            return url;
        }

        /// <summary>
        /// Truncates a string to a maximum length
        /// </summary>
        /// <param name="input">The input string</param>
        /// <param name="maxLength">The maximum length</param>
        /// <param name="addEllipsis">Whether to add an ellipsis when truncating</param>
        /// <returns>The truncated string</returns>
        public static string Truncate(string input, int maxLength, bool addEllipsis = true)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            {
                return input;
            }
            
            return addEllipsis
                ? input.Substring(0, maxLength - 3) + "..."
                : input.Substring(0, maxLength);
        }

        /// <summary>
        /// Generates a random string
        /// </summary>
        /// <param name="length">The length of the string</param>
        /// <param name="includeSpecialChars">Whether to include special characters</param>
        /// <returns>A random string</returns>
        public static string GenerateRandomString(int length, bool includeSpecialChars = false)
        {
            const string alphanumericChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
            
            var chars = includeSpecialChars
                ? alphanumericChars + specialChars
                : alphanumericChars;
            
            var random = new Random();
            var result = new StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Checks if a string is a valid JSON
        /// </summary>
        /// <param name="input">The input string</param>
        /// <returns>True if the string is a valid JSON, false otherwise</returns>
        public static bool IsValidJson(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }
            
            input = input.Trim();
            
            if ((input.StartsWith("{") && input.EndsWith("}")) || // Object
                (input.StartsWith("[") && input.EndsWith("]")))   // Array
            {
                try
                {
                    System.Text.Json.JsonDocument.Parse(input);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
    }
}
