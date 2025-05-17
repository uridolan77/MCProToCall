using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models.Completion;
using Microsoft.Extensions.Logging;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// A simple implementation of ICompletionService for testing purposes
    /// </summary>
    public class CompletionService : ICompletionService
    {
        private readonly ILogger<CompletionService> _logger;

        /// <summary>
        /// Creates a new instance of CompletionService
        /// </summary>
        /// <param name="logger">The logger to use</param>
        public CompletionService(ILogger<CompletionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<CompletionResponse> CreateCompletionAsync(CompletionRequest request)
        {
            _logger.LogInformation("Creating completion with model: {ModelId}", request.ModelId);

            // In a real implementation, this would call an API like OpenAI
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
                            Content = "This is a mock response from the CompletionService. In a real implementation, this would be a response from an LLM API."
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new CompletionUsage
                {
                    PromptTokens = 10,
                    CompletionTokens = 10,
                    TotalTokens = 20
                }
            };

            return Task.FromResult(response);
        }
    }
}
