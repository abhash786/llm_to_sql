using Microsoft.Extensions.Logging;
using NlToSqlEngine.Components;
using NlToSqlEngine.Models;
using NlToSqlEngine.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace NlToSqlEngine.Core
{
    /// <summary>
    /// Main implementation of the NL to SQL Engine
    /// Orchestrates the entire process following Claude's methodology:
    /// 1. Schema Discovery & Exploration  
    /// 2. Query Planning using LLM
    /// 3. Progressive Query Execution
    /// 4. Business Analysis & Insights
    /// </summary>
    public class NlToSqlEngineService : INlToSqlEngine
    {
        private readonly ILlmService _llmService;
        private readonly ISchemaDiscovery _schemaDiscovery;
        private readonly IQueryPlanner _queryPlanner;
        private readonly IQueryExecutor _queryExecutor;
        private readonly IBusinessInsightAnalyzer _insightAnalyzer;
        private readonly ILogger<NlToSqlEngineService> _logger;

        public NlToSqlEngineService(
            ILlmService llmService,
            ISchemaDiscovery schemaDiscovery,
            IQueryPlanner queryPlanner,
            IQueryExecutor queryExecutor,
            IBusinessInsightAnalyzer insightAnalyzer,
            ILogger<NlToSqlEngineService> logger)
        {
            _llmService = llmService;
            _schemaDiscovery = schemaDiscovery;
            _queryPlanner = queryPlanner;
            _queryExecutor = queryExecutor;
            _insightAnalyzer = insightAnalyzer;
            _logger = logger;
        }

        public async Task<SqlQueryResult> ProcessNaturalLanguageQueryAsync(string naturalLanguageQuery)
        {
            var result = new SqlQueryResult
            {
                OriginalQuery = naturalLanguageQuery,
                Metadata = new ExecutionMetadata
                {
                    StartTime = DateTime.UtcNow,
                    ToolsUsed = new()
                }
            };

            try
            {
                _logger.LogInformation("🔍 Starting NL to SQL processing for: {Query}", naturalLanguageQuery);

                // Phase 1: LLM-Powered Query Analysis
                _logger.LogInformation("📋 Phase 1: Analyzing query intent with LLM...");
                var llmAnalysis = await AnalyzeQueryWithLlmAsync(naturalLanguageQuery);

                // Phase 2: Schema Discovery & Exploration (mimicking Claude's approach)
                _logger.LogInformation("🗂️ Phase 2: Schema discovery and exploration...");
                var schemaContext = await _schemaDiscovery.DiscoverRelevantSchemaAsync(llmAnalysis);
                result.Metadata.SchemasExplored = schemaContext.SchemasExplored;
                result.Metadata.TablesAnalyzed = schemaContext.RelevantTables.ConvertAll(t => t.FullTableName);

                // Phase 3: LLM-Powered Query Planning
                _logger.LogInformation("📝 Phase 3: Creating execution plan with LLM...");
                var queryPlan = await _queryPlanner.CreateQueryPlanAsync(llmAnalysis, schemaContext);

                // Phase 4: Progressive Query Execution (like Claude's step-by-step approach)
                _logger.LogInformation("⚡ Phase 4: Executing queries progressively...");
                var executionResult = await _queryExecutor.ExecuteQueryPlanAsync(queryPlan);
                result.ExecutionSteps = executionResult.Steps;
                result.Data = executionResult.FinalData;

                // Phase 5: Business Insights Generation (LLM-powered analysis)
                _logger.LogInformation("💡 Phase 5: Generating business insights...");
                result.Insights = await _insightAnalyzer.GenerateInsightsAsync(
                    naturalLanguageQuery,
                    executionResult,
                    schemaContext);

                // Phase 6: Generate Final Natural Language Answer
                _logger.LogInformation("📄 Phase 6: Generating final answer...");
                result.FinalAnswer = await _insightAnalyzer.GenerateFinalAnswerAsync(
                    naturalLanguageQuery,
                    result);

                result.Success = true;
                _logger.LogInformation("✅ Successfully processed query in {Duration}ms",
                    DateTime.UtcNow.Subtract(result.Metadata.StartTime).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing query: {Query}", naturalLanguageQuery);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                result.Metadata.EndTime = DateTime.UtcNow;
                result.Metadata.TotalExecutionTime = result.Metadata.EndTime - result.Metadata.StartTime;
                result.Metadata.TotalSteps = result.ExecutionSteps.Count;
            }

            return result;
        }

        private async Task<LlmQueryAnalysis> AnalyzeQueryWithLlmAsync(string naturalLanguageQuery)
        {
            var systemPrompt = @"You are an expert database analyst who helps understand natural language queries about databases.

Your task is to analyze the user's question and extract structured information that will help in database exploration and query generation.

Please analyze the query and return a JSON response with the following structure:
{
    ""queryIntent"": ""Brief description of what the user wants to know"",
    ""extractedEntities"": [""entity1"", ""entity2"", ...],
    ""parameters"": {
        ""limit"": number_if_top_n_query,
        ""timeframe"": ""if_any_time_constraints"",
        ""groupBy"": ""if_grouping_required"",
        ""orderBy"": ""if_sorting_specified"",
        ""filters"": {}
    },
    ""queryType"": ""TopN|Count|Sum|Average|List|Comparison|Analysis"",
    ""confidenceScore"": 0.0-1.0,
    ""reasoning"": ""Why you interpreted the query this way"",
    ""suggestedSearchTerms"": [""term1"", ""term2"", ...]
}

Focus on:
1. Business entities (users, customers, transactions, roles, etc.)
2. Metrics and measurements (usage, activity, counts, amounts)
3. Time constraints (recent, last week, since date, etc.)
4. Ranking/ordering requirements (top, highest, most, best)
5. Grouping requirements (by department, by type, etc.)

Example analysis:
Query: ""Top 20 users by usage""
- Intent: Find the 20 users with highest usage metrics
- Entities: users, usage
- Type: TopN
- Parameters: {""limit"": 20, ""orderBy"": ""usage DESC""}
- Search terms: [""user"", ""usage"", ""activity"", ""transaction""]";

            var userPrompt = $"Analyze this database query: '{naturalLanguageQuery}'";
            _logger.LogInformation(userPrompt);
            try
            {
                var response = await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
                var analysis = JsonSerializer.Deserialize<LlmQueryAnalysis>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation(response);
                return analysis ?? CreateFallbackAnalysis(naturalLanguageQuery);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to analyze query with LLM");
                return CreateFallbackAnalysis(naturalLanguageQuery);
            }
        }

        private LlmQueryAnalysis CreateFallbackAnalysis(string query)
        {
            return new LlmQueryAnalysis
            {
                QueryIntent = "General data analysis and retrieval",
                ConfidenceScore = 0.3,
                Reasoning = "Fallback analysis due to LLM processing error",
                QueryType = "Analysis",
                ExtractedEntities = new List<string> { "data" },
                Parameters = new Dictionary<string, object>(),
                SuggestedSearchTerms = new List<string> { "user", "usage", "activity" }
            };
        }
    }
}
