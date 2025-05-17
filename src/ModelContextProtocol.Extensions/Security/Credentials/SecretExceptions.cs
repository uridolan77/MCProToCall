using System;

namespace ModelContextProtocol.Extensions.Security.Credentials
{
    /// <summary>
    /// Exception thrown when a secret is not found
    /// </summary>
    public class SecretNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the SecretNotFoundException class
        /// </summary>
        public SecretNotFoundException() : base() { }

        /// <summary>
        /// Initializes a new instance of the SecretNotFoundException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public SecretNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SecretNotFoundException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public SecretNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when there is an error accessing a secret
    /// </summary>
    public class SecretAccessException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the SecretAccessException class
        /// </summary>
        public SecretAccessException() : base() { }

        /// <summary>
        /// Initializes a new instance of the SecretAccessException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public SecretAccessException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SecretAccessException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public SecretAccessException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when there is an error rotating a secret
    /// </summary>
    public class SecretRotationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the SecretRotationException class
        /// </summary>
        public SecretRotationException() : base() { }

        /// <summary>
        /// Initializes a new instance of the SecretRotationException class with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public SecretRotationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SecretRotationException class with a message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public SecretRotationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
