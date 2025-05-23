using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModelContextProtocol.Extensions.Performance
{
    /// <summary>
    /// Configuration options for compression middleware
    /// </summary>
    public class CompressionMiddlewareOptions
    {
        /// <summary>
        /// Whether to enable compression
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Compression level to use
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// Minimum response size in bytes to compress
        /// </summary>
        public int MinimumCompressionSize { get; set; } = 1024;

        /// <summary>
        /// Whether to enable Brotli compression (preferred over gzip)
        /// </summary>
        public bool EnableBrotli { get; set; } = true;

        /// <summary>
        /// Whether to enable Gzip compression
        /// </summary>
        public bool EnableGzip { get; set; } = true;

        /// <summary>
        /// Content types that should be compressed
        /// </summary>
        public string[] CompressibleContentTypes { get; set; } = new[]
        {
            "application/json",
            "application/xml",
            "text/plain",
            "text/html",
            "text/css",
            "text/javascript",
            "application/javascript",
            "application/rpc+json",
            "application/x-msgpack"
        };
    }

    /// <summary>
    /// High-performance compression middleware for MCP messages
    /// </summary>
    public class McpCompressionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<McpCompressionMiddleware> _logger;
        private readonly CompressionMiddlewareOptions _options;

        public McpCompressionMiddleware(
            RequestDelegate next,
            ILogger<McpCompressionMiddleware> logger,
            IOptions<CompressionMiddlewareOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.EnableCompression)
            {
                await _next(context);
                return;
            }

            // Check if client accepts compression
            var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
            var compressionMethod = DetermineCompressionMethod(acceptEncoding);

            if (compressionMethod == CompressionMethod.None)
            {
                await _next(context);
                return;
            }

            // Wrap the response stream with compression
            var originalBodyStream = context.Response.Body;
            var compressionStream = CreateCompressionStream(originalBodyStream, compressionMethod);

            context.Response.Body = compressionStream;

            // Set appropriate headers
            SetCompressionHeaders(context.Response, compressionMethod);

            var originalResponseSize = 0L;
            var compressedResponseSize = 0L;

            try
            {
                // Capture original response size
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                await _next(context);

                originalResponseSize = memoryStream.Length;

                // Only compress if response meets minimum size requirement
                if (memoryStream.Length >= _options.MinimumCompressionSize && 
                    ShouldCompressContentType(context.Response.ContentType))
                {
                    memoryStream.Position = 0;
                    
                    // Compress and write to original stream
                    using (compressionStream)
                    {
                        await memoryStream.CopyToAsync(compressionStream);
                    }

                    compressedResponseSize = GetCompressedSize(originalBodyStream);

                    _logger.LogDebug("Compressed response from {OriginalSize} to {CompressedSize} bytes ({CompressionRatio:P1})",
                        originalResponseSize, compressedResponseSize, 
                        1.0 - (double)compressedResponseSize / originalResponseSize);
                }
                else
                {
                    // Don't compress, write original content
                    context.Response.Headers.Remove("Content-Encoding");
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during response compression");
                
                // Fall back to uncompressed response
                context.Response.Headers.Remove("Content-Encoding");
                context.Response.Body = originalBodyStream;
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private CompressionMethod DetermineCompressionMethod(string acceptEncoding)
        {
            if (string.IsNullOrEmpty(acceptEncoding))
                return CompressionMethod.None;

            acceptEncoding = acceptEncoding.ToLowerInvariant();

            // Prefer Brotli over Gzip for better compression
            if (_options.EnableBrotli && acceptEncoding.Contains("br"))
                return CompressionMethod.Brotli;

            if (_options.EnableGzip && (acceptEncoding.Contains("gzip") || acceptEncoding.Contains("*")))
                return CompressionMethod.Gzip;

            return CompressionMethod.None;
        }

        private Stream CreateCompressionStream(Stream outputStream, CompressionMethod method)
        {
            return method switch
            {
                CompressionMethod.Gzip => new GZipStream(outputStream, _options.CompressionLevel, leaveOpen: true),
                CompressionMethod.Brotli => new BrotliStream(outputStream, _options.CompressionLevel, leaveOpen: true),
                _ => outputStream
            };
        }

        private void SetCompressionHeaders(HttpResponse response, CompressionMethod method)
        {
            var encoding = method switch
            {
                CompressionMethod.Gzip => "gzip",
                CompressionMethod.Brotli => "br",
                _ => null
            };

            if (!string.IsNullOrEmpty(encoding))
            {
                response.Headers["Content-Encoding"] = encoding;
                response.Headers["Vary"] = "Accept-Encoding";
            }
        }

        private bool ShouldCompressContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            var mainType = contentType.Split(';')[0].Trim().ToLowerInvariant();
            
            foreach (var compressibleType in _options.CompressibleContentTypes)
            {
                if (mainType.Equals(compressibleType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private long GetCompressedSize(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.Length;
            
            try
            {
                return stream.Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Supported compression methods
    /// </summary>
    public enum CompressionMethod
    {
        None,
        Gzip,
        Brotli
    }
}
