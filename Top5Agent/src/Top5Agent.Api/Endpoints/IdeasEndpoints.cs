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
        })
        .WithSummary("List ideas")
        .WithDescription("Returns all ideas ordered by creation date. Filter by status using the query parameter. Possible statuses: pending, approved, rejected, scripted.")
        .Produces<object>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var idea = await db.Ideas.FindAsync(id);
            return idea is null ? Results.NotFound() : Results.Ok(idea);
        })
        .WithSummary("Get idea by ID")
        .WithDescription("Returns a single idea with all fields including the embedding vector and topic metadata.")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

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
        })
        .WithSummary("Update idea status")
        .WithDescription("""
            Approve or reject an idea.
            Approving enqueues the full processing pipeline: script writing (Claude) → fact review (GPT-4o) → content polishing (Claude).
            Rejecting marks the idea as rejected with no further action.
            Allowed values: 'approved', 'rejected'.
            """)
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public record StatusUpdateRequest(string Status);
