using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class IdeaGeneratorService(
    ILlmClient gptClient,
    AppDbContext db,
    ILogger<IdeaGeneratorService> logger)
{
    public async Task<List<Idea>> GenerateAsync(string niche, int count = 10, CancellationToken ct = default)
    {
        var existingTitles = await db.Ideas
            .Select(i => i.Title)
            .ToArrayAsync(ct);

        logger.LogInformation("Generating {Count} ideas for niche '{Niche}' (existing: {Existing})",
            count, niche, existingTitles.Length);

        var rawJson = await gptClient.CompleteAsync(
            Prompts.IdeaGeneratorSystem,
            Prompts.IdeaGeneratorUser(niche, count, existingTitles),
            ct);

        var ideaDtos = ParseIdeaJson(rawJson);

        var ideas = ideaDtos.Select(dto => new Idea
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Niche = dto.Niche,
            Summary = dto.Summary,
            Status = IdeaStatus.Pending,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        db.Ideas.AddRange(ideas);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Saved {Count} new ideas to database", ideas.Count);
        return ideas;
    }

    private static List<IdeaJson> ParseIdeaJson(string raw)
    {
        var json = raw.Trim();
        // Strip markdown fences if present
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];
        }

        return JsonSerializer.Deserialize<List<IdeaJson>>(json)
            ?? throw new InvalidOperationException("LLM returned null or unparseable idea JSON.");
    }
}
