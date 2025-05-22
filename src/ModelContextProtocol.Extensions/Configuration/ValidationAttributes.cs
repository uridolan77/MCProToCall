using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;

namespace ModelContextProtocol.Extensions.Configuration
{
    /// <summary>
    /// Validates that a port number is valid
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidPortAttribute : ValidationAttribute
    {
        /// <inheritdoc/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is int port)
            {
                if (port < 1 || port > 65535)
                {
                    return new ValidationResult($"Port must be between 1 and 65535, but was {port}");
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a host is valid
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValidHostAttribute : ValidationAttribute
    {
        /// <inheritdoc/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string host && !string.IsNullOrWhiteSpace(host))
            {
                // Validate IP address or hostname
                if (!IPAddress.TryParse(host, out _) && 
                    !Uri.CheckHostName(host).Equals(UriHostNameType.Dns))
                {
                    return new ValidationResult($"'{host}' is not a valid IP address or hostname");
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a file exists
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FileExistsAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether the file is required
        /// </summary>
        public bool Required { get; set; } = true;

        /// <inheritdoc/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string path)
            {
                if (string.IsNullOrWhiteSpace(path) && !Required)
                    return ValidationResult.Success;

                if (!File.Exists(path))
                {
                    return new ValidationResult($"File not found: {path}");
                }
            }
            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates that a directory exists
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DirectoryExistsAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether to create the directory if it doesn't exist
        /// </summary>
        public bool CreateIfMissing { get; set; } = false;

        /// <inheritdoc/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string path && !string.IsNullOrWhiteSpace(path))
            {
                if (!Directory.Exists(path))
                {
                    if (CreateIfMissing)
                    {
                        try
                        {
                            Directory.CreateDirectory(path);
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to create directory {path}: {ex.Message}");
                        }
                    }
                    else
                    {
                        return new ValidationResult($"Directory not found: {path}");
                    }
                }
            }
            return ValidationResult.Success;
        }
    }
}
