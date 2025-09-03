namespace NlToSqlEngine.Configuration
{
    public class LlmConfiguration
    {
        public string Provider { get; set; } = "OpenAI"; // OpenAI, AzureOpenAI, Anthropic
        public string ApiKey { get; set; }
        public string Endpoint { get; set; } // For Azure OpenAI
        public string ModelName { get; set; } = "gpt-4";
        public int MaxTokens { get; set; } = 2000;
        public double Temperature { get; set; } = 0.1; // Low temperature for consistent, factual responses
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
    }

    public class DatabaseConfiguration
    {
        public string ConnectionString { get; set; }
        public string DatabaseType { get; set; } = "SqlServer"; // SqlServer, PostgreSQL, MySQL, etc.
        public int QueryTimeoutSeconds { get; set; } = 30;
        public int MaxRowsPerQuery { get; set; } = 1000;
        public bool EnableQueryLogging { get; set; } = true;
    }

    public class EngineConfiguration
    {
        public int MaxExecutionSteps { get; set; } = 20;
        public int MaxTablesPerAnalysis { get; set; } = 10;
        public bool EnableProgressiveAnalysis { get; set; } = true;
        public bool EnableBusinessInsights { get; set; } = true;
        public double MinConfidenceThreshold { get; set; } = 0.6;
    }
}