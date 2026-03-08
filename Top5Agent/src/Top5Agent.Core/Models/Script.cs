namespace Top5Agent.Core.Models;

public class Script
{
    public Guid Id { get; set; }
    public Guid IdeaId { get; set; }
    public string JsonContent { get; set; } = string.Empty; // full ScriptJson stored as JSON
    public string? RawText { get; set; }
    public string Status { get; set; } = ScriptStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Idea Idea { get; set; } = null!;
    public ICollection<ScriptSection> Sections { get; set; } = [];
    public ICollection<ScriptReview> Reviews { get; set; } = [];
    public ICollection<Source> Sources { get; set; } = [];
}

public static class ScriptStatus
{
    public const string Draft = "draft";
    public const string Reviewed = "reviewed";
    public const string Polished = "polished";
    public const string Approved = "approved";
    public const string NeedsReview = "needs_review";
}
