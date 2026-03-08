using System.ClientModel;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Top5Agent.Core.Interfaces;

namespace Top5Agent.Infrastructure.LlmClients;

public class GptClient(OpenAIClient openAiClient, ILogger<GptClient> logger) : ILlmClient
{
    private const string Model = "gpt-4o";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var chatClient = openAiClient.GetChatClient(Model);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f
        };

        logger.LogDebug("Calling GPT-4o with user prompt length {Length}", userPrompt.Length);

        var response = await chatClient.CompleteChatAsync(messages, options, ct);
        var content = response.Value.Content[0].Text;

        logger.LogDebug("GPT-4o responded with {Length} characters", content.Length);
        return content;
    }
}
