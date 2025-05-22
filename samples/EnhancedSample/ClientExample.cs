using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnhancedSample
{
    /// <summary>
    /// Example client program
    /// </summary>
    public class ClientExample
    {
        /// <summary>
        /// Runs the client example
        /// </summary>
        public static async Task RunAsync()
        {
            Console.WriteLine("Starting client example...");

            using var client = new StreamingClient("ws://localhost:8080/ws");
            
            try
            {
                await client.ConnectAsync();
                Console.WriteLine("Connected to server");

                // Get server capabilities
                var capabilities = await client.CallMethodAsync<dynamic>("system.getCapabilities");
                Console.WriteLine($"Server version: {capabilities.version}");
                Console.WriteLine($"Streaming supported: {capabilities.features.streaming}");

                // Call regular method
                var pingResult = await client.CallMethodAsync<dynamic>("system.ping");
                Console.WriteLine($"Ping result: {pingResult.pong}");

                // Call cached method
                Console.WriteLine("Calling cached method (first call)...");
                var start = DateTime.UtcNow;
                var cachedResult1 = await client.CallMethodAsync<dynamic>("demo.cached");
                var elapsed1 = (DateTime.UtcNow - start).TotalMilliseconds;
                Console.WriteLine($"Cached result: {cachedResult1.message} (took {elapsed1}ms)");

                // Call cached method again (should be faster)
                Console.WriteLine("Calling cached method (second call)...");
                start = DateTime.UtcNow;
                var cachedResult2 = await client.CallMethodAsync<dynamic>("demo.cached");
                var elapsed2 = (DateTime.UtcNow - start).TotalMilliseconds;
                Console.WriteLine($"Cached result: {cachedResult2.message} (took {elapsed2}ms)");
                Console.WriteLine($"Cache speedup: {elapsed1 / elapsed2:F1}x");

                // Call streaming method
                Console.WriteLine("Calling streaming method...");
                var streamId = await client.CallStreamingMethodAsync("system.streamingPing", new { count = 10 });
                Console.WriteLine($"Stream started with ID: {streamId}");

                // Consume stream
                Console.WriteLine("Consuming stream...");
                await foreach (var item in client.ConsumeStreamAsync<dynamic>(streamId))
                {
                    Console.WriteLine($"Stream item: {item.message} at {item.timestamp}");
                }
                Console.WriteLine("Stream completed");

                // Call LLM streaming method
                Console.WriteLine("Calling LLM streaming method...");
                var llmStreamId = await client.CallStreamingMethodAsync("llm.generate", new 
                { 
                    prompt = "Tell me a story", 
                    maxTokens = 100 
                });
                Console.WriteLine($"LLM stream started with ID: {llmStreamId}");

                // Consume LLM stream
                Console.WriteLine("Consuming LLM stream...");
                var text = "";
                await foreach (var item in client.ConsumeStreamAsync<dynamic>(llmStreamId))
                {
                    Console.Write(item.token + " ");
                    text += item.token + " ";
                }
                Console.WriteLine("\nGenerated text: " + text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("Client example completed");
        }
    }
}
