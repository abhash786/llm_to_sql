using System.Threading.Tasks;

namespace NlToSqlEngine.Services
{
    /// <summary>
    /// Interface for LLM service abstraction
    /// Supports OpenAI, Azure OpenAI, or other LLM providers
    /// </summary>
    public interface ILlmService
    {
        /// <summary>
        /// Get a completion from the LLM given system and user prompts
        /// </summary>
        /// <param name="systemPrompt">System instructions for the LLM</param>
        /// <param name="userPrompt">User's input/question</param>
        /// <returns>LLM's response</returns>
        Task<string> GetCompletionAsync(string systemPrompt, string userPrompt);

        /// <summary>
        /// Get a completion with conversation history
        /// </summary>
        /// <param name="messages">List of conversation messages</param>
        /// <returns>LLM's response</returns>
        Task<string> GetCompletionAsync(List<LlmMessage> messages);
    }

    public class LlmMessage
    {
        public string Role { get; set; } // system, user, assistant
        public string Content { get; set; }
    }
}