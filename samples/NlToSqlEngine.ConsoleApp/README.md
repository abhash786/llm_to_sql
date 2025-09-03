# NL to SQL Engine - Console Application Demo

This console application demonstrates the Natural Language to SQL Engine that replicates Claude's database analysis methodology.

## 🚀 Quick Start

### 1. Setup Configuration

#### Option A: Update appsettings.json
Edit `appsettings.json` with your configuration:

```json
{
  "LlmConfiguration": {
    "Provider": "Anthropic",
    "ApiKey": "your-claude-api-key-here",
    "ModelName": "claude-3-5-sonnet-20241022"
  },
  "DatabaseConfiguration": {
    "ConnectionString": "Server=your-server;Database=your-database;Integrated Security=true;"
  }
}
```

#### Option B: Use User Secrets (Recommended)
Store sensitive information like API keys securely:

```bash
cd samples/NlToSqlEngine.ConsoleApp
dotnet user-secrets set "LlmConfiguration:ApiKey" "your-claude-api-key"
dotnet user-secrets set "DatabaseConfiguration:ConnectionString" "your-connection-string"
```

#### Option C: Environment Variables
Set environment variables:

```bash
export LlmConfiguration__ApiKey="your-claude-api-key"
export DatabaseConfiguration__ConnectionString="your-connection-string"
```

### 2. Run the Application

```bash
cd samples/NlToSqlEngine.ConsoleApp
dotnet run
```

## 🎯 Demo Queries

The console app comes with pre-built demo queries that match the original test:

1. **"Top 20 Users by Usage"** - Replicates the exact analysis Claude performed
2. **"Show me users with the highest activity in the last month"**
3. **"Which departments have the most active users?"**
4. **"List all users who have never logged in"**
5. **"Find users with administrative roles"**
6. **Custom query** - Enter your own natural language question

## 🔧 Configuration Options

### LLM Configuration
- **Provider**: `"Anthropic"` (Claude - default), `"OpenAI"`, or `"AzureOpenAI"`
- **ApiKey**: Your API key
- **ModelName**: 
  - Claude: `"claude-3-5-sonnet-20241022"`, `"claude-3-opus-20240229"`, `"claude-3-haiku-20240307"`
  - OpenAI: `"gpt-4"`, `"gpt-3.5-turbo"`
  - Azure: Your deployment name
- **Temperature**: 0.1 (low for consistent, factual responses)

**Why Claude is Default?** This engine replicates Claude's methodology, so using Claude itself provides the most authentic experience!

### Database Configuration
- **ConnectionString**: Your SQL Server connection string
- **QueryTimeoutSeconds**: Maximum time for queries (default: 30)
- **MaxRowsPerQuery**: Maximum rows per query (default: 1000)

### Engine Configuration
- **MaxExecutionSteps**: Maximum analysis steps (default: 20)
- **MaxTablesPerAnalysis**: Maximum tables to analyze (default: 10)
- **EnableProgressiveAnalysis**: Use step-by-step approach (default: true)
- **EnableBusinessInsights**: Generate business insights (default: true)

## 🔍 What You'll See

The console application shows the complete analysis process, just like Claude's approach:

### 1. Execution Steps
```
📋 EXECUTION STEPS:
Step 1: Schema discovery and exploration
  🎯 Purpose: Understand database structure
  💭 Reasoning: Need to identify relevant tables for usage analysis
  ⏱️ Duration: 245ms
  📊 Results: 15 tables found
```

### 2. Final Answer
```
💬 FINAL ANSWER:
Based on the analysis, I found the top 20 users by usage. The highest usage 
comes from Rashmi Kulkarni with 5 used actions across 3 roles, followed by 
Ruchi S Kulkarni Kshire and SHWETALI, each with 3 used actions...
```

### 3. Key Metrics
```
📊 KEY METRICS:
• Total Records: 20
• TotalUsedActions - Total: 15
• TotalUsedActions - Average: 0.75
• Unique Users: 20
• Departments Represented: 5
```

### 4. Sample Data
```
📄 SAMPLE DATA (5 of 20 records):
UserId          | UserName        | TotalUsedActions | Department
10031347        | RK_30JAN24      | 5               | 
10031417        | RKDEC08         | 3               | QA IGA
```

### 5. Business Insights
```
🔍 PATTERNS IDENTIFIED:
• High concentration: Top 20% of usage values account for 87.3% of total
• Data sparsity: 17/20 records have zero usage values (85.0%)

💡 RECOMMENDATIONS:
• Focus on user engagement strategies for inactive users
• Investigate why top users have significantly higher usage
• Consider role-based training programs
```

## 🛠️ Troubleshooting

### Common Issues

1. **"LLM API error"**
   - Check your API key is correct
   - Verify you have sufficient API credits
   - Ensure the model name is valid

2. **"Database connection failed"**
   - Verify your connection string
   - Check database server is accessible
   - Ensure proper authentication

3. **"No relevant tables found"**
   - The engine searches for tables based on your query
   - Try more specific queries like "user usage" or "activity logs"
   - Check your database has tables with relevant names/columns

### Logging

Enable detailed logging to see what's happening:

```json
{
  "Logging": {
    "LogLevel": {
      "NlToSqlEngine": "Debug"
    }
  }
}
```

## 🏗️ Architecture

The console app demonstrates the complete engine pipeline:

1. **LLM Query Analysis** - Understands your natural language question
2. **Schema Discovery** - Finds relevant database tables and columns  
3. **Query Planning** - Creates step-by-step execution plan
4. **Progressive Execution** - Runs queries building understanding incrementally
5. **Business Insights** - Generates actionable recommendations
6. **Natural Language Answer** - Provides human-readable response

This exactly replicates how Claude analyzed your database to answer "Top 20 Users by Usage"!

## 🔗 Integration

To use this engine in your own applications:

```csharp
// In your DI setup
services.AddNlToSqlEngine(configuration);

// In your service
public class YourService
{
    private readonly INlToSqlEngine _engine;
    
    public YourService(INlToSqlEngine engine)
    {
        _engine = engine;
    }
    
    public async Task<string> AnalyzeAsync(string query)
    {
        var result = await _engine.ProcessNaturalLanguageQueryAsync(query);
        return result.FinalAnswer;
    }
}
```