using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Top5Agent.Core.Models;
using Top5Agent.Infrastructure.Data;
using Top5Agent.Infrastructure.MediaClients;

namespace Top5Agent.Pipeline;

public class HeyGenAudioService(
    HeyGenClient heyGenClient,
    AppDbContext db,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<HeyGenAudioService> logger)
{
    private const string MediaRoot = "media";

    public async Task GenerateAllAsync(Guid scriptId, CancellationToken ct = default)
    {
        var voiceId = config["HeyGen:VoiceId"]
            ?? throw new InvalidOperationException("HeyGen:VoiceId is not configured.");

        var script = await db.Scripts
            .Include(s => s.Idea)
            .FirstOrDefaultAsync(s => s.Id == scriptId, ct);

        if (script is null)
        {
            logger.LogError("Script {ScriptId} not found", scriptId);
            return;
        }

        var sections = await db.ScriptSections
            .Where(s => s.ScriptId == scriptId && s.Position >= 1 && s.Position <= 5)
            .ToListAsync(ct);

        var scriptFolder = MediaFileNaming.Sanitize(script.Idea.Title);
        var scriptPath = Path.Combine(MediaRoot, scriptFolder);

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section.Narration)) continue;

            // Idempotency: skip if already generated
            var exists = await db.HeygenAudioFiles
                .AnyAsync(a => a.ScriptSectionId == section.Id, ct);
            if (exists)
            {
                logger.LogInformation("Audio already generated for section {SectionId}, skipping", section.Id);
                continue;
            }

            var audioFile = new HeygenAudioFile
            {
                Id = Guid.NewGuid(),
                ScriptSectionId = section.Id,
                VoiceId = voiceId,
                ScriptText = section.Narration,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var audioUrl = await heyGenClient.TextToSpeechAsync(voiceId, section.Narration, ct);

                var fileName = MediaFileNaming.AudioFileName(section.Position, section.Title);
                var localPath = await DownloadFileAsync(audioUrl, scriptPath, fileName, ct);

                audioFile.AudioUrl = audioUrl;
                audioFile.LocalPath = localPath;
                audioFile.Status = HeygenAudioStatus.Completed;

                logger.LogInformation("TTS audio saved to {Path} for section {SectionId}", localPath, section.Id);
            }
            catch (Exception ex)
            {
                audioFile.Status = HeygenAudioStatus.Failed;
                audioFile.ErrorMessage = ex.Message;
                logger.LogError(ex, "Failed to generate TTS audio for section {SectionId}", section.Id);
            }

            db.HeygenAudioFiles.Add(audioFile);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<string> DownloadFileAsync(string url, string savePath, string fileName, CancellationToken ct)
    {
        Directory.CreateDirectory(savePath);
        var filePath = Path.Combine(savePath, fileName);
        using var http = httpClientFactory.CreateClient();
        using var stream = await http.GetStreamAsync(url, ct);
        using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, ct);
        return filePath;
    }

}
