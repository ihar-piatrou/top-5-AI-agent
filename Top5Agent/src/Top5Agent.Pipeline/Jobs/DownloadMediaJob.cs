using Hangfire;
using Microsoft.Extensions.Logging;

namespace Top5Agent.Pipeline.Jobs;

public class DownloadMediaJob(
    MediaAcquisitionService mediaAcquisition,
    IBackgroundJobClient jobClient,
    ILogger<DownloadMediaJob> logger)
{
    public async Task ExecuteAsync(Guid scriptId, Guid runId, bool useAvatarIvModel = false, CancellationToken ct = default)
    {
        logger.LogInformation("Downloading media for script {ScriptId}", scriptId);
        await mediaAcquisition.DownloadAllAsync(scriptId, runId, ct);
        logger.LogInformation("Media download complete for script {ScriptId}", scriptId);

        jobClient.Enqueue<GenerateHeyGenMediaJob>(j => j.ExecuteAsync(scriptId, useAvatarIvModel, CancellationToken.None));
        logger.LogInformation("Enqueued HeyGen media generation for script {ScriptId}", scriptId);
    }
}
