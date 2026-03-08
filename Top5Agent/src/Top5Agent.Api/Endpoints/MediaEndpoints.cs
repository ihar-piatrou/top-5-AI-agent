using Microsoft.EntityFrameworkCore;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Api.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media").WithTags("Media");

        group.MapGet("/{scriptId:guid}", async (Guid scriptId, AppDbContext db) =>
        {
            var assets = await db.MediaAssets
                .Where(m => m.ScriptSection.ScriptId == scriptId)
                .Select(m => new
                {
                    m.Id,
                    m.ScriptSectionId,
                    m.PexelsId,
                    m.AssetType,
                    m.RemoteUrl,
                    m.LocalPath,
                    m.Attribution,
                    m.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(assets);
        });
    }
}
