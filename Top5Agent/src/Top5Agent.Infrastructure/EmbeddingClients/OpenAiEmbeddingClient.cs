using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using Top5Agent.Core.Interfaces;

namespace Top5Agent.Infrastructure.EmbeddingClients;

public class OpenAiEmbeddingClient(OpenAIClient openAiClient, ILogger<OpenAiEmbeddingClient> logger) : IEmbeddingClient
{
    private const string Model = "text-embedding-3-small";

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        logger.LogDebug("Getting embedding for text of length {Length}", text.Length);

        var embeddingClient = openAiClient.GetEmbeddingClient(Model);
        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);

        return response.Value.ToFloats().ToArray();
    }
}
