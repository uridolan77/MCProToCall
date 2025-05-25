using Microsoft.AspNetCore.Http;
using System.Text;

namespace ModelContextProtocol.Extensions.Testing.Chaos
{
    /// <summary>
    /// Introduces artificial latency
    /// </summary>
    public class LatencyFault : IChaosFault
    {
        private readonly TimeSpan _delay;

        public LatencyFault(TimeSpan delay)
        {
            _delay = delay;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            await Task.Delay(_delay, context.RequestAborted);
            
            // Continue with normal processing after delay
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Request processed with artificial latency");
        }
    }

    /// <summary>
    /// Returns an HTTP error response
    /// </summary>
    public class ErrorFault : IChaosFault
    {
        private readonly int _statusCode;

        public ErrorFault(int statusCode)
        {
            _statusCode = statusCode;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = _statusCode;
            
            var errorMessage = _statusCode switch
            {
                400 => "Bad Request (Chaos Fault)",
                401 => "Unauthorized (Chaos Fault)",
                403 => "Forbidden (Chaos Fault)",
                404 => "Not Found (Chaos Fault)",
                429 => "Too Many Requests (Chaos Fault)",
                500 => "Internal Server Error (Chaos Fault)",
                502 => "Bad Gateway (Chaos Fault)",
                503 => "Service Unavailable (Chaos Fault)",
                504 => "Gateway Timeout (Chaos Fault)",
                _ => $"HTTP {_statusCode} (Chaos Fault)"
            };

            await context.Response.WriteAsync(errorMessage);
        }
    }

    /// <summary>
    /// Simulates a timeout by hanging the request
    /// </summary>
    public class TimeoutFault : IChaosFault
    {
        private readonly TimeSpan _timeout;

        public TimeoutFault(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            try
            {
                // Wait for the timeout duration or until the request is cancelled
                await Task.Delay(_timeout, context.RequestAborted);
                
                // If we reach here, the timeout wasn't cancelled
                context.Response.StatusCode = 504; // Gateway Timeout
                await context.Response.WriteAsync("Request timed out (Chaos Fault)");
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled, which is expected behavior
                context.Response.StatusCode = 499; // Client Closed Request
            }
        }
    }

    /// <summary>
    /// Throws an exception to test error handling
    /// </summary>
    public class ExceptionFault : IChaosFault
    {
        private readonly string _message;

        public ExceptionFault(string message)
        {
            _message = message;
        }

        public Task ExecuteAsync(HttpContext context)
        {
            throw new ChaosException(_message);
        }
    }

    /// <summary>
    /// Simulates memory pressure by allocating large amounts of memory
    /// </summary>
    public class MemoryPressureFault : IChaosFault
    {
        private readonly int _sizeMb;

        public MemoryPressureFault(int sizeMb)
        {
            _sizeMb = sizeMb;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            // Allocate memory to simulate pressure
            var memoryHog = new List<byte[]>();
            
            try
            {
                for (int i = 0; i < _sizeMb; i++)
                {
                    // Allocate 1MB chunks
                    memoryHog.Add(new byte[1024 * 1024]);
                    
                    // Check if request was cancelled
                    if (context.RequestAborted.IsCancellationRequested)
                        break;
                }

                // Hold the memory for a short time
                await Task.Delay(TimeSpan.FromSeconds(5), context.RequestAborted);

                context.Response.StatusCode = 200;
                await context.Response.WriteAsync($"Memory pressure fault executed ({_sizeMb}MB allocated)");
            }
            finally
            {
                // Clear the memory
                memoryHog.Clear();
                GC.Collect();
            }
        }
    }

    /// <summary>
    /// Simulates network issues by corrupting response data
    /// </summary>
    public class CorruptionFault : IChaosFault
    {
        private readonly double _corruptionRate;

        public CorruptionFault(double corruptionRate = 0.1)
        {
            _corruptionRate = Math.Clamp(corruptionRate, 0.0, 1.0);
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            var originalResponse = "This is a normal response that will be corrupted";
            var corruptedResponse = CorruptString(originalResponse);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(corruptedResponse);
        }

        private string CorruptString(string input)
        {
            var random = new Random();
            var chars = input.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (random.NextDouble() < _corruptionRate)
                {
                    // Replace with a random character
                    chars[i] = (char)random.Next(32, 127);
                }
            }

            return new string(chars);
        }
    }

    /// <summary>
    /// Simulates slow network by sending response in small chunks with delays
    /// </summary>
    public class SlowNetworkFault : IChaosFault
    {
        private readonly TimeSpan _chunkDelay;
        private readonly int _chunkSize;

        public SlowNetworkFault(TimeSpan chunkDelay, int chunkSize = 10)
        {
            _chunkDelay = chunkDelay;
            _chunkSize = chunkSize;
        }

        public async Task ExecuteAsync(HttpContext context)
        {
            var message = "This response is being sent slowly to simulate network issues. " +
                         "Each chunk is delayed to test client timeout handling and patience.";

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";

            // Send response in small chunks with delays
            for (int i = 0; i < message.Length; i += _chunkSize)
            {
                var chunk = message.Substring(i, Math.Min(_chunkSize, message.Length - i));
                await context.Response.WriteAsync(chunk);
                await context.Response.Body.FlushAsync();

                if (i + _chunkSize < message.Length) // Don't delay after the last chunk
                {
                    await Task.Delay(_chunkDelay, context.RequestAborted);
                }
            }
        }
    }

    /// <summary>
    /// Exception thrown by chaos faults
    /// </summary>
    public class ChaosException : Exception
    {
        public ChaosException(string message) : base($"Chaos Fault: {message}")
        {
        }

        public ChaosException(string message, Exception innerException) 
            : base($"Chaos Fault: {message}", innerException)
        {
        }
    }
}
