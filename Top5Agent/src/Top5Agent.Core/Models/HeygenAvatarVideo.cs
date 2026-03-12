namespace Top5Agent.Core.Models;

public class HeygenAvatarVideo
{
    public Guid Id { get; set; }
    public Guid ScriptSectionId { get; set; }
    public string HeygenVideoId { get; set; } = string.Empty;
    public string AvatarId { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string ScriptText { get; set; } = string.Empty;
    public string Status { get; set; } = HeygenVideoStatus.Pending;
    public string? VideoUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ScriptSection ScriptSection { get; set; } = null!;
}

public static class HeygenVideoStatus
{
    public const string Pending = "pending";
    public const string Waiting = "waiting";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
