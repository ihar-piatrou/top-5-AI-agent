using Hangfire;
using Microsoft.EntityFrameworkCore;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Pipeline.Jobs;

namespace Top5Agent.Api.Endpoints;

public static class HeyGenEndpoints
{
    public static void MapHeyGenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/heygen").WithTags("HeyGen");

        group.MapGet("/{scriptId:guid}", async (Guid scriptId, AppDbContext db) =>
        {
            var avatarVideos = await db.HeygenAvatarVideos
                .Where(v => v.ScriptSection.ScriptId == scriptId)
                .Select(v => new
                {
                    v.Id,
                    v.ScriptSectionId,
                    SectionPosition = v.ScriptSection.Position,
                    SectionTitle = v.ScriptSection.Title,
                    v.HeygenVideoId,
                    v.Status,
                    v.VideoUrl,
                    v.LocalPath,
                    v.ErrorMessage,
                    v.CreatedAt,
                    v.CompletedAt
                })
                .OrderBy(v => v.SectionPosition)
                .ToListAsync();

            var audioFiles = await db.HeygenAudioFiles
                .Where(a => a.ScriptSection.ScriptId == scriptId)
                .Select(a => new
                {
                    a.Id,
                    a.ScriptSectionId,
                    SectionPosition = a.ScriptSection.Position,
                    SectionTitle = a.ScriptSection.Title,
                    a.Status,
                    a.AudioUrl,
                    a.LocalPath,
                    a.ErrorMessage,
                    a.CreatedAt
                })
                .OrderBy(a => a.SectionPosition)
                .ToListAsync();

            return Results.Ok(new { avatarVideos, audioFiles });
        })
        .WithSummary("Get HeyGen generation status for a script")
        .WithDescription("""
            Returns all HeyGen avatar videos and TTS audio files for the given script.
            Avatar videos are created for: Hook (Narration), Items 1–5 (Headline), Outro (Narration).
            Audio files are created for Items 1–5 (Narration). TTS audio is synchronous so audio status
            is immediately 'completed' or 'failed'. Avatar video status starts as 'pending' and transitions
            to 'processing' → 'completed' as the polling job runs every 2 minutes.
            """)
        .Produces<object>(StatusCodes.Status200OK);

        group.MapPost("/poll", (IBackgroundJobClient jobClient) =>
        {
            var jobId = jobClient.Enqueue<PollHeyGenJobsJob>(j => j.ExecuteAsync(CancellationToken.None));
            return Results.Ok(new { JobId = jobId });
        })
        .WithSummary("Manually trigger HeyGen video polling")
        .WithDescription("Enqueues a one-off poll of all pending/processing HeyGen avatar videos. Useful for testing without waiting for the 2-minute recurring job.")
        .Produces<object>(StatusCodes.Status200OK);

        group.MapPost("/{scriptId:guid}/generate", (Guid scriptId, IBackgroundJobClient jobClient) =>
        {
            var jobId = jobClient.Enqueue<GenerateHeyGenMediaJob>(j => j.ExecuteAsync(scriptId, CancellationToken.None));
            return Results.Ok(new { JobId = jobId });
        })
        .WithSummary("Manually trigger HeyGen media generation for a script")
        .WithDescription("Enqueues avatar video submission and TTS audio generation for all sections of the given script. Use this to re-trigger generation after a failure or to test without going through the full media download step.")
        .Produces<object>(StatusCodes.Status200OK);
    }
}
