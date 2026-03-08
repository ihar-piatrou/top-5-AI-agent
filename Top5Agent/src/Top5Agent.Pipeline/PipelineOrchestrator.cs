using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline.Jobs;

namespace Top5Agent.Pipeline;

public class PipelineOrchestrator(
    IdeaGeneratorService ideaGenerator,
    DuplicateDetectorService duplicateDetector,
    AppDbContext db,
    IBackgroundJobClient jobClient,
    ILogger<PipelineOrchestrator> logger)
{
    public async Task<Guid> RunAsync(string niche, int count = 10, bool autoApprove = false, CancellationToken ct = default)
    {
        var run = new PipelineRun
        {
            Id = Guid.NewGuid(),
            TriggerReason = $"Manual run for niche '{niche}'",
            Status = PipelineRunStatus.Running,
            CreatedAt = DateTime.UtcNow
        };

        db.PipelineRuns.Add(run);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Pipeline run {RunId} started for niche '{Niche}'", run.Id, niche);

        try
        {
            // Step 1: Generate ideas
            var ideas = await ideaGenerator.GenerateAsync(niche, count, ct);

            // Step 2: Deduplicate
            var uniqueIdeas = new List<Idea>();
            foreach (var idea in ideas)
            {
                var isDuplicate = await duplicateDetector.IsDuplicateAsync(idea.Title, ct);
                if (isDuplicate)
                {
                    idea.Status = IdeaStatus.Rejected;
                    logger.LogInformation("Rejected duplicate idea: {Title}", idea.Title);
                }
                else
                {
                    await duplicateDetector.StoreEmbeddingAsync(idea.Id, idea.Title, ct);
                    uniqueIdeas.Add(idea);
                }
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation("After deduplication: {Unique} unique ideas, {Rejected} rejected",
                uniqueIdeas.Count, ideas.Count - uniqueIdeas.Count);

            // Step 3: Auto-approve or pause for human review
            if (autoApprove)
            {
                foreach (var idea in uniqueIdeas)
                {
                    idea.Status = IdeaStatus.Approved;
                    jobClient.Enqueue<ProcessIdeaJob>(j => j.ExecuteAsync(idea.Id, run.Id, CancellationToken.None));
                }
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Auto-approved {Count} ideas and enqueued processing jobs", uniqueIdeas.Count);
            }
            else
            {
                logger.LogInformation("Pipeline paused — {Count} ideas await human approval via API", uniqueIdeas.Count);
            }

            run.Status = PipelineRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return run.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pipeline run {RunId} failed", run.Id);
            run.Status = PipelineRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }
}
