using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Interfaces;
using Top5Agent.Infrastructure.Data;

namespace Top5Agent.Pipeline;

public class MediaAcquisitionService(
    IMediaProvider mediaProvider,
    AppDbContext db,
    ILogger<MediaAcquisitionService> logger)
{
    private const string MediaRoot = "media";

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

        var scriptFolder = MediaFileNaming.Sanitize(script.Idea.Title);
        var scriptPath = Path.Combine(MediaRoot, scriptFolder);
        Directory.CreateDirectory(Path.Combine(scriptPath, "result"));

        // Seed with every URL already downloaded for any section of this script
        // so the same video is never reused across sections.
        var sectionIds = sections.Select(s => s.Id).ToList();
        var existingUrls = (await db.MediaAssets
            .Where(m => sectionIds.Contains(m.ScriptSectionId))
            .Select(m => m.RemoteUrl)
            .ToListAsync(ct))
            .ToHashSet();

        foreach (var section in sections)
        {
            if (section.MediaQuery is null) continue;

            var queries = section.MediaQuery
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (queries.Length == 0) continue;

            // Start counter after already-saved assets so retries produce unique filenames
            var videoCounter = await db.MediaAssets
                .CountAsync(m => m.ScriptSectionId == section.Id, ct);

            var totalNew = 0;

            foreach (var query in queries)
            {
                try
                {
                    var assets = await mediaProvider.SearchAndDownloadAsync(query, scriptPath, 2, existingUrls, ct);

                    if (assets.Count == 0)
                    {
                        logger.LogWarning("No video found for section '{Title}' query: {Query}", section.Title, query);
                        continue;
                    }

                    foreach (var asset in assets)
                    {
                        videoCounter++;
                        var newFileName = MediaFileNaming.VideoFileName(section.Position, videoCounter, section.Title);
                        var newPath = Path.Combine(scriptPath, newFileName);

                        if (asset.LocalPath != null && File.Exists(asset.LocalPath) && asset.LocalPath != newPath)
                        {
                            File.Move(asset.LocalPath, newPath);
                            asset.LocalPath = newPath;
                        }

                        asset.Id = Guid.NewGuid();
                        asset.ScriptSectionId = section.Id;
                        asset.CreatedAt = DateTime.UtcNow;
                        db.MediaAssets.Add(asset);
                        existingUrls.Add(asset.RemoteUrl);
                        totalNew++;
                    }

                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to download video for section '{Title}' query: {Query}", section.Title, query);
                }
            }

            logger.LogInformation("Downloaded {New} video(s) for section '{Title}'", totalNew, section.Title);
        }
    }
}
