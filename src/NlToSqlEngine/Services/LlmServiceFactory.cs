using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NlToSqlEngine.Configuration;
using NlToSqlEngine.Services.Anthropic;
using NlToSqlEngine.Services.AzureOpenAI;
using NlToSqlEngine.Services.OpenAI;

namespace NlToSqlEngine.Services
{
    /// <summary>
    /// Factory for creating the appropriate LLM service based on configuration
    /// </summary>
    public interface ILlmServiceFactory
    {
        ILlmService CreateLlmService();
    }

    public class LlmServiceFactory : ILlmServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LlmConfiguration _config;

        public LlmServiceFactory(IServiceProvider serviceProvider, IOptions<LlmConfiguration> config)
        {
            _serviceProvider = serviceProvider;
            _config = config.Value;
        }

        public ILlmService CreateLlmService()
        {
            return _config.Provider.ToLower() switch
            {
                "anthropic" or "claude" => _serviceProvider.GetRequiredService<AnthropicLlmService>(),
                "openai" => _serviceProvider.GetRequiredService<OpenAILlmService>(),
                "azureopenai" or "azure" => _serviceProvider.GetRequiredService<AzureOpenAILlmService>(),
                _ => throw new InvalidOperationException($"Unknown LLM provider: {_config.Provider}")
            };
        }
    }
}