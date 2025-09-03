using Microsoft.Extensions.Logging;
using NlToSqlEngine.MCP;
using NlToSqlEngine.Models;
using NlToSqlEngine.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Progressive query executor that mimics Claude's step-by-step database analysis approach
    /// Each step builds upon the previous one, creating a comprehensive understanding
    /// </summary>
    public class QueryExecutorService : IQueryExecutor
    {
        private readonly IMcpToolsService _mcpTools;
        private readonly ILlmService _llmService;
        private readonly ILogger<QueryExecutorService> _logger;

        public QueryExecutorService(
            IMcpToolsService mcpTools,
            ILlmService llmService,
            ILogger<QueryExecutorService> logger)
        {
            _mcpTools = mcpTools;
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<QueryExecutionResult> ExecuteQueryPlanAsync(QueryPlan queryPlan)
        {
            _logger.LogInformation("⚡ Starting progressive query execution with {StepCount} steps", queryPlan.Steps.Count);

            var result = new QueryExecutionResult
            {
                Success = true,
                Steps = new List<QueryExecutionStep>(),
                ExecutionContext = new Dictionary<string, object>()
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                foreach (var planStep in queryPlan.Steps.OrderBy(s => s.StepNumber))
                {
                    _logger.LogInformation("🔄 Step {StepNumber}: {Description}",
                        planStep.StepNumber, planStep.Description);

                    var executionStep = await ExecutePlanStepAsync(planStep, result.ExecutionContext);
                    result.Steps.Add(executionStep);

                    // Update execution context with findings from this step
                    UpdateExecutionContext(result.ExecutionContext, executionStep);

                    // Log step completion
                    _logger.LogInformation("✅ Step {StepNumber} completed in {Duration}ms. Found {RecordCount} records",
                        planStep.StepNumber, executionStep.ExecutionTime.TotalMilliseconds, executionStep.Results?.Count ?? 0);

                    // Break if we encounter an error in a non-optional step
                    if (!string.IsNullOrEmpty(executionStep.Reasoning) &&
                        executionStep.Reasoning.Contains("Error") &&
                        !planStep.IsOptional)
                    {
                        _logger.LogWarning("⚠️ Step {StepNumber} had errors but continuing execution", planStep.StepNumber);
                    }
                }

                // The final data is typically from the last step that returned data
                var lastDataStep = result.Steps.LastOrDefault(s => s.Results?.Any() == true);
                result.FinalData = lastDataStep?.Results ?? new List<Dictionary<string, object>>();

                _logger.LogInformation("🎉 Query execution completed successfully in {TotalTime}ms. Final result: {RecordCount} records",
                    stopwatch.ElapsedMilliseconds, result.FinalData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Query execution failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            stopwatch.Stop();
            return result;
        }

        private async Task<QueryExecutionStep> ExecutePlanStepAsync(QueryPlanStep planStep, Dictionary<string, object> context)
        {
            var stopwatch = Stopwatch.StartNew();

            var executionStep = new QueryExecutionStep
            {
                StepNumber = planStep.StepNumber,
                StepType = planStep.StepType,
                Description = planStep.Description,
                SqlQuery = planStep.SqlTemplate,
                Purpose = planStep.Purpose,
                ExecutedAt = DateTime.UtcNow,
                Results = new List<Dictionary<string, object>>()
            };

            try
            {
                // Generate reasoning for this step using LLM
                executionStep.Reasoning = await _llmService.GetCompletionAsync(
                    "You are a database analyst explaining your reasoning for each step in the analysis process. Provide a clear, concise explanation of why this step is necessary and what we expect to learn from it. Keep it to 1-2 sentences and focus on the business value of the step.",
                    $"Step: {planStep.Description}\nContext: {JsonSerializer.Serialize(new { planStep.Purpose, Context = context }, new JsonSerializerOptions { WriteIndented = true })}\n\nExplain why this step is important:");

                _logger.LogDebug("💭 Step reasoning: {Reasoning}", executionStep.Reasoning);

                // Execute based on step type
                switch (planStep.StepType)
                {
                    case "DataExploration":
                        executionStep.Results = await ExecuteDataExplorationAsync(planStep, context);
                        break;

                    case "SchemaAnalysis":
                        executionStep.Results = await ExecuteSchemaAnalysisAsync(planStep, context);
                        break;

                    case "DataAnalysis":
                        executionStep.Results = await ExecuteDataAnalysisAsync(planStep, context);
                        break;

                    case "QueryConstruction":
                        executionStep.Results = await ExecuteQueryConstructionAsync(planStep, context);
                        break;

                    case "FinalQuery":
                        executionStep.Results = await ExecuteFinalQueryAsync(planStep, context);
                        break;

                    default:
                        _logger.LogWarning("⚠️ Unknown step type: {StepType}, treating as SQL query", planStep.StepType);
                        executionStep.Results = await ExecuteSqlQueryAsync(planStep.SqlTemplate);
                        break;
                }

                _logger.LogInformation("📊 Step {StepNumber} executed successfully. Retrieved {Count} records",
                    planStep.StepNumber, executionStep.Results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error executing step {StepNumber}: {Description}",
                    planStep.StepNumber, planStep.Description);

                executionStep.Reasoning = $"Error executing step: {ex.Message}";
                // Don't fail completely - continue with empty results
            }

            stopwatch.Stop();
            executionStep.ExecutionTime = stopwatch.Elapsed;

            return executionStep;
        }

        private async Task<List<Dictionary<string, object>>> ExecuteDataExplorationAsync(
            QueryPlanStep planStep,
            Dictionary<string, object> context)
        {
            _logger.LogDebug("🔍 Executing data exploration: {Description}", planStep.Description);

            // If SQL template is provided, execute it
            if (!string.IsNullOrEmpty(planStep.SqlTemplate) &&
                !planStep.SqlTemplate.Contains("-- Table structure analysis"))
            {
                return await ExecuteSqlQueryAsync(planStep.SqlTemplate);
            }

            // Otherwise, use MCP tools for exploration
            var tableName = GetTableNameFromParameters(planStep.Parameters);
            if (!string.IsNullOrEmpty(tableName))
            {
                var limit = GetIntFromParameters(planStep.Parameters, "limit", 5);
                return await _mcpTools.SampleRowsAsync(tableName, limit);
            }

            return new List<Dictionary<string, object>>();
        }

        private async Task<List<Dictionary<string, object>>> ExecuteSchemaAnalysisAsync(
            QueryPlanStep planStep,
            Dictionary<string, object> context)
        {
            _logger.LogDebug("🏗️ Executing schema analysis: {Description}", planStep.Description);

            var tableName = GetTableNameFromParameters(planStep.Parameters);
            if (!string.IsNullOrEmpty(tableName))
            {
                // Get table structure
                var columns = await _mcpTools.DescribeTableAsync(tableName);
                var stats = await _mcpTools.GetTableStatsAsync(tableName);
                var foreignKeys = await _mcpTools.GetForeignKeysAsync(tableName);

                // Convert to dictionary format for consistency
                var results = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["TableName"] = tableName,
                        ["ColumnCount"] = columns.Count,
                        ["RowCount"] = stats.RowCount,
                        ["SizeKB"] = stats.TotalKB,
                        ["ForeignKeyCount"] = foreignKeys.Count,
                        ["Columns"] = string.Join(", ", columns.Select(c => $"{c.ColumnName} ({c.DataType})").Take(5)),
                        ["TopColumns"] = columns.Take(5).Select(c => c.ColumnName).ToList()
                    }
                };

                return results;
            }

            return new List<Dictionary<string, object>>();
        }

        private async Task<List<Dictionary<string, object>>> ExecuteDataAnalysisAsync(
            QueryPlanStep planStep,
            Dictionary<string, object> context)
        {
            _logger.LogDebug("📈 Executing data analysis: {Description}", planStep.Description);

            return await ExecuteSqlQueryAsync(planStep.SqlTemplate);
        }

        private async Task<List<Dictionary<string, object>>> ExecuteQueryConstructionAsync(
            QueryPlanStep planStep,
            Dictionary<string, object> context)
        {
            _logger.LogDebug("🔨 Executing query construction: {Description}", planStep.Description);

            return await ExecuteSqlQueryAsync(planStep.SqlTemplate);
        }

        private async Task<List<Dictionary<string, object>>> ExecuteFinalQueryAsync(
            QueryPlanStep planStep,
            Dictionary<string, object> context)
        {
            _logger.LogDebug("🎯 Executing final query: {Description}", planStep.Description);

            // This is the most important step - the final answer to the user's question
            var results = await ExecuteSqlQueryAsync(planStep.SqlTemplate);

            _logger.LogInformation("🎉 Final query completed. Retrieved {Count} records as the answer", results.Count);

            return results;
        }

        private async Task<List<Dictionary<string, object>>> ExecuteSqlQueryAsync(string sqlQuery)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                return new List<Dictionary<string, object>>();
            }

            try
            {
                // Clean up the SQL query
                var cleanQuery = sqlQuery.Trim();
                if (cleanQuery.StartsWith("--"))
                {
                    return new List<Dictionary<string, object>>();
                }

                return await _mcpTools.ExecuteSelectQueryAsync(cleanQuery);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ SQL execution failed: {Query}", sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length)));
                throw;
            }
        }

        private void UpdateExecutionContext(Dictionary<string, object> context, QueryExecutionStep step)
        {
            // Store information from this step that might be useful for subsequent steps
            context[$"Step{step.StepNumber}_RecordCount"] = step.Results?.Count ?? 0;
            context[$"Step{step.StepNumber}_Type"] = step.StepType;

            if (step.Results?.Any() == true)
            {
                var firstRecord = step.Results.First();
                context[$"Step{step.StepNumber}_Columns"] = firstRecord.Keys.ToList();

                // Store sample data for reference
                context[$"Step{step.StepNumber}_SampleData"] = step.Results.Take(3).ToList();
            }

            // Update global context with discoveries
            if (step.StepType == "DataExploration" && step.Results?.Any() == true)
            {
                var recordCount = step.Results.Count;
                if (!context.ContainsKey("TotalRecordsExplored"))
                    context["TotalRecordsExplored"] = 0;

                context["TotalRecordsExplored"] = (int)context["TotalRecordsExplored"] + recordCount;
            }

            if (step.StepType == "FinalQuery")
            {
                context["FinalAnswerReady"] = true;
                context["FinalRecordCount"] = step.Results?.Count ?? 0;
            }
        }

        private string GetTableNameFromParameters(Dictionary<string, object> parameters)
        {
            if (parameters?.ContainsKey("tables") == true)
            {
                var tables = parameters["tables"];
                if (tables is string[] stringArray && stringArray.Length > 0)
                    return stringArray[0];
                if (tables is List<string> stringList && stringList.Count > 0)
                    return stringList[0];
                if (tables is string singleTable)
                    return singleTable;
            }

            return null;
        }

        private int GetIntFromParameters(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (parameters?.ContainsKey(key) == true)
            {
                if (int.TryParse(parameters[key]?.ToString(), out int result))
                    return result;
            }
            return defaultValue;
        }
    }
}