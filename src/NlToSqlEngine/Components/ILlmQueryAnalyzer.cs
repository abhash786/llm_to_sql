using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// LLM-powered query analyzer that understands natural language queries
    /// This is the brain of the system that replaces hardcoded pattern matching
    /// </summary>
    public interface ILlmQueryAnalyzer
    {
        /// <summary>
        /// Analyze a natural language query using LLM to understand intent, entities, and parameters
        /// </summary>
        /// <param name="naturalLanguageQuery">The user's question in natural language</param>
        /// <returns>Structured analysis of the query</returns>
        Task<LlmQueryAnalysis> AnalyzeQueryAsync(string naturalLanguageQuery);

        /// <summary>
        /// Generate the final natural language answer based on execution results
        /// </summary>
        /// <param name="originalQuery">The original user question</param>
        /// <param name="queryResult">The complete execution result with data and insights</param>
        /// <returns>A natural language answer that directly responds to the user's question</returns>
        Task<string> GenerateFinalAnswerAsync(string originalQuery, SqlQueryResult queryResult);

        /// <summary>
        /// Generate reasoning for a specific step in the analysis process
        /// </summary>
        /// <param name="stepDescription">What we're trying to accomplish</param>
        /// <param name="context">Current context and discoveries</param>
        /// <returns>Human-readable reasoning for the step</returns>
        Task<string> GenerateStepReasoningAsync(string stepDescription, object context);
    }
}