# 🤖 Natural Language to SQL Engine

A sophisticated NL to SQL engine that **exactly replicates Claude's database analysis methodology** using LLM intelligence and MCP (Model Context Protocol) tools.

## 🎯 What This Engine Does

This engine mimics the **exact same approach** Claude uses when analyzing databases:

1. **🔍 Schema Discovery** - Explores database structure intelligently
2. **📊 Progressive Analysis** - Builds understanding step-by-step  
3. **🧠 LLM-Powered Planning** - Uses AI to create smart execution strategies
4. **⚡ MCP Tools Integration** - Replicates Claude's database exploration tools
5. **💡 Business Insights** - Generates actionable recommendations
6. **📝 Natural Language Answers** - Provides human-readable responses

## 🚀 Quick Demo

**Input:** "Top 20 Users by Usage"

**Output:** Complete analysis with step-by-step reasoning, data insights, and business recommendations - just like Claude!

## 🏗️ Architecture Overview

```
Natural Language Query
         ↓
   LLM Query Analyzer (GPT-4/Claude)
         ↓
   Schema Discovery Service
         ↓
   LLM Query Planner 
         ↓
   Progressive Query Executor
         ↓
   Business Insight Analyzer
         ↓
   Natural Language Answer
```

## 📁 Project Structure

```
D:\Projects\llm_based_engine_claude\
├── src/
│   └── NlToSqlEngine/                    # Main engine library
│       ├── Core/                         # Core engine interfaces & services
│       ├── Models/                       # Data models and DTOs
│       ├── Components/                   # Analysis components
│       │   ├── LlmQueryAnalyzerService   # AI-powered query understanding
│       │   ├── SchemaDiscoveryService    # Database exploration  
│       │   ├── QueryPlannerService       # Execution planning
│       │   ├── QueryExecutorService      # Progressive execution
│       │   └── BusinessInsightAnalyzer   # Insights generation
│       ├── MCP/                          # MCP tools (Claude's DB tools)
│       ├── Services/                     # LLM integration services
│       ├── Configuration/                # Configuration models
│       └── DependencyInjection/          # DI extensions
└── samples/
    └── NlToSqlEngine.ConsoleApp/         # Demo console application
```

## 🎮 Try It Now

### 1. Clone & Setup
```bash
git clone <your-repo-url>
cd llm_based_engine_claude
```

### 2. Configure
Edit `samples/NlToSqlEngine.ConsoleApp/appsettings.json`:

```json
{
  "LlmConfiguration": {
    "Provider": "OpenAI",
    "ApiKey": "your-openai-api-key",
    "ModelName": "gpt-4"
  },
  "DatabaseConfiguration": {
    "ConnectionString": "your-sql-server-connection-string"
  }
}
```

### 3. Run Demo
```bash
cd samples/NlToSqlEngine.ConsoleApp
dotnet run
```

### 4. Ask Questions
```
🔍 Processing query: 'Top 20 customers by Usage'
📊 Analysis in progress...

📋 EXECUTION STEPS:
Step 1: Schema discovery and exploration
Step 2: Sample UserRoleUsages table data
Step 3: Join users with usage data
Step 4: Calculate usage metrics
Step 5: Generate final ranking query

💬 FINAL ANSWER:
Based on the analysis, I found the top 20 users by usage...

📊 KEY METRICS:
• Total Records: 20
• Average Usage: 0.75 actions per user
• Top User: Rashmi Kulkarni (5 actions)
```

## 🧠 LLM Integration

Supports multiple LLM providers:

- **OpenAI** (GPT-4, GPT-3.5-turbo)
- **Azure OpenAI** (Enterprise deployments)  
- **Anthropic Claude** (Claude-3-sonnet, Claude-3-opus)

## 🔧 Key Features

### ✅ **LLM-Powered Intelligence**
- No hardcoded patterns - uses AI to understand queries
- Intelligent schema discovery based on query intent
- Smart execution planning with reasoning

### ✅ **MCP Tools Replication**
- `list_schemas`, `list_tables`, `search_schema`
- `describe_table`, `foreign_keys`, `table_stats`
- `sample_rows`, `query_select`, `explain_query`
- Exact same tools Claude uses!

### ✅ **Progressive Analysis**
- Step-by-step query building
- Context-aware execution  
- Each step informs the next

### ✅ **Business Intelligence**
- Statistical analysis and patterns
- Actionable business recommendations
- Risk identification and opportunities

### ✅ **Production Ready**
- Comprehensive logging and error handling
- Configurable timeouts and limits
- Full dependency injection support
- Multiple database provider support

## 🔗 Integration Examples

### ASP.NET Core API
```csharp
[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly INlToSqlEngine _engine;
    
    public QueryController(INlToSqlEngine engine)
    {
        _engine = engine;
    }
    
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] string query)
    {
        var result = await _engine.ProcessNaturalLanguageQueryAsync(query);
        return Ok(result);
    }
}

// In Program.cs
builder.Services.AddNlToSqlEngine(builder.Configuration);
```

### Console Application
```csharp
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddNlToSqlEngine(context.Configuration);
    })
    .Build();

var engine = host.Services.GetRequiredService<INlToSqlEngine>();
var result = await engine.ProcessNaturalLanguageQueryAsync("Top 10 customers by revenue");
Console.WriteLine(result.FinalAnswer);
```

### Blazor Application
```csharp
@inject INlToSqlEngine Engine

<input @bind="query" placeholder="Ask a question about your data..." />
<button @onclick="AnalyzeAsync">Analyze</button>

@if (result != null)
{
    <div class="results">
        <h3>Answer:</h3>
        <p>@result.FinalAnswer</p>
        
        <h3>Data:</h3>
        <table>
            @foreach (var record in result.Data.Take(10))
            {
                <tr>
                    @foreach (var kvp in record.Take(5))
                    {
                        <td>@kvp.Value</td>
                    }
                </tr>
            }
        </table>
    </div>
}

@code {
    private string query = "";
    private SqlQueryResult? result;
    
    private async Task AnalyzeAsync()
    {
        result = await Engine.ProcessNaturalLanguageQueryAsync(query);
    }
}
```

## 🎯 Example Queries

The engine excels at business intelligence queries:

- **"Top 20 users by usage"** ✅
- **"Which departments have the highest activity?"** ✅  
- **"Show me inactive users from last month"** ✅
- **"Compare user engagement across regions"** ✅
- **"Find users with administrative roles"** ✅
- **"What's the average session duration by department?"** ✅

## 🔐 Security & Performance

- **SQL Injection Protection** - Only SELECT statements allowed
- **Query Timeouts** - Configurable execution limits  
- **Result Size Limits** - Prevents memory issues
- **Rate Limiting** - LLM API call management
- **Comprehensive Logging** - Full audit trail

## 📊 Monitoring & Observability  

Built-in metrics and logging:

```csharp
services.AddNlToSqlEngine(configuration)
    .AddMetrics()  // Execution time, success rates
    .AddTracing()  // Distributed tracing support
    .AddHealthChecks(); // Database & LLM health checks
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Integration tests (requires database)
dotnet test --filter "Category=Integration"
```

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit pull request

## 📜 License

MIT License - See [LICENSE](LICENSE) file for details.

## 🙋 Support

- **Documentation**: See `/docs` folder
- **Issues**: GitHub Issues
- **Discussions**: GitHub Discussions

## 🎉 Acknowledgments

This project replicates the sophisticated database analysis methodology pioneered by Anthropic's Claude AI, bringing that same intelligence to your applications through LLM integration.

---

**Built with ❤️ to make database analysis as intelligent as Claude!**