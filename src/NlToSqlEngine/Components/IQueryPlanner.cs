using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Query planning service that creates step-by-step execution plans
    /// This mimics Claude's progressive approach to building complex queries
    /// by breaking them down into logical, incremental steps
    /// </summary>
    public interface IQueryPlanner
    {
        /// <summary>
        /// Create a comprehensive query execution plan based on LLM analysis and schema context
        /// This follows Claude's methodology of progressive query building
        /// </summary>
        /// <param name="llmAnalysis">LLM's understanding of the query</param>
        /// <param name="schemaContext">Discovered schema information</param>
        /// <returns>Step-by-step execution plan</returns>
        Task<QueryPlan> CreateQueryPlanAsync(LlmQueryAnalysis llmAnalysis, SchemaContext schemaContext);
    }
}