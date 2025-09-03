using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NlToSqlEngine.Configuration;
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
    /// LLM-powered implementation that uses OpenAI/Azure OpenAI to understand queries
    /// This replaces pattern matching with intelligent natural language understanding
    /// </summary>
    public class LlmQueryAnalyzerService : ILlmQueryAnalyzer
    {
        private readonly ILlmService _llmService;
        private readonly ILogger<LlmQueryAnalyzerService> _logger;

        public LlmQueryAnalyzerService(
            ILlmService llmService,
            ILogger<LlmQueryAnalyzerService> logger)
        {
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<LlmQueryAnalysis> AnalyzeQueryAsync(string naturalLanguageQuery)
        {
            _logger.LogInformation("🧠 Analyzing query with LLM: {Query}", naturalLanguageQuery);

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

            try
            {
                var response = await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
                var analysis = JsonSerializer.Deserialize<LlmQueryAnalysis>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("✅ LLM analysis complete. Intent: {Intent}, Confidence: {Confidence}",
                    analysis.QueryIntent, analysis.ConfidenceScore);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to analyze query with LLM");

                // Fallback to basic analysis if LLM fails
                return CreateFallbackAnalysis(naturalLanguageQuery);
            }
        }

        public async Task<string> GenerateFinalAnswerAsync(string originalQuery, SqlQueryResult queryResult)
        {
            _logger.LogInformation("🎯 Generating final answer for: {Query}", originalQuery);

            var systemPrompt = @"You are a data analyst who provides clear, business-focused answers to database queries.

Your task is to take the technical query execution results and provide a natural language answer that directly addresses the user's original question.

Guidelines:
1. Start with a direct answer to the question
2. Highlight key findings and metrics
3. Provide business context and insights
4. Use clear, non-technical language
5. Include specific numbers and data points
6. End with actionable recommendations if applicable

Format your response as:
- Direct answer (1-2 sentences)
- Key findings (bullet points)
- Business insights (if applicable)
- Recommendations (if applicable)

Keep it concise but comprehensive.";

            var executionSummary = CreateExecutionSummary(queryResult);
            var dataSnapshot = CreateDataSnapshot(queryResult.Data);
            var insightsSummary = queryResult.Insights != null ?
                JsonSerializer.Serialize(queryResult.Insights, new JsonSerializerOptions { WriteIndented = true }) :
                "No insights generated";

            var userPrompt = $@"Original Question: '{originalQuery}'

Execution Summary:
{executionSummary}

Data Results:
{dataSnapshot}

Generated Insights:
{insightsSummary}

Please provide a clear, business-focused answer to the original question.";

            try
            {
                var answer = await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
                _logger.LogInformation("✅ Final answer generated successfully");
                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate final answer with LLM");
                return CreateFallbackAnswer(originalQuery, queryResult);
            }
        }

        public async Task<string> GenerateStepReasoningAsync(string stepDescription, object context)
        {
            var systemPrompt = @"You are a database analyst explaining your reasoning for each step in the analysis process.

Provide a clear, concise explanation of why this step is necessary and what we expect to learn from it.
Keep it to 1-2 sentences and focus on the business value of the step.";

            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
            var userPrompt = $"Step: {stepDescription}\nContext: {contextJson}\n\nExplain why this step is important:";

            try
            {
                return await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate step reasoning, using fallback");
                return $"Executing {stepDescription} to gather necessary data for analysis.";
            }
        }

        private LlmQueryAnalysis CreateFallbackAnalysis(string query)
        {
            var analysis = new LlmQueryAnalysis
            {
                QueryIntent = "General data analysis and retrieval",
                ConfidenceScore = 0.3,
                Reasoning = "Fallback analysis due to LLM processing error",
                QueryType = "Analysis",
                ExtractedEntities = new(),
                Parameters = new(),
                SuggestedSearchTerms = new()
            };

            // Basic keyword extraction
            var words = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var businessTerms = new[] {
                "user", "users", "customer", "customers", "employee", "employees",
                "usage", "activity", "transaction", "transactions", "login", "logins",
                "role", "roles", "permission", "permissions", "access", "session",
                "department", "company", "system", "application", "top", "count",
                "sum", "total", "average", "by"
            };

            foreach (var word in words)
            {
                if (businessTerms.Contains(word))
                {
                    analysis.ExtractedEntities.Add(word);
                    analysis.SuggestedSearchTerms.Add(word);
                }
            }

            // Check for top N queries
            for (int i = 0; i < words.Length - 1; i++)
            {
                if (words[i] == "top" && int.TryParse(words[i + 1], out int limit))
                {
                    analysis.QueryType = "TopN";
                    analysis.Parameters["limit"] = limit;
                    break;
                }
            }

            return analysis;
        }

        private string CreateExecutionSummary(SqlQueryResult result)
        {
            var summary = $"Executed {result.ExecutionSteps.Count} steps in {result.Metadata.TotalExecutionTime.TotalMilliseconds:F0}ms\n";
            summary += $"Analyzed {result.Metadata.TablesAnalyzed.Count} tables: {string.Join(", ", result.Metadata.TablesAnalyzed)}\n";
            summary += $"Retrieved {result.Data.Count} records\n";

            return summary;
        }

        private string CreateDataSnapshot(List<Dictionary<string, object>> data)
        {
            if (!data.Any())
                return "No data returned";

            var snapshot = $"Found {data.Count} records.\n";

            // Show first few records as example
            var sampleSize = Math.Min(3, data.Count);
            snapshot += $"Sample of first {sampleSize} records:\n";

            for (int i = 0; i < sampleSize; i++)
            {
                var record = data[i];
                var recordStr = string.Join(", ", record.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                snapshot += $"  {i + 1}. {recordStr}\n";
            }

            return snapshot;
        }

        private string CreateFallbackAnswer(string originalQuery, SqlQueryResult result)
        {
            var answer = $"Based on your query '{originalQuery}', I found {result.Data.Count} records.\n\n";

            if (result.Data.Any())
            {
                var firstRecord = result.Data.First();
                answer += "Key fields in the results include: " + string.Join(", ", firstRecord.Keys) + "\n\n";
                answer += "The analysis involved exploring " + string.Join(", ", result.Metadata.TablesAnalyzed) + " tables.";
            }

            return answer;
        }
    }
}