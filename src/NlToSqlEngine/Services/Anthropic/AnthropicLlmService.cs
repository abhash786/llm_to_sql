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

namespace NlToSqlEngine.Services.Anthropic
{
    /// <summary>
    /// Anthropic Claude LLM service implementation
    /// The perfect choice since this engine replicates Claude's methodology!
    /// </summary>
    public class AnthropicLlmService : ILlmService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfiguration _config;
        private readonly ILogger<AnthropicLlmService> _logger;

        public AnthropicLlmService(
            HttpClient httpClient,
            IOptions<LlmConfiguration> config,
            ILogger<AnthropicLlmService> logger)
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
                new() { Role = "user", Content = userPrompt }
            };

            return await GetCompletionAsync(messages, systemPrompt);
        }

        public async Task<string> GetCompletionAsync(List<LlmMessage> messages)
        {
            // Extract system message if present
            var systemMessage = messages.FirstOrDefault(m => m.Role == "system")?.Content ?? "";
            var userMessages = messages.Where(m => m.Role != "system").ToList();
            
            return await GetCompletionAsync(userMessages, systemMessage);
        }

        private async Task<string> GetCompletionAsync(List<LlmMessage> messages, string systemPrompt = "")
        {
            _logger.LogDebug("🧠 Requesting Claude completion with {MessageCount} messages", messages.Count);

            var requestPayload = CreateRequestPayload(messages, systemPrompt);
            var endpoint = "https://api.anthropic.com/v1/messages";

            try
            {
                var jsonContent = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = false });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogDebug("📤 Sending request to Claude: {Endpoint}", endpoint);

                var response = await _httpClient.PostAsync(endpoint, content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Claude API returned error: {StatusCode} - {Content}", 
                        response.StatusCode, responseContent.Substring(0, Math.Min(300, responseContent.Length)));
                    throw new HttpRequestException($"Claude API error ({response.StatusCode}): {responseContent}");
                }

                var completion = ParseResponse(responseContent);

                _logger.LogDebug("📨 Received Claude response: {Length} characters", completion?.Length ?? 0);
                _logger.LogInformation("✨ Claude (the original inspiration for this engine) provided the analysis!");

                return completion ?? throw new InvalidOperationException("No completion received from Claude");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "❌ HTTP error communicating with Claude");
                throw new InvalidOperationException($"Failed to communicate with Claude: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error during Claude completion");
                throw;
            }
        }

        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "messages-2023-12-15");
        }

        private object CreateRequestPayload(List<LlmMessage> messages, string systemPrompt)
        {
            var payload = new
            {
                model = _config.ModelName,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                system = string.IsNullOrEmpty(systemPrompt) ? 
                    "You are Claude, an AI assistant created by Anthropic. You excel at database analysis and provide thorough, step-by-step reasoning." : 
                    systemPrompt,
                messages = messages.Select(m => new { 
                    role = m.Role == "assistant" ? "assistant" : "user", // Claude only supports user/assistant 
                    content = m.Content 
                }).ToArray()
            };

            return payload;
        }

        private string ParseResponse(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Handle successful response
                if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
                {
                    var firstContent = content[0];
                    if (firstContent.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }

                // Handle error response
                if (root.TryGetProperty("error", out var error))
                {
                    var errorType = error.TryGetProperty("type", out var type) ? type.GetString() : "unknown_error";
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    
                    _logger.LogError("❌ Claude API error - Type: {ErrorType}, Message: {ErrorMessage}", errorType, errorMessage);
                    throw new InvalidOperationException($"Claude API error ({errorType}): {errorMessage}");
                }

                // Handle different response types
                if (root.TryGetProperty("type", out var responseType))
                {
                    var typeValue = responseType.GetString();
                    if (typeValue == "message")
                    {
                        // This is a complete message response, try parsing content again
                        if (root.TryGetProperty("content", out var messageContent) && messageContent.GetArrayLength() > 0)
                        {
                            var firstMessageContent = messageContent[0];
                            if (firstMessageContent.TryGetProperty("text", out var messageText))
                            {
                                return messageText.GetString() ?? "";
                            }
                        }
                    }
                    else if (typeValue == "error")
                    {
                        var errorMessage = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                        throw new InvalidOperationException($"Claude error: {errorMessage}");
                    }
                }

                // Log the unexpected response for debugging
                _logger.LogWarning("⚠️ Unexpected Claude response format: {Response}", 
                    responseContent.Substring(0, Math.Min(200, responseContent.Length)));
                
                throw new InvalidOperationException("Unexpected response format from Claude API");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "❌ Failed to parse Claude response: {Response}", 
                    responseContent.Substring(0, Math.Min(500, responseContent.Length)));
                throw new InvalidOperationException($"Invalid JSON response from Claude: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}