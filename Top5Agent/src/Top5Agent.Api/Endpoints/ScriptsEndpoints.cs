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
        });

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
        });
    }
}
