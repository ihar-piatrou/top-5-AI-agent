using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Infrastructure.MediaClients;

namespace Top5Agent.Pipeline;

public class HeyGenAvatarVideoService(
    HeyGenClient heyGenClient,
    AppDbContext db,
    IConfiguration config,
    ILogger<HeyGenAvatarVideoService> logger)
{
    public async Task SubmitAllAsync(Guid scriptId, CancellationToken ct = default)
    {
        var avatarId = config["HeyGen:AvatarId"]
            ?? throw new InvalidOperationException("HeyGen:AvatarId is not configured.");
        var voiceId = config["HeyGen:VoiceId"]
            ?? throw new InvalidOperationException("HeyGen:VoiceId is not configured.");

        var sections = await db.ScriptSections
            .Where(s => s.ScriptId == scriptId)
            .ToListAsync(ct);

        foreach (var section in sections)
        {
            var scriptText = SelectText(section);
            if (string.IsNullOrWhiteSpace(scriptText))
            {
                logger.LogWarning("Section {SectionId} (pos={Position}) has no usable text, skipping", section.Id, section.Position);
                continue;
            }

            // Idempotency: skip if already submitted
            var exists = await db.HeygenAvatarVideos
                .AnyAsync(v => v.ScriptSectionId == section.Id, ct);
            if (exists)
            {
                logger.LogInformation("Avatar video already submitted for section {SectionId}, skipping", section.Id);
                continue;
            }

            try
            {
                var videoId = await heyGenClient.CreateAvatarVideoAsync(avatarId, voiceId, scriptText, ct);

                db.HeygenAvatarVideos.Add(new HeygenAvatarVideo
                {
                    Id = Guid.NewGuid(),
                    ScriptSectionId = section.Id,
                    HeygenVideoId = videoId,
                    AvatarId = avatarId,
                    VoiceId = voiceId,
                    ScriptText = scriptText,
                    Status = HeygenVideoStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Submitted avatar video {VideoId} for section {SectionId} (pos={Position})",
                    videoId, section.Id, section.Position);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit avatar video for section {SectionId}", section.Id);
            }
        }
    }

    // Items 1–5 use Headline; Hook (0) and Outro (99) use Narration
    // Wrapped with a 1-second break at start and end for HeyGen avatar pacing
    private static string? SelectText(ScriptSection section)
    {
        var raw = section.Position is >= 1 and <= 5
            ? section.Headline
            : section.Narration;

        if (string.IsNullOrWhiteSpace(raw)) return null;
        return $"<break time=\"1s\" />{raw}<break time=\"1s\" />";
    }
}
