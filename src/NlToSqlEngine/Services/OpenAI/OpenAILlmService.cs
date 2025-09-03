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

namespace NlToSqlEngine.Services.OpenAI
{
    /// <summary>
    /// OpenAI LLM service implementation for GPT models
    /// </summary>
    public class OpenAILlmService : ILlmService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfiguration _config;
        private readonly ILogger<OpenAILlmService> _logger;

        public OpenAILlmService(
            HttpClient httpClient,
            IOptions<LlmConfiguration> config,
            ILogger<OpenAILlmService> logger)
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
            _logger.LogDebug("🧠 Requesting OpenAI completion with {MessageCount} messages", messages.Count);

            var requestPayload = CreateRequestPayload(messages);
            var endpoint = "https://api.openai.com/v1/chat/completions";

            try
            {
                var jsonContent = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = false });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("📤 Sending request to OpenAI: {Endpoint}", endpoint);

                var response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var completion = ParseResponse(responseContent);

                _logger.LogDebug("📨 Received OpenAI response: {Length} characters", completion?.Length ?? 0);

                return completion ?? throw new InvalidOperationException("No completion received from OpenAI");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP error communicating with OpenAI");
                throw new InvalidOperationException($"Failed to communicate with OpenAI: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error during OpenAI completion");
                throw;
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }

        private object CreateRequestPayload(List<LlmMessage> messages)
        {
            return new
            {
                model = _config.ModelName,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature
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
                    throw new InvalidOperationException($"OpenAI API error: {errorMessage}");
                }

                throw new InvalidOperationException("Unexpected response format from OpenAI");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ Failed to parse OpenAI response: {Response}", 
                    responseContent.Substring(0, Math.Min(500, responseContent.Length)));
                throw new InvalidOperationException($"Invalid JSON response from OpenAI: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}