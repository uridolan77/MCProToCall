using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models.Completion;
using Microsoft.Extensions.Logging;
using ProgressPlayReporting.Core.Interfaces;

namespace ProgressPlayReporting.LlmIntegration
{
    /// <summary>
    /// Implementation of ILlmGateway that uses the LLMGateway library
    /// </summary>
    public class LlmGatewayService : ILlmGateway
    {
        private readonly ICompletionService _completionService;
        private readonly ILogger<LlmGatewayService> _logger;
        private readonly string _defaultModelId;

        /// <summary>
        /// Creates a new instance of LlmGatewayService
        /// </summary>
        /// <param name="completionService">The completion service to use</param>
        /// <param name="logger">The logger to use</param>
        /// <param name="defaultModelId">The default model ID to use if not specified in options</param>
        public LlmGatewayService(
            ICompletionService completionService,
            ILogger<LlmGatewayService> logger,
            string defaultModelId = "gpt-4")
        {
            _completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultModelId = defaultModelId;
        }

        /// <inheritdoc />
        public async Task<string> GenerateCompletionAsync(string prompt, LlmRequestOptions options = null)
        {
            try
            {
                _logger.LogInformation("Generating completion for prompt: {PromptStart}...", 
                    prompt.Length > 50 ? prompt.Substring(0, 50) + "..." : prompt);

                var request = CreateCompletionRequest(prompt, options);
                var response = await _completionService.CreateCompletionAsync(request);

                if (response?.Choices?.Count > 0 && !string.IsNullOrEmpty(response.Choices[0].Message?.Content))
                {
                    return response.Choices[0].Message.Content;
                }

                _logger.LogWarning("Received empty response from LLM");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating completion");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string> GenerateConversationResponseAsync(IList<LlmMessage> messages, LlmRequestOptions options = null)
        {
            try
            {
                _logger.LogInformation("Generating conversation response for {MessageCount} messages", messages.Count);

                var request = CreateConversationRequest(messages, options);
                var response = await _completionService.CreateCompletionAsync(request);

                if (response?.Choices?.Count > 0 && !string.IsNullOrEmpty(response.Choices[0].Message?.Content))
                {
                    return response.Choices[0].Message.Content;
                }

                _logger.LogWarning("Received empty response from LLM");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating conversation response");
                throw;
            }
        }

        private CompletionRequest CreateCompletionRequest(string prompt, LlmRequestOptions options)
        {
            var messages = new List<Message>
            {
                new Message { Role = "user", Content = prompt }
            };

            return CreateRequestWithOptions(messages, options);
        }

        private CompletionRequest CreateConversationRequest(IList<LlmMessage> messages, LlmRequestOptions options)
        {
            var requestMessages = messages.Select(m => new Message
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            return CreateRequestWithOptions(requestMessages, options);
        }

        private CompletionRequest CreateRequestWithOptions(List<Message> messages, LlmRequestOptions options)
        {
            var request = new CompletionRequest
            {
                ModelId = options?.ModelParameters?.ContainsKey("model_id") 
                    ? options.ModelParameters["model_id"].ToString() 
                    : _defaultModelId,
                Messages = messages,
                MaxTokens = options?.MaxTokens,
                Temperature = options?.Temperature,
                Stop = options?.StopSequences
            };

            // Apply any additional model parameters
            if (options?.ModelParameters != null)
            {
                foreach (var param in options.ModelParameters)
                {
                    // Skip model_id as it's already handled
                    if (param.Key == "model_id") continue;

                    // Map common parameters
                    switch (param.Key)
                    {
                        case "top_p":
                            request.TopP = Convert.ToDouble(param.Value);
                            break;
                        case "n":
                            request.N = Convert.ToInt32(param.Value);
                            break;
                        case "stream":
                            request.Stream = Convert.ToBoolean(param.Value);
                            break;
                        // More parameters can be mapped here as needed
                    }
                }
            }

            return request;
        }
    }
}
