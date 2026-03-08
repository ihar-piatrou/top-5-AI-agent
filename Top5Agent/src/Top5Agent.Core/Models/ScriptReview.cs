namespace Top5Agent.Core.Models;

public class ScriptReview
{
    public Guid Id { get; set; }
    public Guid ScriptId { get; set; }
    public string Reviewer { get; set; } = string.Empty; // 'gpt-4o' | 'claude-sonnet-4-6'
    public string? ReviewText { get; set; }
    public string? IssuesFound { get; set; } // JSON array of issue strings
    public bool Approved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Script Script { get; set; } = null!;
}
