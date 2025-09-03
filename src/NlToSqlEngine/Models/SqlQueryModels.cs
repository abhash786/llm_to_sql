using System;
using System.Collections.Generic;

namespace NlToSqlEngine.Models
{
    public class SqlQueryResult
    {
        public string OriginalQuery { get; set; }
        public List<QueryExecutionStep> ExecutionSteps { get; set; } = new();
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public BusinessInsights Insights { get; set; }
        public ExecutionMetadata Metadata { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string FinalAnswer { get; set; }
    }

    public class QueryExecutionStep
    {
        public int StepNumber { get; set; }
        public string StepType { get; set; } // SchemaDiscovery, DataExploration, QueryExecution, etc.
        public string Description { get; set; }
        public string SqlQuery { get; set; }
        public List<Dictionary<string, object>> Results { get; set; }
        public string Reasoning { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string Purpose { get; set; }
    }

    public class BusinessInsights
    {
        public string Summary { get; set; }
        public List<KeyMetric> KeyMetrics { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> Patterns { get; set; } = new();
        public List<string> Observations { get; set; } = new();
    }

    public class KeyMetric
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }

    public class ExecutionMetadata
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalSteps { get; set; }
        public List<string> TablesAnalyzed { get; set; } = new();
        public List<string> SchemasExplored { get; set; } = new();
        public List<string> ToolsUsed { get; set; } = new();
    }

    public class SchemaContext
    {
        public List<TableInfo> RelevantTables { get; set; } = new();
        public List<string> SearchTerms { get; set; } = new();
        public string QueryIntent { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> SchemasExplored { get; set; } = new();
        public double ConfidenceScore { get; set; }
    }

    public class TableInfo
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string FullTableName => $"{SchemaName}.{TableName}";
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
        public TableStats Stats { get; set; }
        public double RelevanceScore { get; set; }
        public List<Dictionary<string, object>> SampleData { get; set; } = new();
    }

    public class ColumnInfo
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public int? MaxLength { get; set; }
    }

    public class ForeignKeyInfo
    {
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }

    public class TableStats
    {
        public long RowCount { get; set; }
        public long TotalKB { get; set; }
        public long UsedKB { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class QueryPlan
    {
        public string QueryIntent { get; set; }
        public List<QueryPlanStep> Steps { get; set; } = new();
        public SchemaContext Context { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Strategy { get; set; }
        public double ConfidenceScore { get; set; }
    }

    public class QueryPlanStep
    {
        public int StepNumber { get; set; }
        public string StepType { get; set; }
        public string Description { get; set; }
        public string SqlTemplate { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Purpose { get; set; }
        public string Reasoning { get; set; }
        public List<string> ExpectedColumns { get; set; } = new();
        public bool IsOptional { get; set; }
    }

    // LLM Integration Models
    public class LlmQueryAnalysis
    {
        public string QueryIntent { get; set; }
        public List<string> ExtractedEntities { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string QueryType { get; set; }
        public double ConfidenceScore { get; set; }
        public string Reasoning { get; set; }
        public List<string> SuggestedSearchTerms { get; set; } = new();
    }

    public class LlmQueryPlan
    {
        public string Strategy { get; set; }
        public List<LlmQueryPlanStep> Steps { get; set; } = new();
        public string Reasoning { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public double ConfidenceScore { get; set; }
    }

    public class LlmQueryPlanStep
    {
        public int Order { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Reasoning { get; set; }
        public string ExpectedOutcome { get; set; }
    }
}