using System.Threading.Tasks;
using NlToSqlEngine.Models;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Query executor that progressively executes the planned steps
    /// This mimics Claude's approach of building understanding through iterative queries
    /// </summary>
    public interface IQueryExecutor
    {
        /// <summary>
        /// Execute a complete query plan step by step, building understanding progressively
        /// This follows Claude's methodology of learning from each step to inform the next
        /// </summary>
        /// <param name="queryPlan">The step-by-step execution plan</param>
        /// <returns>Complete execution results with all intermediate steps and final data</returns>
        Task<QueryExecutionResult> ExecuteQueryPlanAsync(QueryPlan queryPlan);
    }

    /// <summary>
    /// Result of executing a complete query plan
    /// </summary>
    public class QueryExecutionResult
    {
        public List<QueryExecutionStep> Steps { get; set; } = new();
        public List<Dictionary<string, object>> FinalData { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> ExecutionContext { get; set; } = new();
    }
}