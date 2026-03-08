using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Interfaces;

namespace Top5Agent.Infrastructure.LlmClients;

public class ClaudeClient(HttpClient httpClient, ILogger<ClaudeClient> logger) : ILlmClient
{
    private const string Model = "claude-sonnet-4-6";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        logger.LogDebug("Calling Claude {Model} with user prompt length {Length}", Model, userPrompt.Length);

        var requestBody = new AnthropicRequest
        {
            Model = Model,
            MaxTokens = 4096,
            System = systemPrompt,
            Messages = [new AnthropicMessage { Role = "user", Content = userPrompt }]
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("v1/messages", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from Anthropic API.");

        var text = result.Content.FirstOrDefault()?.Text ?? string.Empty;
        logger.LogDebug("Claude responded with {Length} characters", text.Length);
        return text;
    }

    // ── Private request/response DTOs ─────────────────────────────────────────

    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]    public string Model { get; set; } = string.Empty;
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")]   public string System { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public AnthropicMessage[] Messages { get; set; } = [];
    }

    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]    public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")] public AnthropicContent[] Content { get; set; } = [];
    }

    private sealed class AnthropicContent
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }
}
