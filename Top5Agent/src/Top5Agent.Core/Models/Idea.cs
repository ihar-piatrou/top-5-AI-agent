namespace Top5Agent.Core.Models;

public class Idea
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Niche { get; set; }
    public string? Summary { get; set; }
    public string? Embedding { get; set; } // JSON float[] from text-embedding-3-small
    public string Status { get; set; } = IdeaStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Script> Scripts { get; set; } = [];
}

public static class IdeaStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Scripted = "scripted";
}
