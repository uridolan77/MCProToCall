using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models.Completion;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// A simple implementation of ICompletionService that returns mock responses for testing purposes
    /// </summary>
    public class MockCompletionService : ICompletionService
    {
        private readonly ILogger<MockCompletionService> _logger;

        /// <summary>
        /// Initializes a new instance of the MockCompletionService class
        /// </summary>
        /// <param name="logger">Logger for the service</param>
        public MockCompletionService(ILogger<MockCompletionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a completion response based on the request
        /// </summary>
        /// <param name="request">The completion request</param>
        /// <returns>A mock completion response</returns>
        public Task<CompletionResponse> CreateCompletionAsync(CompletionRequest request)
        {
            _logger.LogInformation("Generating mock completion for model: {ModelId}", request.ModelId);

            // Create a simple mock response
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
                            Content = "This is a mock response from the LLM integration service."
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
