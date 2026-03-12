using Microsoft.Extensions.Logging;

namespace Top5Agent.Pipeline.Jobs;

public class GenerateHeyGenMediaJob(
    HeyGenAvatarVideoService avatarVideoService,
    HeyGenAudioService audioService,
    ILogger<GenerateHeyGenMediaJob> logger)
{
    public async Task ExecuteAsync(Guid scriptId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting HeyGen media generation for script {ScriptId}", scriptId);

        await avatarVideoService.SubmitAllAsync(scriptId, ct);
        await audioService.GenerateAllAsync(scriptId, ct);

        logger.LogInformation("HeyGen media generation complete for script {ScriptId}", scriptId);
    }
}
