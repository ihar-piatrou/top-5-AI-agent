using Top5Agent.Core.Models;

namespace Top5Agent.Core.Interfaces;

public interface IMediaProvider
{
    Task<List<MediaAsset>> SearchAndDownloadAsync(string query, string savePath, int maxResults, ISet<string> existingUrls, CancellationToken ct = default);
}
