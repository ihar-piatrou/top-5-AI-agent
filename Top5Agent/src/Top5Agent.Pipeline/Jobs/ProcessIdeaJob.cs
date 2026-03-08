using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline.Jobs;

public class ProcessIdeaJob(
    ScriptWriterService scriptWriter,
    FactReviewerService factReviewer,
    ContentPolisherService contentPolisher,
    AppDbContext db,
    ILogger<ProcessIdeaJob> logger)
{
    public async Task ExecuteAsync(Guid ideaId, Guid runId, CancellationToken ct = default)
    {
        var idea = await db.Ideas.FindAsync([ideaId], ct)
            ?? throw new InvalidOperationException($"Idea {ideaId} not found.");

        logger.LogInformation("Processing idea {IdeaId}: {Title}", ideaId, idea.Title);

        // 4a. Write script
        var script = await scriptWriter.WriteAsync(idea, ct);

        // 4b. Fact review
        await factReviewer.ReviewAsync(script.Id, ct);

        // 4c. Polish (skip if needs_review — requires human intervention)
        var refreshed = await db.Scripts.FindAsync([script.Id], ct);
        if (refreshed?.Status == Core.Models.ScriptStatus.Reviewed)
        {
            await contentPolisher.PolishAsync(script.Id, ct);
        }
        else
        {
            logger.LogWarning("Script {ScriptId} flagged for human review — skipping polish", script.Id);
        }

        logger.LogInformation("Idea {IdeaId} processing complete", ideaId);
    }
}
