using Microsoft.Extensions.Logging;

namespace Top5Agent.Pipeline.Jobs;

public class PollHeyGenJobsJob(
    HeyGenPollingService pollingService,
    ILogger<PollHeyGenJobsJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        logger.LogDebug("Running HeyGen polling job");
        await pollingService.PollAllAsync(ct);
    }
}
