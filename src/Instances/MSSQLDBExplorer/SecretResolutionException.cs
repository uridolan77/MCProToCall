using System;

namespace PPrePorter.Core.Services
{
    /// <summary>
    /// Exception thrown when a secret cannot be resolved
    /// </summary>
    public class SecretResolutionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the SecretResolutionException class
        /// </summary>
        public SecretResolutionException() : base() { }

        /// <summary>
        /// Initializes a new instance of the SecretResolutionException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public SecretResolutionException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SecretResolutionException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public SecretResolutionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
