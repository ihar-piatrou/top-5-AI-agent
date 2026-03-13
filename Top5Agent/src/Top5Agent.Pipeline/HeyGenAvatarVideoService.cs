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
    public async Task SubmitAllAsync(Guid scriptId, bool useAvatarIvModel = false, CancellationToken ct = default)
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
                var videoId = await heyGenClient.CreateAvatarVideoAsync(avatarId, voiceId, scriptText, useAvatarIvModel, ct);

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

    // Items 1–5 use Headline prefixed with "Number N."; Hook (0) and Outro (99) use Narration
    // Wrapped with a 0.5-second break at start and end for HeyGen avatar pacing
    private static string? SelectText(ScriptSection section)
    {
        string? raw;
        if (section.Position is >= 1 and <= 5)
        {
            var numberWord = section.Position switch
            {
                1 => "One",
                2 => "Two",
                3 => "Three",
                4 => "Four",
                5 => "Five",
                _ => section.Position.ToString()
            };
            raw = string.IsNullOrWhiteSpace(section.Headline)
                ? null
                : $"Number {numberWord}. {section.Headline}";
        }
        else
        {
            raw = section.Narration;
        }

        if (string.IsNullOrWhiteSpace(raw)) return null;
        return $"<break time=\"0.5s\" />{raw}<break time=\"0.5s\" />";
    }
}
