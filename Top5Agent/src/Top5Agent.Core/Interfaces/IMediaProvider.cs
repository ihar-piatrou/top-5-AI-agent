using Top5Agent.Core.Models;

namespace Top5Agent.Core.Interfaces;

public interface IMediaProvider
{
    Task<List<MediaAsset>> SearchAndDownloadAsync(string query, MediaType type, string savePath, int maxResults, CancellationToken ct = default);
}
