using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProgressPlayReporting.Core.Interfaces
{
    /// <summary>
    /// Interface for LLM Gateway that handles communication with large language models
    /// </summary>
    public interface ILlmGateway
    {
        /// <summary>
        /// Generates a completion response from the LLM based on the provided prompt
        /// </summary>
        /// <param name="prompt">The text prompt to send to the LLM</param>
        /// <param name="options">Optional parameters to control the generation</param>
        /// <returns>The generated text from the LLM</returns>
        Task<string> GenerateCompletionAsync(string prompt, LlmRequestOptions options = null);
        
        /// <summary>
        /// Generates a response from the LLM using a structured conversation
        /// </summary>
        /// <param name="messages">List of messages representing the conversation</param>
        /// <param name="options">Optional parameters to control the generation</param>
        /// <returns>The generated response from the LLM</returns>
        Task<string> GenerateConversationResponseAsync(IList<LlmMessage> messages, LlmRequestOptions options = null);
    }

    /// <summary>
    /// Represents a message in a conversation with an LLM
    /// </summary>
    public class LlmMessage
    {
        /// <summary>
        /// The role of the message sender (system, user, assistant)
        /// </summary>
        public string Role { get; set; }
        
        /// <summary>
        /// The content of the message
        /// </summary>
        public string Content { get; set; }
    }

    /// <summary>
    /// Options for LLM request customization
    /// </summary>
    public class LlmRequestOptions
    {
        /// <summary>
        /// Controls randomness: 0 = deterministic, 1 = maximum creativity
        /// </summary>
        public float? Temperature { get; set; }
        
        /// <summary>
        /// Maximum number of tokens to generate
        /// </summary>
        public int? MaxTokens { get; set; }
        
        /// <summary>
        /// Stop sequences that will cause the model to stop generating
        /// </summary>
        public List<string> StopSequences { get; set; }
        
        /// <summary>
        /// Include input tokens in usage calculations
        /// </summary>
        public bool CountInputTokens { get; set; } = true;
        
        /// <summary>
        /// Additional model-specific parameters
        /// </summary>
        public Dictionary<string, object> ModelParameters { get; set; } = new Dictionary<string, object>();
    }
}
