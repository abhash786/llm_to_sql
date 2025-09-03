using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NlToSqlEngine.Components;
using NlToSqlEngine.Configuration;
using NlToSqlEngine.Core;
using NlToSqlEngine.MCP;
using NlToSqlEngine.Services;
using NlToSqlEngine.Services.OpenAI;
using NlToSqlEngine.Services.AzureOpenAI;
using NlToSqlEngine.Services.Anthropic;

namespace NlToSqlEngine.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring the NL to SQL Engine services in DI container
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all NL to SQL Engine services to the DI container
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration instance</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddNlToSqlEngine(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Configuration
            services.Configure<LlmConfiguration>(configuration.GetSection("LlmConfiguration"));
            services.Configure<DatabaseConfiguration>(configuration.GetSection("DatabaseConfiguration"));
            services.Configure<EngineConfiguration>(configuration.GetSection("EngineConfiguration"));

            // Core engine services
            services.AddScoped<INlToSqlEngine, NlToSqlEngineService>();

            // LLM services - register all providers
            services.AddHttpClient<Services.OpenAI.OpenAILlmService>();
            services.AddHttpClient<Services.AzureOpenAI.AzureOpenAILlmService>();
            services.AddHttpClient<Services.Anthropic.AnthropicLlmService>();
            
            // LLM factory and main service
            services.AddScoped<ILlmServiceFactory, LlmServiceFactory>();
            services.AddScoped<ILlmService>(provider => provider.GetRequiredService<ILlmServiceFactory>().CreateLlmService());
            services.AddScoped<ILlmQueryAnalyzer, LlmQueryAnalyzerService>();

            // MCP Tools (database access layer)
            services.AddScoped<IMcpToolsService, McpToolsService>();

            // Component services
            services.AddScoped<ISchemaDiscovery, SchemaDiscoveryService>();
            services.AddScoped<IQueryPlanner, QueryPlannerService>();
            services.AddScoped<IQueryExecutor, QueryExecutorService>();
            services.AddScoped<IBusinessInsightAnalyzer, BusinessInsightAnalyzerService>();

            return services;
        }

        /// <summary>
        /// Adds NL to SQL Engine services with custom LLM configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="llmConfig">Custom LLM configuration</param>
        /// <param name="dbConnectionString">Database connection string</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddNlToSqlEngine(
            this IServiceCollection services,
            LlmConfiguration llmConfig,
            string dbConnectionString)
        {
            // Configuration
            services.Configure<LlmConfiguration>(options =>
            {
                options.Provider = llmConfig.Provider;
                options.ApiKey = llmConfig.ApiKey;
                options.Endpoint = llmConfig.Endpoint;
                options.ModelName = llmConfig.ModelName;
                options.MaxTokens = llmConfig.MaxTokens;
                options.Temperature = llmConfig.Temperature;
                options.TimeoutSeconds = llmConfig.TimeoutSeconds;
                options.MaxRetries = llmConfig.MaxRetries;
            });

            services.Configure<DatabaseConfiguration>(options =>
            {
                options.ConnectionString = dbConnectionString;
                options.DatabaseType = "SqlServer";
                options.QueryTimeoutSeconds = 30;
                options.MaxRowsPerQuery = 1000;
                options.EnableQueryLogging = true;
            });

            services.Configure<EngineConfiguration>(options =>
            {
                options.MaxExecutionSteps = 20;
                options.MaxTablesPerAnalysis = 10;
                options.EnableProgressiveAnalysis = true;
                options.EnableBusinessInsights = true;
                options.MinConfidenceThreshold = 0.6;
            });

            // Core engine services
            services.AddScoped<INlToSqlEngine, NlToSqlEngineService>();

            // LLM services - register all providers
            services.AddHttpClient<Services.OpenAI.OpenAILlmService>();
            services.AddHttpClient<Services.AzureOpenAI.AzureOpenAILlmService>();
            services.AddHttpClient<Services.Anthropic.AnthropicLlmService>();
            
            // LLM factory and main service
            services.AddScoped<ILlmServiceFactory, LlmServiceFactory>();
            services.AddScoped<ILlmService>(provider => provider.GetRequiredService<ILlmServiceFactory>().CreateLlmService());
            services.AddScoped<ILlmQueryAnalyzer, LlmQueryAnalyzerService>();

            // MCP Tools (database access layer)
            services.AddScoped<IMcpToolsService, McpToolsService>();

            // Component services
            services.AddScoped<ISchemaDiscovery, SchemaDiscoveryService>();
            services.AddScoped<IQueryPlanner, QueryPlannerService>();
            services.AddScoped<IQueryExecutor, QueryExecutorService>();
            services.AddScoped<IBusinessInsightAnalyzer, BusinessInsightAnalyzerService>();

            return services;
        }
    }
}