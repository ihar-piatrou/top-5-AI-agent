using Hangfire;
using Microsoft.EntityFrameworkCore;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline.Jobs;

namespace Top5Agent.Api.Endpoints;

public static class IdeasEndpoints
{
    public static void MapIdeasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ideas").WithTags("Ideas");

        group.MapGet("/", async (string? status, AppDbContext db) =>
        {
            var query = db.Ideas.AsQueryable();
            if (status is not null)
                query = query.Where(i => i.Status == status);

            var ideas = await query
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new
                {
                    i.Id,
                    i.Title,
                    i.Niche,
                    i.Summary,
                    i.Status,
                    i.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(ideas);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var idea = await db.Ideas.FindAsync(id);
            return idea is null ? Results.NotFound() : Results.Ok(idea);
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            StatusUpdateRequest req,
            AppDbContext db,
            IBackgroundJobClient jobClient) =>
        {
            var idea = await db.Ideas.FindAsync(id);
            if (idea is null) return Results.NotFound();

            var allowed = new[] { IdeaStatus.Approved, IdeaStatus.Rejected };
            if (!allowed.Contains(req.Status))
                return Results.BadRequest($"Status must be one of: {string.Join(", ", allowed)}");

            idea.Status = req.Status;
            await db.SaveChangesAsync();

            if (req.Status == IdeaStatus.Approved)
            {
                var hasScript = await db.Scripts.AnyAsync(s =>
                    s.IdeaId == id && s.Status != ScriptStatus.Draft);

                if (!hasScript)
                    jobClient.Enqueue<ProcessIdeaJob>(j => j.ExecuteAsync(id, Guid.Empty, CancellationToken.None));
            }

            return Results.Ok(new { idea.Id, idea.Status });
        });
    }
}

public record StatusUpdateRequest(string Status);
