using Hangfire;
using Microsoft.EntityFrameworkCore;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline.Jobs;

namespace Top5Agent.Api.Endpoints;

public static class ScriptsEndpoints
{
    public static void MapScriptsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scripts").WithTags("Scripts");

        group.MapGet("/{ideaId:guid}", async (Guid ideaId, AppDbContext db) =>
        {
            var script = await db.Scripts
                .Include(s => s.Sections)
                .Include(s => s.Reviews)
                .Include(s => s.Sources)
                .FirstOrDefaultAsync(s => s.IdeaId == ideaId);

            return script is null ? Results.NotFound() : Results.Ok(script);
        })
        .WithSummary("Get script by idea ID")
        .WithDescription("Returns the script for a given idea, including all sections (hook, items 1–5, outro), fact-check reviews, and source URLs.")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            StatusUpdateRequest req,
            AppDbContext db,
            IBackgroundJobClient jobClient) =>
        {
            var script = await db.Scripts.FindAsync(id);
            if (script is null) return Results.NotFound();

            var allowed = new[] { ScriptStatus.Approved, ScriptStatus.NeedsReview };
            if (!allowed.Contains(req.Status))
                return Results.BadRequest($"Status must be one of: {string.Join(", ", allowed)}");

            script.Status = req.Status;
            await db.SaveChangesAsync();

            if (req.Status == ScriptStatus.Approved)
            {
                jobClient.Enqueue<DownloadMediaJob>(j => j.ExecuteAsync(id, Guid.Empty, CancellationToken.None));
            }

            return Results.Ok(new { script.Id, script.Status });
        })
        .WithSummary("Update script status")
        .WithDescription("""
            Approve or flag a script for review.
            Approving enqueues media download: for each script section, one video per media query is fetched from Pexels and saved locally.
            Marking as 'needs_review' flags the script for manual inspection without triggering any background job.
            Allowed values: 'approved', 'needs_review'.
            """)
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}
