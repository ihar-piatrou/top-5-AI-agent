using Microsoft.Extensions.Logging;

namespace Top5Agent.Pipeline.Jobs;

public class DownloadMediaJob(
    MediaAcquisitionService mediaAcquisition,
    ILogger<DownloadMediaJob> logger)
{
    public async Task ExecuteAsync(Guid scriptId, Guid runId, CancellationToken ct = default)
    {
        logger.LogInformation("Downloading media for script {ScriptId}", scriptId);
        await mediaAcquisition.DownloadAllAsync(scriptId, runId, ct);
        logger.LogInformation("Media download complete for script {ScriptId}", scriptId);
    }
}
