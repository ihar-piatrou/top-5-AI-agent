using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.DTOs;
using Top5Agent.Core.Interfaces;
using Top5Agent.Core.Models;

namespace Top5Agent.Infrastructure.MediaClients;

public class PexelsMediaProvider(HttpClient httpClient, ILogger<PexelsMediaProvider> logger) : IMediaProvider
{
    public async Task<List<MediaAsset>> SearchAndDownloadAsync(string query, MediaType type, string savePath, int maxResults, CancellationToken ct = default)
    {
        return type == MediaType.Photo
            ? await SearchAndDownloadPhotosAsync(query, savePath, maxResults, ct)
            : await SearchAndDownloadVideosAsync(query, savePath, maxResults, ct);
    }

    private async Task<List<MediaAsset>> SearchAndDownloadPhotosAsync(string query, string savePath, int maxResults, CancellationToken ct)
    {
        logger.LogDebug("Searching Pexels photos for: {Query} (max {Max})", query, maxResults);

        var response = await httpClient.GetFromJsonAsync<PexelsPhotoResponse>(
            $"v1/search?query={Uri.EscapeDataString(query)}&per_page={maxResults}", ct);

        if (response?.Photos is null or [])
        {
            logger.LogWarning("No Pexels photos found for query: {Query}", query);
            return [];
        }

        var assets = new List<MediaAsset>();
        var photoDir = Path.Combine(savePath, "photo");

        for (var i = 0; i < response.Photos.Count(); i++)
        {
            var photo = response.Photos[i];
            var fileUrl = photo.Src.Large;
            var localPath = await DownloadFileAsync(fileUrl, photoDir, $"photo_{i + 1}.jpg", ct);

            assets.Add(new MediaAsset
            {
                PexelsId = photo.Id.ToString(),
                AssetType = "photo",
                RemoteUrl = fileUrl,
                LocalPath = localPath,
                Attribution = $"Photo by {photo.Photographer} on Pexels"
            });

            if (i >= 3) // max 4 pictures if exist
                break;
        }

        return assets;
    }

    private async Task<List<MediaAsset>> SearchAndDownloadVideosAsync(string query, string savePath, int maxResults, CancellationToken ct)
    {
        logger.LogDebug("Searching Pexels videos for: {Query} (max {Max})", query, maxResults);

        var response = await httpClient.GetFromJsonAsync<PexelsVideoResponse>(
            $"videos/search?query={Uri.EscapeDataString(query)}&per_page={maxResults}", ct);

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

            var localPath = await DownloadFileAsync(videoFile.Link, videoDir, $"video_{i + 1}.mp4", ct);

            assets.Add(new MediaAsset
            {
                PexelsId = video.Id.ToString(),
                AssetType = "video",
                RemoteUrl = videoFile.Link,
                LocalPath = localPath,
                Attribution = $"Video by {video.User.Name} on Pexels"
            });

            if (i >= 2) // max 3 video if exist
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
