using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class ScriptWriterService(
    ILlmClient claudeClient,
    AppDbContext db,
    ILogger<ScriptWriterService> logger)
{
    public async Task<Script> WriteAsync(Idea idea, CancellationToken ct = default)
    {
        // Idempotency: return existing script if already written (handles Hangfire retries)
        var existing = await db.Scripts
            .FirstOrDefaultAsync(s => s.IdeaId == idea.Id && s.Status != ScriptStatus.Draft, ct);
        if (existing is not null)
        {
            logger.LogInformation("Script already exists for idea {IdeaId}, skipping write", idea.Id);
            return existing;
        }

        logger.LogInformation("Writing script for idea: {Title}", idea.Title);

        var rawJson = await claudeClient.CompleteAsync(
            Prompts.ScriptWriterSystem,
            Prompts.ScriptWriterUser(idea.Title, idea.Summary ?? string.Empty),
            ct);

        var scriptJson = ParseScriptJson(rawJson);
        var rawText = BuildRawText(scriptJson);

        var script = new Script
        {
            Id = Guid.NewGuid(),
            IdeaId = idea.Id,
            JsonContent = rawJson,
            RawText = rawText,
            Status = ScriptStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        var sections = BuildSections(script.Id, scriptJson);
        script.Sections = sections;

        db.Scripts.Add(script);
        await db.SaveChangesAsync(ct);

        idea.Status = IdeaStatus.Scripted;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Script {ScriptId} created with {Sections} sections", script.Id, sections.Count);
        return script;
    }

    private static ScriptJson ParseScriptJson(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];
        }

        return JsonSerializer.Deserialize<ScriptJson>(json)
            ?? throw new InvalidOperationException("LLM returned null or unparseable script JSON.");
    }

    private static List<ScriptSection> BuildSections(Guid scriptId, ScriptJson script)
    {
        var sections = new List<ScriptSection>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ScriptId = scriptId,
                Position = 0,
                Title = "Hook",
                Narration = script.Hook,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var item in script.Items)
        {
            var firstMedia = item.Media.FirstOrDefault();
            sections.Add(new ScriptSection
            {
                Id = Guid.NewGuid(),
                ScriptId = scriptId,
                Position = item.Position,
                Title = item.Title,
                Narration = item.Narration,
                MediaQuery = firstMedia?.Query,
                MediaType = firstMedia?.Type,
                CreatedAt = DateTime.UtcNow
            });
        }

        sections.Add(new ScriptSection
        {
            Id = Guid.NewGuid(),
            ScriptId = scriptId,
            Position = 99,
            Title = "Outro",
            Narration = script.Outro,
            CreatedAt = DateTime.UtcNow
        });

        return sections;
    }

    private static string BuildRawText(ScriptJson script)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {script.Title}");
        sb.AppendLine();
        sb.AppendLine($"## Hook");
        sb.AppendLine(script.Hook);
        sb.AppendLine();

        foreach (var item in script.Items)
        {
            sb.AppendLine($"## {item.Position}. {item.Title}");
            sb.AppendLine(item.Narration);
            sb.AppendLine();
        }

        sb.AppendLine("## Outro");
        sb.AppendLine(script.Outro);
        return sb.ToString();
    }
}
