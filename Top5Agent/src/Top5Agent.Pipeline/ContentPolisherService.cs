using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class ContentPolisherService(
    ILlmClient claudeClient,
    AppDbContext db,
    ILogger<ContentPolisherService> logger)
{
    public async Task<Script> PolishAsync(Guid scriptId, CancellationToken ct = default)
    {
        var script = await db.Scripts
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == scriptId, ct)
            ?? throw new InvalidOperationException($"Script {scriptId} not found.");

        var latestReview = script.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        if (latestReview is null)
            throw new InvalidOperationException($"Script {scriptId} has no review to polish from.");

        logger.LogInformation("Polishing script {ScriptId}", scriptId);

        var polishedJson = await claudeClient.CompleteAsync(
            Prompts.ContentPolisherSystem,
            Prompts.ContentPolisherUser(script.JsonContent, latestReview.ReviewText ?? "[]"),
            ct);

        var cleanJson = StripMarkdownFences(polishedJson);

        // Validate the JSON is parseable before saving
        var parsed = JsonSerializer.Deserialize<ScriptJson>(cleanJson)
            ?? throw new InvalidOperationException("Claude returned unparseable polished script JSON.");

        script.JsonContent = cleanJson;
        script.RawText = BuildRawText(parsed);
        script.Status = ScriptStatus.Polished;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Script {ScriptId} polished and saved", scriptId);
        return script;
    }

    private static string StripMarkdownFences(string raw)
    {
        var json = raw.Trim();
        if (!json.StartsWith("```")) return json;

        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        return start >= 0 && end > start ? json[start..(end + 1)] : json;
    }

    private static string BuildRawText(ScriptJson script)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {script.Title}");
        sb.AppendLine();
        sb.AppendLine("## Hook");
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
