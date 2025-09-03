using Microsoft.Extensions.Logging;
using NlToSqlEngine.Models;
using NlToSqlEngine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// LLM-powered query planner that creates step-by-step execution strategies
    /// This replicates Claude's approach of building queries progressively through logical steps
    /// </summary>
    public class QueryPlannerService : IQueryPlanner
    {
        private readonly ILlmService _llmService;
        private readonly ILogger<QueryPlannerService> _logger;

        public QueryPlannerService(
            ILlmService llmService,
            ILogger<QueryPlannerService> logger)
        {
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<QueryPlan> CreateQueryPlanAsync(LlmQueryAnalysis llmAnalysis, SchemaContext schemaContext)
        {
            _logger.LogInformation("📝 Creating query execution plan for: {Intent}", llmAnalysis.QueryIntent);

            // Use LLM to generate the execution strategy
            var llmPlan = await GenerateLlmQueryPlanAsync(llmAnalysis, schemaContext);

            var queryPlan = new QueryPlan
            {
                QueryIntent = llmAnalysis.QueryIntent,
                Context = schemaContext,
                Parameters = llmAnalysis.Parameters,
                Strategy = llmPlan.Strategy,
                ConfidenceScore = llmPlan.ConfidenceScore,
                Steps = new List<QueryPlanStep>()
            };

            // Convert LLM plan steps to executable query steps
            var stepNumber = 1;
            foreach (var llmStep in llmPlan.Steps.OrderBy(s => s.Order))
            {
                var queryStep = await ConvertLlmStepToQueryStepAsync(llmStep, schemaContext, stepNumber++);
                if (queryStep != null)
                {
                    queryPlan.Steps.Add(queryStep);
                }
            }

            // Always ensure we have basic exploration steps if LLM plan is incomplete
            await EnsureEssentialStepsAsync(queryPlan, schemaContext);

            _logger.LogInformation("✅ Created execution plan with {StepCount} steps", queryPlan.Steps.Count);
            return queryPlan;
        }

        private async Task<LlmQueryPlan> GenerateLlmQueryPlanAsync(LlmQueryAnalysis analysis, SchemaContext context)
        {
            var systemPrompt = @"You are an expert database analyst creating step-by-step query execution plans.

Your task is to create a logical, progressive plan for analyzing a database to answer the user's question. 
Follow the methodology that Claude uses: start with exploration, build understanding, then construct the final query.

Given the query analysis and discovered schema, create a JSON execution plan with this structure:
{
    ""strategy"": ""Brief description of the overall approach"",
    ""steps"": [
        {
            ""order"": 1,
            ""action"": ""exploration|analysis|query_building|final_execution"",
            ""description"": ""What this step accomplishes"",
            ""parameters"": {
                ""tables"": [""table1"", ""table2""],
                ""operation"": ""sample|describe|join|aggregate"",
                ""limit"": number_if_applicable
            },
            ""reasoning"": ""Why this step is necessary"",
            ""expectedOutcome"": ""What we expect to learn""
        }
    ],
    ""reasoning"": ""Overall strategy explanation"",
    ""parameters"": {},
    ""confidenceScore"": 0.0-1.0
}

IMPORTANT PRINCIPLES:
1. Start with data exploration (sample key tables to understand structure)
2. Build understanding progressively (don't jump to complex queries immediately)
3. Use intermediate steps to validate assumptions
4. End with the final query that directly answers the question
5. Consider relationships between tables
6. Account for data quality and edge cases

Example for ""Top 20 users by usage"":
- Step 1: Sample UserRoleUsages table to understand usage metrics
- Step 2: Sample Users table to understand user information
- Step 3: Test joins between tables to verify relationships
- Step 4: Create aggregated query for usage metrics
- Step 5: Final query with proper sorting and limiting

Focus on creating a logical progression that builds knowledge step by step.";

            var availableTables = context.RelevantTables
                .Select(t => new {
                    t.TableName,
                    t.RelevanceScore,
                    Columns = t.Columns.Select(c => c.ColumnName).Take(5).ToList(),
                    RowCount = t.Stats?.RowCount ?? 0
                })
                .ToList();

            var userPrompt = $@"Query Analysis:
{JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true })}

Available Tables:
{JsonSerializer.Serialize(availableTables, new JsonSerializerOptions { WriteIndented = true })}

Schema Context:
- Intent: {context.QueryIntent}
- Search Terms: {string.Join(", ", context.SearchTerms)}
- Confidence: {context.ConfidenceScore:F2}

Create a step-by-step execution plan to answer this query effectively.";

            try
            {
                var response = await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
                var llmPlan = JsonSerializer.Deserialize<LlmQueryPlan>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("🧠 LLM generated plan with {StepCount} steps. Strategy: {Strategy}",
                    llmPlan.Steps?.Count ?? 0, llmPlan.Strategy);

                return llmPlan ?? CreateFallbackPlan(analysis, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate LLM query plan, using fallback");
                return CreateFallbackPlan(analysis, context);
            }
        }

        private async Task<QueryPlanStep> ConvertLlmStepToQueryStepAsync(
            LlmQueryPlanStep llmStep,
            SchemaContext context,
            int stepNumber)
        {
            var queryStep = new QueryPlanStep
            {
                StepNumber = stepNumber,
                Description = llmStep.Description,
                Purpose = llmStep.ExpectedOutcome,
                Reasoning = llmStep.Reasoning,
                Parameters = llmStep.Parameters ?? new Dictionary<string, object>()
            };

            // Convert LLM action to specific step type and SQL template
            switch (llmStep.Action.ToLower())
            {
                case "exploration":
                    queryStep.StepType = "DataExploration";
                    queryStep = await CreateExplorationStepAsync(queryStep, llmStep, context);
                    break;

                case "analysis":
                    queryStep.StepType = "DataAnalysis";
                    queryStep = await CreateAnalysisStepAsync(queryStep, llmStep, context);
                    break;

                case "query_building":
                    queryStep.StepType = "QueryConstruction";
                    queryStep = await CreateQueryBuildingStepAsync(queryStep, llmStep, context);
                    break;

                case "final_execution":
                    queryStep.StepType = "FinalQuery";
                    queryStep = await CreateFinalQueryStepAsync(queryStep, llmStep, context);
                    break;

                default:
                    _logger.LogWarning("⚠️ Unknown action type: {Action}", llmStep.Action);
                    return null;
            }

            return queryStep;
        }

        private async Task<QueryPlanStep> CreateExplorationStepAsync(
            QueryPlanStep step,
            LlmQueryPlanStep llmStep,
            SchemaContext context)
        {
            var tables = GetTablesFromParameters(llmStep.Parameters);
            var operation = GetStringFromParameters(llmStep.Parameters, "operation", "sample");
            var limit = GetIntFromParameters(llmStep.Parameters, "limit", 5);

            if (tables.Any() && operation == "sample")
            {
                var tableName = tables.First();
                step.SqlTemplate = $"SELECT TOP ({limit}) * FROM {tableName}";
                step.ExpectedColumns = new List<string> { "*" };
            }
            else if (tables.Any() && operation == "describe")
            {
                // This will be handled by MCP tools, not SQL
                step.SqlTemplate = "-- Table structure analysis via MCP tools";
                step.StepType = "SchemaAnalysis";
            }

            return step;
        }

        private async Task<QueryPlanStep> CreateAnalysisStepAsync(
            QueryPlanStep step,
            LlmQueryPlanStep llmStep,
            SchemaContext context)
        {
            var tables = GetTablesFromParameters(llmStep.Parameters);
            var operation = GetStringFromParameters(llmStep.Parameters, "operation", "aggregate");

            if (operation == "join" && tables.Count >= 2)
            {
                // Create a test join query
                var primaryTable = tables[0];
                var secondaryTable = tables[1];

                // Find likely join columns (simplified heuristic)
                var joinColumn = FindLikelyJoinColumn(primaryTable, secondaryTable, context);

                step.SqlTemplate = $@"SELECT TOP 5 
    t1.*, 
    t2.*
FROM {primaryTable} t1
INNER JOIN {secondaryTable} t2 ON t1.{joinColumn} = t2.{joinColumn}";
            }
            else if (operation == "aggregate")
            {
                // Create basic aggregation
                var table = tables.FirstOrDefault() ?? context.RelevantTables.FirstOrDefault()?.FullTableName;
                step.SqlTemplate = $@"SELECT 
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT UserId) as UniqueUsers
FROM {table}";
            }

            return step;
        }

        private async Task<QueryPlanStep> CreateQueryBuildingStepAsync(
            QueryPlanStep step,
            LlmQueryPlanStep llmStep,
            SchemaContext context)
        {
            // This step builds intermediate queries that contribute to the final answer
            var tables = GetTablesFromParameters(llmStep.Parameters);
            var limit = GetIntFromParameters(llmStep.Parameters, "limit");

            // Build a more complex aggregation query
            if (tables.Any())
            {
                var primaryTable = tables.First();
                step.SqlTemplate = BuildIntermediateQueryAsync(primaryTable, context, limit);
            }

            return step;
        }

        private async Task<QueryPlanStep> CreateFinalQueryStepAsync(
            QueryPlanStep step,
            LlmQueryPlanStep llmStep,
            SchemaContext context)
        {
            // This is the final query that directly answers the user's question
            var tables = GetTablesFromParameters(llmStep.Parameters);
            var limit = GetIntFromParameters(llmStep.Parameters, "limit", 20);

            step.SqlTemplate = BuildFinalQueryAsync(context, limit);
            step.ExpectedColumns = new List<string> { "UserId", "UserName", "TotalUsage", "Ranking" };

            return step;
        }

        private async Task EnsureEssentialStepsAsync(QueryPlan plan, SchemaContext context)
        {
            // Ensure we have at least basic exploration steps
            if (!plan.Steps.Any(s => s.StepType == "DataExploration"))
            {
                var explorationStep = new QueryPlanStep
                {
                    StepNumber = 1,
                    StepType = "DataExploration",
                    Description = "Explore primary tables to understand data structure",
                    SqlTemplate = $"SELECT TOP 5 * FROM {context.RelevantTables.FirstOrDefault()?.FullTableName ?? "Users"}",
                    Purpose = "Understand the structure and content of key tables",
                    Reasoning = "Essential first step to understand available data"
                };

                plan.Steps.Insert(0, explorationStep);

                // Renumber all steps
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    plan.Steps[i].StepNumber = i + 1;
                }
            }
        }

        private string BuildFinalQueryAsync(SchemaContext context, int limit)
        {
            // Build a comprehensive final query based on discovered schema
            var userTable = context.RelevantTables.FirstOrDefault(t =>
                t.TableName.ToLower().Contains("user") && !t.TableName.ToLower().Contains("role"));

            var usageTable = context.RelevantTables.FirstOrDefault(t =>
                t.TableName.ToLower().Contains("usage") || t.TableName.ToLower().Contains("role"));

            if (userTable != null && usageTable != null)
            {
                return $@"SELECT TOP {limit}
    u.UserId,
    u.SapUserName as UserName,
    u.FullName,
    u.Department,
    SUM(uru.UsedActions) as TotalUsedActions,
    SUM(uru.TotalActions) as TotalActions,
    COUNT(DISTINCT uru.RoleName) as RoleCount,
    MAX(uru.LastUse) as MostRecentUse,
    CASE 
        WHEN SUM(uru.TotalActions) > 0 
        THEN ROUND(CAST(SUM(uru.UsedActions) AS FLOAT) / SUM(uru.TotalActions) * 100, 2)
        ELSE 0 
    END as UsagePercentage
FROM {userTable.FullTableName} u
INNER JOIN {usageTable.FullTableName} uru ON u.UserId = uru.UserId
WHERE u.IsDeleted = 0 OR u.IsDeleted IS NULL
GROUP BY u.UserId, u.SapUserName, u.FullName, u.Department
ORDER BY TotalUsedActions DESC, RoleCount DESC, TotalActions DESC";
            }

            // Fallback query
            return $"SELECT TOP {limit} * FROM {context.RelevantTables.FirstOrDefault()?.FullTableName ?? "Users"} ORDER BY UserId";
        }

        private string BuildIntermediateQueryAsync(string tableName, SchemaContext context, int? limit)
        {
            return $@"SELECT 
    COUNT(*) as RecordCount,
    MIN(UserId) as MinUserId,
    MAX(UserId) as MaxUserId
FROM {tableName}";
        }

        private string FindLikelyJoinColumn(string table1, string table2, SchemaContext context)
        {
            // Simple heuristic to find join columns
            var table1Info = context.RelevantTables.FirstOrDefault(t => t.FullTableName == table1);
            var table2Info = context.RelevantTables.FirstOrDefault(t => t.FullTableName == table2);

            // Look for UserId as most common join column
            if (table1Info?.Columns.Any(c => c.ColumnName == "UserId") == true &&
                table2Info?.Columns.Any(c => c.ColumnName == "UserId") == true)
            {
                return "UserId";
            }

            // Look for any common Id column
            var table1Ids = table1Info?.Columns.Where(c => c.ColumnName.EndsWith("Id")).Select(c => c.ColumnName) ?? new List<string>();
            var table2Ids = table2Info?.Columns.Where(c => c.ColumnName.EndsWith("Id")).Select(c => c.ColumnName) ?? new List<string>();

            return table1Ids.Intersect(table2Ids).FirstOrDefault() ?? "Id";
        }

        private LlmQueryPlan CreateFallbackPlan(LlmQueryAnalysis analysis, SchemaContext context)
        {
            return new LlmQueryPlan
            {
                Strategy = "Fallback progressive analysis plan",
                ConfidenceScore = 0.5,
                Reasoning = "Generated fallback plan due to LLM processing error",
                Steps = new List<LlmQueryPlanStep>
                {
                    new LlmQueryPlanStep
                    {
                        Order = 1,
                        Action = "exploration",
                        Description = "Sample primary table data",
                        Parameters = new Dictionary<string, object>
                        {
                            ["tables"] = new[] { context.RelevantTables.FirstOrDefault()?.FullTableName ?? "Users" },
                            ["operation"] = "sample",
                            ["limit"] = 5
                        }
                    },
                    new LlmQueryPlanStep
                    {
                        Order = 2,
                        Action = "final_execution",
                        Description = "Execute final query",
                        Parameters = new Dictionary<string, object>
                        {
                            ["limit"] = analysis.Parameters.ContainsKey("limit") ? analysis.Parameters["limit"] : 20
                        }
                    }
                }
            };
        }

        private List<string> GetTablesFromParameters(Dictionary<string, object> parameters)
        {
            if (parameters?.ContainsKey("tables") == true)
            {
                if (parameters["tables"] is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    return jsonElement.EnumerateArray().Select(e => e.GetString()).ToList();
                }
                else if (parameters["tables"] is string[] stringArray)
                {
                    return stringArray.ToList();
                }
            }
            return new List<string>();
        }

        private string GetStringFromParameters(Dictionary<string, object> parameters, string key, string defaultValue = "")
        {
            return parameters?.ContainsKey(key) == true ? parameters[key]?.ToString() ?? defaultValue : defaultValue;
        }

        private int GetIntFromParameters(Dictionary<string, object> parameters, string key, int defaultValue = 0)
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