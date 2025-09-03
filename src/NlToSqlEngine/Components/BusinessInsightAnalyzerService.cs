using Microsoft.Extensions.Logging;
using NlToSqlEngine.Components;
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
    /// LLM-powered business insight analyzer that provides meaningful business context
    /// This replicates Claude's ability to generate actionable business insights from data
    /// </summary>
    public class BusinessInsightAnalyzerService : IBusinessInsightAnalyzer
    {
        private readonly ILlmService _llmService;
        private readonly ILogger<BusinessInsightAnalyzerService> _logger;

        public BusinessInsightAnalyzerService(
            ILlmService llmService,
            ILogger<BusinessInsightAnalyzerService> logger)
        {
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<BusinessInsights> GenerateInsightsAsync(
            string originalQuery,
            QueryExecutionResult executionResult,
            SchemaContext schemaContext)
        {
            _logger.LogInformation("💡 Generating business insights for query: {Query}", originalQuery);

            try
            {
                // Generate LLM-powered insights
                var llmInsights = await GenerateLlmInsightsAsync(originalQuery, executionResult, schemaContext);

                // Calculate statistical metrics
                var metrics = CalculateKeyMetrics(executionResult.FinalData, originalQuery);

                // Identify data patterns
                var patterns = IdentifyDataPatterns(executionResult.FinalData, executionResult.Steps);

                // Generate observations from execution process
                var observations = GenerateExecutionObservations(executionResult.Steps, schemaContext);

                var insights = new BusinessInsights
                {
                    Summary = llmInsights.Summary,
                    KeyMetrics = metrics,
                    Recommendations = llmInsights.Recommendations,
                    Patterns = patterns.Concat(llmInsights.Patterns ?? new List<string>()).ToList(),
                    Observations = observations
                };

                _logger.LogInformation("✅ Generated {MetricCount} metrics, {PatternCount} patterns, {RecommendationCount} recommendations",
                    insights.KeyMetrics.Count, insights.Patterns.Count, insights.Recommendations.Count);

                return insights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate business insights");
                return CreateFallbackInsights(originalQuery, executionResult);
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



        private async Task<LlmBusinessInsights> GenerateLlmInsightsAsync(
            string originalQuery,
            QueryExecutionResult executionResult,
            SchemaContext schemaContext)
        {
            var systemPrompt = @"You are a senior business analyst who provides actionable insights from database analysis results.

Your task is to analyze the query execution results and provide business-focused insights that help decision-makers understand the implications of the data.

Return your analysis as JSON in this format:
{
    ""summary"": ""2-3 sentence executive summary of key findings"",
    ""patterns"": [
        ""Pattern 1: Description of significant data pattern"",
        ""Pattern 2: Another important pattern""
    ],
    ""recommendations"": [
        ""Actionable recommendation 1"",
        ""Actionable recommendation 2"",
        ""Actionable recommendation 3""
    ]
}

Focus on:
1. Business implications of the findings
2. Actionable insights that drive decision-making
3. Risk identification and opportunities
4. Resource allocation recommendations
5. Performance optimization suggestions
6. User behavior insights (if applicable)

Provide specific, actionable recommendations rather than generic advice.
Use business language that executives and managers can understand and act upon.";

            var executionSummary = CreateDetailedExecutionSummary(executionResult);
            var dataAnalysis = CreateDataAnalysis(executionResult.FinalData, originalQuery);
            var contextSummary = CreateContextSummary(schemaContext);

            var userPrompt = $@"Original Business Question: '{originalQuery}'

Execution Summary:
{executionSummary}

Data Analysis:
{dataAnalysis}

Database Context:
{contextSummary}

Please provide business insights and actionable recommendations based on this analysis.";

            try
            {
                var response = await _llmService.GetCompletionAsync(systemPrompt, userPrompt);
                var insights = JsonSerializer.Deserialize<LlmBusinessInsights>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return insights ?? CreateDefaultLlmInsights(originalQuery);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate LLM insights, using fallback");
                return CreateDefaultLlmInsights(originalQuery);
            }
        }

        private List<KeyMetric> CalculateKeyMetrics(List<Dictionary<string, object>> finalData, string originalQuery)
        {
            var metrics = new List<KeyMetric>();

            if (!finalData?.Any() == true)
            {
                return metrics;
            }

            var firstRecord = finalData.First();
            var numericColumns = GetNumericColumns(firstRecord);

            // Total records metric
            metrics.Add(new KeyMetric
            {
                Name = "Total Records",
                Value = finalData.Count,
                Description = "Total number of records returned by the analysis",
                Category = "Volume"
            });

            // Calculate metrics for numeric columns
            foreach (var column in numericColumns)
            {
                var values = finalData
                    .Select(r => ConvertToDouble(r.GetValueOrDefault(column)))
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToList();

                if (values.Any())
                {
                    metrics.Add(new KeyMetric
                    {
                        Name = $"{column} - Total",
                        Value = values.Sum(),
                        Description = $"Sum of all {column} values",
                        Category = "Aggregate"
                    });

                    metrics.Add(new KeyMetric
                    {
                        Name = $"{column} - Average",
                        Value = Math.Round(values.Average(), 2),
                        Description = $"Average {column} value",
                        Category = "Statistical"
                    });

                    if (values.Count > 1)
                    {
                        var sorted = values.OrderBy(v => v).ToList();
                        metrics.Add(new KeyMetric
                        {
                            Name = $"{column} - Median",
                            Value = sorted.Count % 2 == 0
                                ? Math.Round((sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0, 2)
                                : sorted[sorted.Count / 2],
                            Description = $"Median {column} value",
                            Category = "Statistical"
                        });
                    }

                    metrics.Add(new KeyMetric
                    {
                        Name = $"{column} - Range",
                        Value = $"{values.Min()} - {values.Max()}",
                        Description = $"Range of {column} values",
                        Category = "Distribution"
                    });
                }
            }

            // Special metrics for user-related queries
            if (originalQuery.ToLower().Contains("user") && finalData.Any())
            {
                var userIdColumn = firstRecord.Keys.FirstOrDefault(k =>
                    k.ToLower().Contains("user") && k.ToLower().Contains("id"));

                if (userIdColumn != null)
                {
                    var uniqueUsers = finalData.Select(r => r[userIdColumn]).Distinct().Count();
                    metrics.Add(new KeyMetric
                    {
                        Name = "Unique Users",
                        Value = uniqueUsers,
                        Description = "Number of unique users in the result set",
                        Category = "Business"
                    });
                }

                // Department distribution if available
                var deptColumn = firstRecord.Keys.FirstOrDefault(k =>
                    k.ToLower().Contains("department"));

                if (deptColumn != null)
                {
                    var departments = finalData
                        .Select(r => r.GetValueOrDefault(deptColumn)?.ToString())
                        .Where(d => !string.IsNullOrEmpty(d))
                        .GroupBy(d => d)
                        .Count();

                    metrics.Add(new KeyMetric
                    {
                        Name = "Departments Represented",
                        Value = departments,
                        Description = "Number of different departments in the results",
                        Category = "Business"
                    });
                }
            }

            return metrics.Take(10).ToList(); // Limit to top 10 most relevant metrics
        }

        private List<string> IdentifyDataPatterns(
            List<Dictionary<string, object>> finalData,
            List<QueryExecutionStep> steps)
        {
            var patterns = new List<string>();

            if (!finalData?.Any() == true)
            {
                patterns.Add("No data patterns identified - empty result set");
                return patterns;
            }

            // Analyze distribution patterns
            var firstRecord = finalData.First();
            var numericColumns = GetNumericColumns(firstRecord);

            foreach (var column in numericColumns.Take(3)) // Analyze top 3 numeric columns
            {
                var values = finalData
                    .Select(r => ConvertToDouble(r.GetValueOrDefault(column)))
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToList();

                if (values.Any())
                {
                    // Check for concentration patterns
                    var sorted = values.OrderByDescending(v => v).ToList();
                    if (sorted.Count > 1)
                    {
                        var top20Percent = sorted.Take(Math.Max(1, sorted.Count / 5)).Sum();
                        var total = sorted.Sum();

                        if (total > 0)
                        {
                            var concentration = (top20Percent / total) * 100;
                            if (concentration > 80)
                            {
                                patterns.Add($"High concentration: Top 20% of {column} values account for {concentration:F1}% of total");
                            }
                        }
                    }

                    // Check for zero/null patterns
                    var zeroCount = values.Count(v => v == 0);
                    if (zeroCount > values.Count * 0.5)
                    {
                        patterns.Add($"Data sparsity: {zeroCount}/{values.Count} records have zero {column} values ({(zeroCount * 100.0 / values.Count):F1}%)");
                    }
                }
            }

            // Analyze execution patterns
            var explorationSteps = steps.Where(s => s.StepType == "DataExploration").ToList();
            if (explorationSteps.Count > 2)
            {
                patterns.Add($"Comprehensive analysis: {explorationSteps.Count} exploration steps performed for thorough data understanding");
            }

            // Check for relationship patterns
            var joinSteps = steps.Where(s => s.SqlQuery?.ToLower().Contains("join") == true).ToList();
            if (joinSteps.Any())
            {
                patterns.Add($"Complex data relationships: {joinSteps.Count} table joins performed to correlate information");
            }

            return patterns;
        }

        private List<string> GenerateExecutionObservations(
            List<QueryExecutionStep> steps,
            SchemaContext schemaContext)
        {
            var observations = new List<string>();

            // Execution efficiency observations
            var totalTime = steps.Sum(s => s.ExecutionTime.TotalMilliseconds);
            observations.Add($"Query execution completed in {totalTime:F0}ms across {steps.Count} steps");

            // Data quality observations
            var recordCounts = steps.Where(s => s.Results?.Any() == true)
                .Select(s => s.Results.Count)
                .ToList();

            if (recordCounts.Any())
            {
                observations.Add($"Data retrieval varied from {recordCounts.Min()} to {recordCounts.Max()} records per step");
            }

            // Schema observations
            if (schemaContext.RelevantTables?.Any() == true)
            {
                var tablesWithData = schemaContext.RelevantTables.Count(t => t.Stats?.RowCount > 0);
                var totalRows = schemaContext.RelevantTables.Sum(t => t.Stats?.RowCount ?? 0);

                observations.Add($"Analyzed {schemaContext.RelevantTables.Count} tables with {totalRows:N0} total records");

                if (tablesWithData < schemaContext.RelevantTables.Count)
                {
                    observations.Add($"Data availability: {tablesWithData}/{schemaContext.RelevantTables.Count} analyzed tables contain data");
                }
            }

            // Confidence observations
            observations.Add($"Schema discovery confidence: {schemaContext.ConfidenceScore:P1}");

            return observations;
        }

        private string CreateDetailedExecutionSummary(QueryExecutionResult executionResult)
        {
            var summary = $"Executed {executionResult.Steps.Count} steps successfully.\n";
            summary += $"Final result: {executionResult.FinalData.Count} records.\n";

            var stepTypes = executionResult.Steps.GroupBy(s => s.StepType)
                .Select(g => $"{g.Key}: {g.Count()}")
                .ToList();

            summary += $"Step breakdown: {string.Join(", ", stepTypes)}\n";

            return summary;
        }

        private string CreateDataAnalysis(List<Dictionary<string, object>> finalData, string originalQuery)
        {
            if (!finalData?.Any() == true)
            {
                return "No data available for analysis.";
            }

            var analysis = $"Retrieved {finalData.Count} records.\n";
            var firstRecord = finalData.First();

            analysis += $"Data structure: {firstRecord.Keys.Count} columns.\n";
            analysis += $"Key fields: {string.Join(", ", firstRecord.Keys.Take(5))}\n";

            // Sample data for context
            if (finalData.Count > 0)
            {
                analysis += $"Sample record: {string.Join(", ", firstRecord.Take(3).Select(kvp => $"{kvp.Key}={kvp.Value}"))}\n";
            }

            return analysis;
        }

        private string CreateContextSummary(SchemaContext schemaContext)
        {
            var summary = $"Analyzed {schemaContext.RelevantTables?.Count ?? 0} relevant tables.\n";
            summary += $"Search terms used: {string.Join(", ", schemaContext.SearchTerms ?? new List<string>())}\n";
            summary += $"Discovery confidence: {schemaContext.ConfidenceScore:P1}\n";

            return summary;
        }

        private List<string> GetNumericColumns(Dictionary<string, object> record)
        {
            return record
                .Where(kvp => IsNumericValue(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private bool IsNumericValue(object value)
        {
            return value != null && (
                value is int || value is long || value is float || value is double || value is decimal ||
                (value is string str && double.TryParse(str, out _))
            );
        }

        private double? ConvertToDouble(object value)
        {
            if (value == null) return null;

            if (value is double d) return d;
            if (value is float f) return f;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is decimal dec) return (double)dec;
            if (value is string str && double.TryParse(str, out double result)) return result;

            return null;
        }

        private LlmBusinessInsights CreateDefaultLlmInsights(string originalQuery)
        {
            return new LlmBusinessInsights
            {
                Summary = $"Analysis completed for query: {originalQuery}. Review the detailed metrics and patterns for specific insights.",
                Patterns = new List<string> { "Standard data analysis patterns identified" },
                Recommendations = new List<string>
                {
                    "Review the detailed metrics for actionable insights",
                    "Consider deeper analysis of high-value data segments",
                    "Monitor key performance indicators regularly"
                }
            };
        }

        private BusinessInsights CreateFallbackInsights(string originalQuery, QueryExecutionResult executionResult)
        {
            return new BusinessInsights
            {
                Summary = $"Basic analysis completed for: {originalQuery}",
                KeyMetrics = CalculateKeyMetrics(executionResult.FinalData, originalQuery),
                Patterns = new List<string> { "Standard data patterns observed" },
                Recommendations = new List<string> { "Review detailed data for specific insights" },
                Observations = new List<string> { $"Processed {executionResult.Steps.Count} analysis steps" }
            };
        }

        // Helper class for LLM response deserialization
        private class LlmBusinessInsights
        {
            public string Summary { get; set; }
            public List<string> Patterns { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }
    }
}