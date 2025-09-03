using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NlToSqlEngine.Configuration;

namespace NlToSqlEngine.Services.AzureOpenAI
{
    /// <summary>
    /// Azure OpenAI LLM service implementation for enterprise GPT deployments
    /// </summary>
    public class AzureOpenAILlmService : ILlmService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfiguration _config;
        private readonly ILogger<AzureOpenAILlmService> _logger;

        public AzureOpenAILlmService(
            HttpClient httpClient,
            IOptions<LlmConfiguration> config,
            ILogger<AzureOpenAILlmService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            
            ConfigureHttpClient();
        }

        public async Task<string> GetCompletionAsync(string systemPrompt, string userPrompt)
        {
            var messages = new List<LlmMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            return await GetCompletionAsync(messages);
        }

        public async Task<string> GetCompletionAsync(List<LlmMessage> messages)
        {
            _logger.LogDebug("🧠 Requesting Azure OpenAI completion with {MessageCount} messages", messages.Count);

            var requestPayload = CreateRequestPayload(messages);
            var endpoint = GetEndpointUrl();

            try
            {
                var jsonContent = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = false });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("📤 Sending request to Azure OpenAI: {Endpoint}", endpoint.Substring(0, Math.Min(50, endpoint.Length)));

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var completion = ParseResponse(responseContent);

                _logger.LogDebug("📨 Received Azure OpenAI response: {Length} characters", completion?.Length ?? 0);

                return completion ?? throw new InvalidOperationException("No completion received from Azure OpenAI");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP error communicating with Azure OpenAI");
                throw new InvalidOperationException($"Failed to communicate with Azure OpenAI: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error during Azure OpenAI completion");
                throw;
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
        }

        private string GetEndpointUrl()
        {
            if (string.IsNullOrEmpty(_config.Endpoint))
            {
                throw new InvalidOperationException("Azure OpenAI endpoint is required");
            }

            var baseUrl = _config.Endpoint.TrimEnd('/');
            return $"{baseUrl}/openai/deployments/{_config.ModelName}/chat/completions?api-version=2023-12-01-preview";
        }

        private object CreateRequestPayload(List<LlmMessage> messages)
        {
            return new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                stream = false
            };
        }

        private string ParseResponse(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        return content.GetString() ?? "";
                    }
                }

                // Check for errors
                if (root.TryGetProperty("error", out var error))
                {
                    var errorMessage = error.TryGetProperty("message", out var msg) ? 
                        msg.GetString() : "Unknown error";
                    var errorCode = error.TryGetProperty("code", out var code) ? 
                        code.GetString() : "unknown";
                    
                    throw new InvalidOperationException($"Azure OpenAI API error ({errorCode}): {errorMessage}");
                }

                throw new InvalidOperationException("Unexpected response format from Azure OpenAI");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ Failed to parse Azure OpenAI response: {Response}", 
                    responseContent.Substring(0, Math.Min(500, responseContent.Length)));
                throw new InvalidOperationException($"Invalid JSON response from Azure OpenAI: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}