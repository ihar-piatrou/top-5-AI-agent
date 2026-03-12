namespace Top5Agent.Core.Models;

public class HeygenAudioFile
{
    public Guid Id { get; set; }
    public Guid ScriptSectionId { get; set; }
    public string VoiceId { get; set; } = string.Empty;
    public string ScriptText { get; set; } = string.Empty;
    public string Status { get; set; } = HeygenAudioStatus.Completed;
    public string? AudioUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ScriptSection ScriptSection { get; set; } = null!;
}

public static class HeygenAudioStatus
{
    public const string Completed = "completed";
    public const string Failed = "failed";
}
