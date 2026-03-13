using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Infrastructure.MediaClients;

namespace Top5Agent.Pipeline;

public class HeyGenPollingService(
    HeyGenClient heyGenClient,
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<HeyGenPollingService> logger)
{
    private const string MediaRoot = "media";

    public async Task PollAllAsync(CancellationToken ct = default)
    {
        var pending = await db.HeygenAvatarVideos
            .Include(v => v.ScriptSection)
                .ThenInclude(s => s.Script)
                    .ThenInclude(s => s.Idea)
            .Where(v => (v.Status == HeygenVideoStatus.Pending
                         || v.Status == HeygenVideoStatus.Waiting
                         || v.Status == HeygenVideoStatus.Processing)
                        && v.LocalPath == null)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            logger.LogDebug("No pending HeyGen avatar videos to poll");
            return;
        }

        logger.LogInformation("Polling {Count} pending HeyGen avatar video(s)", pending.Count);

        foreach (var video in pending)
        {
            try
            {
                var (status, videoUrl, error) = await heyGenClient.GetVideoStatusAsync(video.HeygenVideoId, ct);

                video.Status = status;

                if (status == HeygenVideoStatus.Completed && videoUrl is not null)
                {
                    var scriptFolder = MediaFileNaming.Sanitize(video.ScriptSection.Script.Idea.Title);
                    var scriptPath = Path.Combine(MediaRoot, scriptFolder);
                    var fileName = MediaFileNaming.AvatarFileName(video.ScriptSection.Position, video.ScriptSection.Title);

                    video.VideoUrl = videoUrl;
                    video.LocalPath = await DownloadFileAsync(videoUrl, scriptPath, fileName, ct);
                    video.Status = HeygenVideoStatus.Completed;
                    video.CompletedAt = DateTime.UtcNow;

                    await db.SaveChangesAsync(ct);

                    logger.LogInformation("Avatar video {VideoId} completed, saved to {Path}",
                        video.HeygenVideoId, video.LocalPath);
                }
                else if (status == HeygenVideoStatus.Failed)
                {
                    video.Status = HeygenVideoStatus.Failed;
                    video.ErrorMessage = error ?? "Unknown error";
                    logger.LogWarning("Avatar video {VideoId} failed: {Error}", video.HeygenVideoId, video.ErrorMessage);
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling HeyGen video {VideoId}", video.HeygenVideoId);
            }
        }

    }

    private async Task<string> DownloadFileAsync(string url, string savePath, string fileName, CancellationToken ct)
    {
        Directory.CreateDirectory(savePath);
        var filePath = Path.Combine(savePath, fileName);
        using var http = httpClientFactory.CreateClient();
        using var stream = await http.GetStreamAsync(url, ct);
        using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, ct);
        return filePath;
    }

}
