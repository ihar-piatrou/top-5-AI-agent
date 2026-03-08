namespace Top5Agent.Core.Models;

public class Source
{
    public Guid Id { get; set; }
    public Guid ScriptId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool Verified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Script Script { get; set; } = null!;
}
