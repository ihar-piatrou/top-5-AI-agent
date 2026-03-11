namespace Top5Agent.Core.Models;

public class ScriptSection
{
    public Guid Id { get; set; }
    public Guid ScriptId { get; set; }
    public int Position { get; set; }
    public string? Title { get; set; }
    public string? Headline { get; set; }
    public string Narration { get; set; } = string.Empty;
    public string? MediaQuery { get; set; }  // semicolon-separated video queries
    public string? MediaType { get; set; } // photo|video
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Script Script { get; set; } = null!;
    public ICollection<MediaAsset> MediaAssets { get; set; } = [];
}
