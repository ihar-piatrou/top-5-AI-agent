namespace Top5Agent.Core.Models;

public class MediaAsset
{
    public Guid Id { get; set; }
    public Guid ScriptSectionId { get; set; }
    public string? PexelsId { get; set; }
    public string AssetType { get; set; } = string.Empty; // photo|video
    public string RemoteUrl { get; set; } = string.Empty;
    public string? LocalPath { get; set; }
    public string? Attribution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ScriptSection ScriptSection { get; set; } = null!;
}

