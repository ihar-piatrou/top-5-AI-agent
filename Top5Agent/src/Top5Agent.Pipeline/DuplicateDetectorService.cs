using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Interfaces;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class DuplicateDetectorService(
    IEmbeddingClient embeddingClient,
    AppDbContext db,
    IConfiguration configuration,
    ILogger<DuplicateDetectorService> logger)
{
    private float SimilarityThreshold =>
        configuration.GetValue<float>("Pipeline:DuplicateThreshold", 0.85f);

    public async Task<bool> IsDuplicateAsync(string title, CancellationToken ct = default)
    {
        var candidate = await embeddingClient.GetEmbeddingAsync(title, ct);

        var existingEmbeddings = await db.Ideas
            .Where(i => i.Embedding != null)
            .Select(i => new { i.Title, i.Embedding })
            .ToListAsync(ct);

        if (existingEmbeddings.Count == 0)
            return false;

        float maxSimilarity = 0f;
        string? mostSimilarTitle = null;

        foreach (var existing in existingEmbeddings)
        {
            float[]? vector;
            try
            {
                vector = JsonSerializer.Deserialize<float[]>(existing.Embedding!);
            }
            catch
            {
                continue;
            }

            if (vector is null) continue;

            var similarity = CosineSimilarity(candidate, vector);
            if (similarity > maxSimilarity)
            {
                maxSimilarity = similarity;
                mostSimilarTitle = existing.Title;
            }
        }

        if (maxSimilarity >= SimilarityThreshold)
        {
            logger.LogInformation(
                "Duplicate detected: '{Title}' is {Similarity:P1} similar to '{Existing}'",
                title, maxSimilarity, mostSimilarTitle);
            return true;
        }

        return false;
    }

    public async Task StoreEmbeddingAsync(Guid ideaId, string title, CancellationToken ct = default)
    {
        var embedding = await embeddingClient.GetEmbeddingAsync(title, ct);
        var json = JsonSerializer.Serialize(embedding);

        var idea = await db.Ideas.FindAsync([ideaId], ct);
        if (idea is not null)
        {
            idea.Embedding = json;
            await db.SaveChangesAsync(ct);
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension.");

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f) return 0f;
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
