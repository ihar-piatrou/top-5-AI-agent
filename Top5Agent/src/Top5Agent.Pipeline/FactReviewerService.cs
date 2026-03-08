using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class FactReviewerService(
    ILlmClient gptClient,
    AppDbContext db,
    ILogger<FactReviewerService> logger)
{
    private const int UnverifiableClaimThreshold = 2;

    public async Task<List<FactCheckResult>> ReviewAsync(Guid scriptId, CancellationToken ct = default)
    {
        var script = await db.Scripts
            .Include(s => s.Idea)
            .FirstOrDefaultAsync(s => s.Id == scriptId, ct)
            ?? throw new InvalidOperationException($"Script {scriptId} not found.");

        var scriptJson = JsonSerializer.Deserialize<ScriptJson>(script.JsonContent)
            ?? throw new InvalidOperationException("Failed to deserialize script JSON.");

        var allClaims = scriptJson.Items
            .SelectMany(i => i.VerifyClaims)
            .Distinct()
            .ToArray();

        // Idempotency: skip if already reviewed (handles Hangfire retries)
        var alreadyReviewed = await db.ScriptReviews.AnyAsync(r => r.ScriptId == scriptId, ct);
        if (alreadyReviewed)
        {
            logger.LogInformation("Script {ScriptId} already reviewed, skipping", scriptId);
            return [];
        }

        if (allClaims.Length == 0)
        {
            logger.LogInformation("Script {ScriptId} has no claims to verify.", scriptId);
            script.Status = ScriptStatus.Reviewed;
            await db.SaveChangesAsync(ct);
            return [];
        }

        logger.LogInformation("Fact-checking {Count} claims for script {ScriptId}", allClaims.Length, scriptId);

        var rawJson = await gptClient.CompleteAsync(
            Prompts.FactReviewerSystem,
            Prompts.FactReviewerUser(script.Idea.Title, allClaims),
            ct);

        var results = ParseFactCheckJson(rawJson);

        // Save sources — deduplicated by URL (multiple claims can cite the same source)
        var sources = results
            .Where(r => r.SourceUrl is not null)
            .GroupBy(r => r.SourceUrl!)
            .Select(g => new Source
            {
                Id = Guid.NewGuid(),
                ScriptId = scriptId,
                Url = g.Key,
                Title = g.First().SourceTitle,
                Verified = g.Any(r => r.Verdict == "supported"),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        db.Sources.AddRange(sources);

        // Save review record
        var issuesJson = JsonSerializer.Serialize(
            results.Where(r => r.Verdict != "supported").Select(r => r.Claim).ToArray());

        var review = new ScriptReview
        {
            Id = Guid.NewGuid(),
            ScriptId = scriptId,
            Reviewer = "gpt-4o",
            ReviewText = rawJson,
            IssuesFound = issuesJson,
            Approved = results.All(r => r.Verdict == "supported"),
            CreatedAt = DateTime.UtcNow
        };

        db.ScriptReviews.Add(review);

        // Flag scripts with too many unverifiable claims
        var unverifiable = results.Count(r => r.Verdict is "unsupported" or "uncertain" && r.Rewrite is null);
        script.Status = unverifiable > UnverifiableClaimThreshold
            ? ScriptStatus.NeedsReview
            : ScriptStatus.Reviewed;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Fact review complete: {Supported} supported, {Issues} issues, status={Status}",
            results.Count(r => r.Verdict == "supported"), unverifiable, script.Status);

        return results;
    }

    private static List<FactCheckResult> ParseFactCheckJson(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];
        }

        return JsonSerializer.Deserialize<List<FactCheckResult>>(json)
            ?? throw new InvalidOperationException("LLM returned null or unparseable fact-check JSON.");
    }
}
