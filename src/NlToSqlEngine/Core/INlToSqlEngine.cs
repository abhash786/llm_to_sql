using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Core
{
    /// <summary>
    /// Main interface for the Natural Language to SQL Engine
    /// Mimics the approach Claude uses with MCP tools for database analysis
    /// </summary>
    public interface INlToSqlEngine
    {
        /// <summary>
        /// Process a natural language query and return comprehensive SQL analysis results
        /// This follows the same pattern as Claude's database exploration approach
        /// </summary>
        /// <param name="naturalLanguageQuery">The user's natural language question</param>
        /// <returns>Complete analysis results with execution steps, data, and insights</returns>
        Task<SqlQueryResult> ProcessNaturalLanguageQueryAsync(string naturalLanguageQuery);
    }
}