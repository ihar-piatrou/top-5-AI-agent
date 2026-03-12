using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;

namespace Top5Agent.Infrastructure.MediaClients;

public class PexelsMediaProvider(HttpClient httpClient, ILogger<PexelsMediaProvider> logger) : IMediaProvider
{
    public Task<List<MediaAsset>> SearchAndDownloadAsync(string query, string savePath, int maxResults, ISet<string> existingUrls, CancellationToken ct = default)
        => SearchAndDownloadVideosAsync(query, savePath, maxResults, existingUrls, ct);

    private async Task<List<MediaAsset>> SearchAndDownloadVideosAsync(string query, string savePath, int maxResults, ISet<string> existingUrls, CancellationToken ct)
    {
        logger.LogDebug("Searching Pexels videos for: {Query} (max {Max})", query, maxResults);

        var response = await httpClient.GetFromJsonAsync<PexelsVideoResponse>(
            $"videos/search?query={Uri.EscapeDataString(query)}&per_page={maxResults}&orientation=landscape", ct);

        if (response?.Videos is null or [])
        {
            logger.LogWarning("No Pexels videos found for query: {Query}", query);
            return [];
        }

        var assets = new List<MediaAsset>();
        var videoDir = Path.Combine(savePath, "video");

        for (var i = 0; i < response.Videos.Count(); i++)
        {
            var video = response.Videos[i];
            var videoFile = video.VideoFiles.FirstOrDefault(f => f.Quality == "hd")
                ?? video.VideoFiles.FirstOrDefault();

            if (videoFile is null) continue;
            if (existingUrls.Contains(videoFile.Link)) continue;

            var localPath = await DownloadFileAsync(videoFile.Link, videoDir, $"{Guid.NewGuid()}.mp4", ct);

            assets.Add(new MediaAsset
            {
                PexelsId = video.Id.ToString(),
                AssetType = "video",
                RemoteUrl = videoFile.Link,
                LocalPath = localPath,
                Attribution = $"Video by {video.User.Name} on Pexels"
            });

            if (i + 1 >= maxResults)
                break;
        }

        return assets;
    }

    private async Task<string> DownloadFileAsync(string url, string savePath, string fileName, CancellationToken ct)
    {
        Directory.CreateDirectory(savePath);
        var filePath = Path.Combine(savePath, fileName);

        using var stream = await httpClient.GetStreamAsync(url, ct);
        using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, ct);

        logger.LogDebug("Downloaded media to {Path}", filePath);
        return filePath;
    }
}
