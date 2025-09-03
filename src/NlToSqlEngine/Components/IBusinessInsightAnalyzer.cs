using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Business insight analyzer that generates meaningful business context and recommendations
    /// This mimics Claude's ability to provide business insights and actionable recommendations
    /// based on the data analysis results
    /// </summary>
    public interface IBusinessInsightAnalyzer
    {
        /// <summary>
        /// Generate comprehensive business insights from query execution results
        /// This provides the business context and actionable recommendations that Claude excels at
        /// </summary>
        /// <param name="originalQuery">The user's original natural language question</param>
        /// <param name="executionResult">The complete query execution results</param>
        /// <param name="schemaContext">The database schema context</param>
        /// <returns>Business insights with metrics, patterns, and recommendations</returns>
        Task<BusinessInsights> GenerateInsightsAsync(
            string originalQuery,
            QueryExecutionResult executionResult,
            SchemaContext schemaContext);

        /// <summary>
        /// Generate the final natural language answer based on complete analysis results
        /// </summary>
        /// <param name="originalQuery">The user's original question</param>
        /// <param name="queryResult">The complete SQL query result with insights</param>
        /// <returns>A natural language answer that directly responds to the user's question</returns>
        Task<string> GenerateFinalAnswerAsync(string originalQuery, SqlQueryResult queryResult);
    }
}