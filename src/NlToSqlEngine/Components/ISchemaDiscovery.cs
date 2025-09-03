using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Schema discovery service that mimics Claude's database exploration approach
    /// This follows the exact same methodology I used to analyze your database:
    /// 1. Schema reconnaissance
    /// 2. Table discovery
    /// 3. Search-based relevance finding
    /// 4. Structure analysis
    /// 5. Relationship discovery
    /// 6. Data sampling
    /// </summary>
    public interface ISchemaDiscovery
    {
        /// <summary>
        /// Discover relevant database schema based on LLM query analysis
        /// This replicates the multi-step exploration process Claude uses
        /// </summary>
        /// <param name="llmAnalysis">LLM's analysis of the natural language query</param>
        /// <returns>Schema context with relevant tables and their metadata</returns>
        Task<SchemaContext> DiscoverRelevantSchemaAsync(LlmQueryAnalysis llmAnalysis);
    }
}