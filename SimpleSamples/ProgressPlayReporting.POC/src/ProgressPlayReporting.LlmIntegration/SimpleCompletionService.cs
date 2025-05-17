using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models.Completion;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// A simple implementation of the ICompletionService interface for testing purposes
    /// </summary>
    public class SimpleCompletionService : ICompletionService
    {
        private readonly ILogger<SimpleCompletionService> _logger;

        /// <summary>
        /// Creates a new instance of SimpleCompletionService
        /// </summary>
        /// <param name="logger">The logger to use</param>
        public SimpleCompletionService(ILogger<SimpleCompletionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<CompletionResponse> CreateCompletionAsync(CompletionRequest request)
        {
            _logger.LogInformation("Creating completion with model: {ModelId}", request.ModelId);

            // Log the request details
            var firstMessage = request.Messages.Count > 0 ? request.Messages[0].Content : "No messages provided";
            _logger.LogInformation("First message: {MessageStart}...", 
                firstMessage.Length > 50 ? firstMessage.Substring(0, 50) + "..." : firstMessage);

            // Create a mock response
            var response = new CompletionResponse
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.ModelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = "This is a mock response from the SimpleCompletionService. In a real implementation, this would be a response from an actual LLM API."
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new CompletionUsage
                {
                    PromptTokens = 100,
                    CompletionTokens = 50,
                    TotalTokens = 150
                }
            };

            return Task.FromResult(response);
        }
    }
}
