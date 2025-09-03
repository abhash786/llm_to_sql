using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NlToSqlEngine.Configuration;
using NlToSqlEngine.Core;
using NlToSqlEngine.DependencyInjection;
using NlToSqlEngine.Models;
using System.Text.Json;

namespace NlToSqlEngine.ConsoleApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Natural Language to SQL Engine - Console Demo");
        Console.WriteLine("This engine replicates Claude's database analysis methodology");
        Console.WriteLine("===========================================================");
        Console.WriteLine();

        try
        {
            // Build configuration
            var configuration = BuildConfiguration();
            
            // Setup dependency injection
            var host = CreateHostBuilder(configuration).Build();
            
            // Get the engine service
            var engine = host.Services.GetRequiredService<INlToSqlEngine>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("🔧 Engine initialized successfully");

            // Demo queries that match your original test
            var demoQueries = new[]
            {
                "Top 20 Users by Usage",
                "Show me users with the highest activity in the last month", 
                "Which departments have the most active users?",
                "List all users who have never logged in",
                "Find users with administrative roles"
            };

            Console.WriteLine("📋 Available demo queries:");
            for (int i = 0; i < demoQueries.Length; i++)
            {
                Console.WriteLine($"  {i + 1}. {demoQueries[i]}");
            }
            Console.WriteLine($"  {demoQueries.Length + 1}. Enter custom query");
            Console.WriteLine($"  0. Exit");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Select a query (0-6): ");
                var input = Console.ReadLine();

                if (input == "0" || string.IsNullOrWhiteSpace(input))
                {
                    break;
                }

                string queryToExecute = "";

                if (int.TryParse(input, out int selection))
                {
                    if (selection >= 1 && selection <= demoQueries.Length)
                    {
                        queryToExecute = demoQueries[selection - 1];
                    }
                    else if (selection == demoQueries.Length + 1)
                    {
                        Console.Write("Enter your custom query: ");
                        queryToExecute = Console.ReadLine() ?? "";
                    }
                    else
                    {
                        Console.WriteLine("❌ Invalid selection. Please try again.");
                        continue;
                    }
                }
                else
                {
                    // Treat as direct query
                    queryToExecute = input;
                }

                if (string.IsNullOrWhiteSpace(queryToExecute))
                {
                    Console.WriteLine("❌ Empty query. Please try again.");
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"🔍 Processing query: '{queryToExecute}'");
                Console.WriteLine("📊 Analysis in progress...");
                Console.WriteLine();

                var startTime = DateTime.Now;

                try
                {
                    // Execute the query using our engine
                    var result = await engine.ProcessNaturalLanguageQueryAsync(queryToExecute);
                    
                    var duration = DateTime.Now - startTime;

                    // Display results
                    DisplayResults(result, duration);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing query: {ex.Message}");
                    logger.LogError(ex, "Query processing failed");
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                Console.WriteLine();
            }

            Console.WriteLine("👋 Thank you for using the NL to SQL Engine!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Fatal error: {ex.Message}");
            Console.WriteLine("Please check your configuration and try again.");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Details: {ex.InnerException.Message}");
            }
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()  // For storing API keys securely
            .Build();
    }

    private static IHostBuilder CreateHostBuilder(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add our NL to SQL Engine services
                services.AddNlToSqlEngine(configuration);
                
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
    }

    private static void DisplayResults(SqlQueryResult result, TimeSpan duration)
    {
        Console.WriteLine("🎉 ANALYSIS COMPLETE!");
        Console.WriteLine($"⏱️  Total execution time: {duration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"📊 Success: {(result.Success ? "✅ Yes" : "❌ No")}");
        Console.WriteLine();

        if (!result.Success)
        {
            Console.WriteLine($"❌ Error: {result.ErrorMessage}");
            return;
        }

        // Display execution steps (like Claude's step-by-step analysis)
        Console.WriteLine("📋 EXECUTION STEPS:");
        Console.WriteLine(new string('=', 50));
        
        foreach (var step in result.ExecutionSteps)
        {
            Console.WriteLine($"Step {step.StepNumber}: {step.Description}");
            Console.WriteLine($"  🎯 Purpose: {step.Purpose}");
            Console.WriteLine($"  💭 Reasoning: {step.Reasoning}");
            Console.WriteLine($"  ⏱️  Duration: {step.ExecutionTime.TotalMilliseconds:F0}ms");
            Console.WriteLine($"  📊 Results: {step.Results?.Count ?? 0} records");
            
            if (!string.IsNullOrEmpty(step.SqlQuery) && !step.SqlQuery.StartsWith("--"))
            {
                Console.WriteLine($"  🔍 SQL: {TruncateString(step.SqlQuery, 100)}");
            }
            
            Console.WriteLine();
        }

        // Display final answer
        if (!string.IsNullOrEmpty(result.FinalAnswer))
        {
            Console.WriteLine("💬 FINAL ANSWER:");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine(result.FinalAnswer);
            Console.WriteLine();
        }

        // Display key metrics
        if (result.Insights?.KeyMetrics?.Any() == true)
        {
            Console.WriteLine("📊 KEY METRICS:");
            Console.WriteLine(new string('=', 50));
            
            foreach (var metric in result.Insights.KeyMetrics.Take(5))
            {
                Console.WriteLine($"• {metric.Name}: {metric.Value}");
                if (!string.IsNullOrEmpty(metric.Description))
                {
                    Console.WriteLine($"  └─ {metric.Description}");
                }
            }
            Console.WriteLine();
        }

        // Display data sample
        if (result.Data?.Any() == true)
        {
            Console.WriteLine($"📄 SAMPLE DATA ({Math.Min(5, result.Data.Count)} of {result.Data.Count} records):");
            Console.WriteLine(new string('=', 80));
            
            var sampleData = result.Data.Take(5).ToList();
            
            if (sampleData.Any())
            {
                // Display as table
                var firstRecord = sampleData.First();
                var columns = firstRecord.Keys.Take(4).ToList(); // Show first 4 columns
                
                // Header
                Console.WriteLine(string.Join(" | ", columns.Select(c => c.PadRight(15))));
                Console.WriteLine(new string('-', columns.Count * 18));
                
                // Data rows
                foreach (var record in sampleData)
                {
                    var values = columns.Select(col => 
                    {
                        var value = record.GetValueOrDefault(col)?.ToString() ?? "";
                        return TruncateString(value, 15).PadRight(15);
                    });
                    
                    Console.WriteLine(string.Join(" | ", values));
                }
            }
            Console.WriteLine();
        }

        // Display business insights
        if (result.Insights != null)
        {
            if (result.Insights.Patterns?.Any() == true)
            {
                Console.WriteLine("🔍 PATTERNS IDENTIFIED:");
                Console.WriteLine(new string('=', 50));
                foreach (var pattern in result.Insights.Patterns.Take(3))
                {
                    Console.WriteLine($"• {pattern}");
                }
                Console.WriteLine();
            }

            if (result.Insights.Recommendations?.Any() == true)
            {
                Console.WriteLine("💡 RECOMMENDATIONS:");
                Console.WriteLine(new string('=', 50));
                foreach (var recommendation in result.Insights.Recommendations.Take(3))
                {
                    Console.WriteLine($"• {recommendation}");
                }
                Console.WriteLine();
            }
        }

        // Display execution metadata
        Console.WriteLine("🔧 EXECUTION METADATA:");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"• Total Steps: {result.Metadata.TotalSteps}");
        Console.WriteLine($"• Tables Analyzed: {string.Join(", ", result.Metadata.TablesAnalyzed.Take(3))}");
        Console.WriteLine($"• Schemas Explored: {string.Join(", ", result.Metadata.SchemasExplored)}");
        Console.WriteLine($"• Execution Time: {result.Metadata.TotalExecutionTime.TotalSeconds:F2}s");
    }

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            return input ?? "";
        
        return input.Substring(0, maxLength - 3) + "...";
    }
}