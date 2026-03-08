using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Top5Agent.Pipeline.Jobs;

public class GenerateIdeasJob(
    PipelineOrchestrator orchestrator,
    IConfiguration configuration,
    ILogger<GenerateIdeasJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var niches = configuration.GetSection("Pipeline:Niches").Get<string[]>()
          ?? [
                "survival",
                "danger and safety",
                "travel and cities",
                "animals and wildlife",
                "health and diseases",
                "food and restaurants",
                "technology and gadgets",
                "cars and mechanics",
                "outdoor and hiking",
                "home improvement",
                "history and mysteries",
                "science and discoveries",
                "strange facts",
                "crime and prisons",
                "luxury and expensive things",
                "nature and disasters",
                "psychology and human behavior",
                "extreme places",
                "life hacks and tricks",
                "everyday mistakes"
            ];

        // Pick 10 niches starting from today's rotation offset so every day covers a different window
        const int ideasPerRun = 10;
        var startIndex = DateTime.UtcNow.DayOfYear % niches.Length;
        var selectedNiches = Enumerable.Range(0, ideasPerRun)
            .Select(i => niches[(startIndex + i) % niches.Length])
            .ToArray();

        logger.LogInformation("Daily idea generation triggered for {Count} niches: {Niches}",
            selectedNiches.Length, string.Join(", ", selectedNiches));

        foreach (var niche in selectedNiches)
        {
            logger.LogInformation("Generating idea for niche: {Niche}", niche);
            await orchestrator.RunAsync(niche, count: 1, autoApprove: false, ct);
        }
    }
}
