using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class MediaAcquisitionService(
    IMediaProvider mediaProvider,
    AppDbContext db,
    ILogger<MediaAcquisitionService> logger)
{
    private const string MediaRoot = "media";
    private const int MaxPhotos = 4;
    private const int MaxVideos = 3;

    public async Task DownloadAllAsync(Guid scriptId, Guid runId, CancellationToken ct = default)
    {
        var script = await db.Scripts
            .Include(s => s.Idea)
            .FirstOrDefaultAsync(s => s.Id == scriptId, ct);

        if (script is null)
        {
            logger.LogError("Script {ScriptId} not found", scriptId);
            return;
        }

        var sections = await db.ScriptSections
            .Where(s => s.ScriptId == scriptId && s.MediaQuery != null)
            .ToListAsync(ct);

        logger.LogInformation("Downloading media for script {ScriptId}: {Count} sections", scriptId, sections.Count);

        var scriptFolder = SanitizePath(script.Idea.Title);

        foreach (var section in sections)
        {
            if (section.MediaQuery is null) continue;

            var sectionFolder = SanitizePath(section.Title ?? section.Position.ToString());
            var savePath = Path.Combine(MediaRoot, scriptFolder, sectionFolder);
            var mediaType = section.MediaType?.ToLowerInvariant() == "video" ? MediaType.Video : MediaType.Photo;
            var maxResults = mediaType == MediaType.Video ? MaxVideos : MaxPhotos;

            try
            {
                var existingUrls = (await db.MediaAssets
                    .Where(m => m.ScriptSectionId == section.Id)
                    .Select(m => m.RemoteUrl)
                    .ToListAsync(ct))
                    .ToHashSet();

                var assets = await mediaProvider.SearchAndDownloadAsync(section.MediaQuery, mediaType, savePath, maxResults, ct);

                if (assets.Count == 0)
                {
                    logger.LogWarning("No media found for section '{Title}' query: {Query}", section.Title, section.MediaQuery);
                    continue;
                }

                var newAssets = assets.Where(a => !existingUrls.Contains(a.RemoteUrl)).ToList();

                if (newAssets.Count == 0)
                {
                    logger.LogInformation("All {Count} media result(s) already stored for section '{Title}', skipping",
                        assets.Count, section.Title);
                    continue;
                }

                foreach (var asset in newAssets)
                {
                    asset.Id = Guid.NewGuid();
                    asset.ScriptSectionId = section.Id;
                    asset.CreatedAt = DateTime.UtcNow;
                    db.MediaAssets.Add(asset);
                }

                await db.SaveChangesAsync(ct);

                logger.LogInformation("Downloaded {New} new {Type}(s) for section '{Title}' ({Skipped} duplicate(s) skipped)",
                    newAssets.Count, mediaType, section.Title, assets.Count - newAssets.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download media for section '{Title}'", section.Title);
            }
        }
    }

    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return clean.Trim().TrimEnd('.');
    }
}
