using Microsoft.Extensions.Logging;
using NlToSqlEngine.MCP;
using NlToSqlEngine.Models;
using NlToSqlEngine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NlToSqlEngine.Components
{
    /// <summary>
    /// Implementation of schema discovery that exactly replicates Claude's database exploration methodology
    /// This follows the same step-by-step approach I demonstrated when analyzing your database
    /// </summary>
    public class SchemaDiscoveryService : ISchemaDiscovery
    {
        private readonly IMcpToolsService _mcpTools;
        private readonly ILlmService _llmService;
        private readonly ILogger<SchemaDiscoveryService> _logger;

        public SchemaDiscoveryService(
            IMcpToolsService mcpTools,
            ILlmService llmService,
            ILogger<SchemaDiscoveryService> logger)
        {
            _mcpTools = mcpTools;
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<SchemaContext> DiscoverRelevantSchemaAsync(LlmQueryAnalysis llmAnalysis)
        {
            _logger.LogInformation("🗂️ Starting schema discovery for query intent: {Intent}", llmAnalysis.QueryIntent);

            var context = new SchemaContext
            {
                QueryIntent = llmAnalysis.QueryIntent,
                SearchTerms = llmAnalysis.SuggestedSearchTerms,
                Parameters = llmAnalysis.Parameters
            };

            // Phase 1: Initial Database Reconnaissance (like Claude's first steps)
            await PerformDatabaseReconnaissanceAsync(context);

            // Phase 2: Search-Based Table Discovery (using LLM-suggested terms)
            await DiscoverRelevantTablesAsync(context, llmAnalysis);

            // Phase 3: Table Structure Analysis (detailed examination)
            await AnalyzeTableStructuresAsync(context);

            // Phase 4: Relationship Discovery (foreign keys and connections)
            await DiscoverRelationshipsAsync(context);

            // Phase 5: Data Sampling and Quality Assessment
            await SampleAndAssessDataAsync(context);

            // Phase 6: Calculate Final Relevance Scores
            CalculateFinalRelevanceScores(context);

            _logger.LogInformation("✅ Schema discovery complete. Found {TableCount} relevant tables",
                context.RelevantTables.Count);

            return context;
        }

        private async Task PerformDatabaseReconnaissanceAsync(SchemaContext context)
        {
            _logger.LogInformation("🔍 Phase 1: Database reconnaissance...");

            var reasoning = await _llmService.GetCompletionAsync(
                "You are a database analyst explaining your reasoning for each step. Provide a clear, concise explanation of why this step is necessary. Keep it to 1-2 sentences.",
                $"Step: Initial database reconnaissance for query '{context.QueryIntent}'. Explain why this step is important:");

            _logger.LogInformation("💭 {Reasoning}", reasoning);

            // Step 1.1: List all schemas
            var schemas = await _mcpTools.ListSchemasAsync();
            context.SchemasExplored = schemas.Select(s => s.SchemaName).ToList();

            _logger.LogInformation("📋 Found {SchemaCount} schemas: {Schemas}",
                schemas.Count, string.Join(", ", context.SchemasExplored));

            // Step 1.2: Get comprehensive table list
            var allTables = await _mcpTools.ListTablesAsync();

            _logger.LogInformation("📊 Found {TableCount} total tables in database", allTables.Count);
        }

        private async Task DiscoverRelevantTablesAsync(SchemaContext context, LlmQueryAnalysis llmAnalysis)
        {
            _logger.LogInformation("🔍 Phase 2: Discovering relevant tables using search terms...");

            var candidateTables = new Dictionary<string, TableInfo>();

            // Step 2.1: Search using LLM-suggested terms
            foreach (var searchTerm in llmAnalysis.SuggestedSearchTerms)
            {
                var reasoning = await _llmService.GetCompletionAsync(
                    "You are a database analyst explaining search reasoning. Provide a 1-sentence explanation.",
                    $"Searching schema for term '{searchTerm}' in context of '{context.QueryIntent}'. Why is this search important?");

                _logger.LogInformation("🔍 Searching for: '{SearchTerm}' - {Reasoning}", searchTerm, reasoning);

                var searchResults = await _mcpTools.SearchSchemaAsync(searchTerm);

                _logger.LogInformation("📍 Found {ResultCount} matches for '{SearchTerm}'",
                    searchResults.Count, searchTerm);

                foreach (var result in searchResults)
                {
                    var tableKey = $"{result.SchemaName}.{result.TableName}";

                    if (!candidateTables.ContainsKey(tableKey))
                    {
                        candidateTables[tableKey] = new TableInfo
                        {
                            SchemaName = result.SchemaName,
                            TableName = result.TableName,
                            RelevanceScore = 0
                        };
                    }

                    // Calculate relevance score based on match type and term importance
                    var score = CalculateSearchRelevanceScore(searchTerm, result, llmAnalysis);
                    candidateTables[tableKey].RelevanceScore += score;

                    _logger.LogDebug("📊 Table {TableName} scored {Score} for term '{SearchTerm}'",
                        tableKey, score, searchTerm);
                }
            }

            // Step 2.2: If no results, fall back to pattern matching
            if (!candidateTables.Any())
            {
                _logger.LogWarning("⚠️ No schema search results found, falling back to pattern matching");
                await FallbackPatternMatchingAsync(candidateTables, llmAnalysis);
            }

            context.RelevantTables = candidateTables.Values
                .OrderByDescending(t => t.RelevanceScore)
                .Take(10) // Limit to top 10 candidates for detailed analysis
                .ToList();

            _logger.LogInformation("🎯 Selected {Count} candidate tables for detailed analysis",
                context.RelevantTables.Count);
        }

        private async Task AnalyzeTableStructuresAsync(SchemaContext context)
        {
            _logger.LogInformation("🔍 Phase 3: Analyzing table structures...");

            foreach (var table in context.RelevantTables)
            {
                var reasoning = await _llmService.GetCompletionAsync(
                    "You are a database analyst. Provide a 1-sentence explanation.",
                    $"Analyzing structure of table '{table.FullTableName}' with relevance score {table.RelevanceScore}. Why analyze this table?");

                _logger.LogInformation("🔬 Analyzing {TableName} - {Reasoning}",
                    table.FullTableName, reasoning);

                // Get column descriptions
                var columns = await _mcpTools.DescribeTableAsync(table.FullTableName);
                table.Columns = columns.Select(c => new ColumnInfo
                {
                    ColumnName = c.ColumnName,
                    DataType = c.DataType,
                    IsNullable = c.IsNullable,
                    MaxLength = c.CharacterMaximumLength,
                    IsPrimaryKey = IsPrimaryKeyCandidate(c.ColumnName),
                    IsForeignKey = IsForeignKeyCandidate(c.ColumnName)
                }).ToList();

                // Get table statistics
                var stats = await _mcpTools.GetTableStatsAsync(table.FullTableName);
                table.Stats = new TableStats
                {
                    RowCount = stats.RowCount,
                    TotalKB = stats.TotalKB,
                    UsedKB = stats.UsedKB
                };

                _logger.LogInformation("📊 {TableName}: {RowCount} rows, {ColumnCount} columns, {SizeKB} KB",
                    table.FullTableName, table.Stats.RowCount, table.Columns.Count, table.Stats.TotalKB);
            }
        }

        private async Task DiscoverRelationshipsAsync(SchemaContext context)
        {
            _logger.LogInformation("🔍 Phase 4: Discovering table relationships...");

            foreach (var table in context.RelevantTables)
            {
                var reasoning = await _llmService.GetCompletionAsync(
                    "You are a database analyst. Provide a 1-sentence explanation.",
                    $"Discovering relationships for table '{table.FullTableName}'. Why is this important?");

                _logger.LogInformation("🔗 Finding relationships for {TableName} - {Reasoning}",
                    table.FullTableName, reasoning);

                var foreignKeys = await _mcpTools.GetForeignKeysAsync(table.FullTableName);
                table.ForeignKeys = foreignKeys.Select(fk => new ForeignKeyInfo
                {
                    ColumnName = fk.ColumnName,
                    ReferencedTable = fk.ReferencedTable,
                    ReferencedColumn = fk.ReferencedColumn
                }).ToList();

                if (table.ForeignKeys.Any())
                {
                    _logger.LogInformation("🔗 Found {FKCount} foreign key relationships in {TableName}",
                        table.ForeignKeys.Count, table.FullTableName);

                    // Boost relevance score for tables with relationships to other relevant tables
                    foreach (var fk in table.ForeignKeys)
                    {
                        if (context.RelevantTables.Any(t => t.TableName == fk.ReferencedTable))
                        {
                            table.RelevanceScore += 2; // Relationship bonus
                        }
                    }
                }
            }
        }

        private async Task SampleAndAssessDataAsync(SchemaContext context)
        {
            _logger.LogInformation("🔍 Phase 5: Sampling data for quality assessment...");

            foreach (var table in context.RelevantTables.Take(5)) // Sample top 5 tables only
            {
                try
                {
                    var reasoning = await _llmService.GetCompletionAsync(
                        "You are a database analyst. Provide a 1-sentence explanation.",
                        $"Sampling data from table '{table.FullTableName}' with {table.Stats.RowCount} rows. Why sample this data?");

                    _logger.LogInformation("🎲 Sampling {TableName} - {Reasoning}",
                        table.FullTableName, reasoning);

                    var sampleRows = await _mcpTools.SampleRowsAsync(table.FullTableName, 5);
                    table.SampleData = sampleRows;

                    if (sampleRows.Any())
                    {
                        var sampleRecord = sampleRows.First();
                        _logger.LogInformation("📋 Sample from {TableName}: {SampleKeys}",
                            table.FullTableName, string.Join(", ", sampleRecord.Keys));
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No sample data available for {TableName}", table.FullTableName);
                        table.RelevanceScore -= 1; // Reduce score for empty tables
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to sample data from {TableName}", table.FullTableName);
                }
            }
        }

        private void CalculateFinalRelevanceScores(SchemaContext context)
        {
            _logger.LogInformation("🎯 Calculating final relevance scores...");

            foreach (var table in context.RelevantTables)
            {
                var originalScore = table.RelevanceScore;

                // Bonus for tables with data
                if (table.Stats.RowCount > 0)
                    table.RelevanceScore += 2;

                // Bonus for tables with usage-related columns
                if (table.Columns.Any(c => IsUsageRelatedColumn(c.ColumnName)))
                    table.RelevanceScore += 3;

                // Bonus for user-related tables
                if (table.Columns.Any(c => IsUserRelatedColumn(c.ColumnName)))
                    table.RelevanceScore += 2;

                // Penalty for very small tables (might be lookup tables)
                if (table.Stats.RowCount < 10 && table.Stats.RowCount > 0)
                    table.RelevanceScore -= 1;

                _logger.LogDebug("🎯 {TableName}: {OriginalScore} → {FinalScore}",
                    table.FullTableName, originalScore, table.RelevanceScore);
            }

            // Re-sort by final relevance scores
            context.RelevantTables = context.RelevantTables
                .OrderByDescending(t => t.RelevanceScore)
                .ToList();

            context.ConfidenceScore = context.RelevantTables.Any() ?
                Math.Min(context.RelevantTables.Max(t => t.RelevanceScore) / 10.0, 1.0) : 0.0;
        }

        private double CalculateSearchRelevanceScore(string searchTerm, SchemaSearchResult result, LlmQueryAnalysis analysis)
        {
            double score = 0;

            // Exact table name match gets highest score
            if (result.TableName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                score += 10;
            else if (result.TableName.ToLower().Contains(searchTerm.ToLower()))
                score += 7;

            // Column name matches get medium score
            if (result.ColumnName?.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) == true)
                score += 5;
            else if (result.ColumnName?.ToLower().Contains(searchTerm.ToLower()) == true)
                score += 3;

            // Bonus for key business terms
            if (IsHighValueBusinessTerm(searchTerm))
                score += 2;

            // Bonus based on query type
            if (analysis.QueryType == "TopN" && (searchTerm.Contains("usage") || searchTerm.Contains("activity")))
                score += 3;

            return score;
        }

        private async Task FallbackPatternMatchingAsync(Dictionary<string, TableInfo> candidateTables, LlmQueryAnalysis analysis)
        {
            var allTables = await _mcpTools.ListTablesAsync();

            foreach (var table in allTables)
            {
                foreach (var entity in analysis.ExtractedEntities)
                {
                    if (table.TableName.ToLower().Contains(entity.ToLower()))
                    {
                        var tableKey = $"{table.SchemaName}.{table.TableName}";
                        if (!candidateTables.ContainsKey(tableKey))
                        {
                            candidateTables[tableKey] = new TableInfo
                            {
                                SchemaName = table.SchemaName,
                                TableName = table.TableName,
                                RelevanceScore = 3 // Lower score for pattern matching
                            };
                        }
                    }
                }
            }
        }

        private bool IsPrimaryKeyCandidate(string columnName)
        {
            return columnName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                   columnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsForeignKeyCandidate(string columnName)
        {
            return columnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) &&
                   !columnName.Equals("Id", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsUsageRelatedColumn(string columnName)
        {
            var usageTerms = new[] { "usage", "used", "activity", "action", "transaction", "login", "session", "lastuse", "count" };
            return usageTerms.Any(term => columnName.ToLower().Contains(term));
        }

        private bool IsUserRelatedColumn(string columnName)
        {
            var userTerms = new[] { "user", "employee", "person", "customer", "account", "name" };
            return userTerms.Any(term => columnName.ToLower().Contains(term));
        }

        private bool IsHighValueBusinessTerm(string term)
        {
            var highValueTerms = new[] { "user", "usage", "activity", "transaction", "customer", "employee", "role", "permission" };
            return highValueTerms.Contains(term.ToLower());
        }
    }
}