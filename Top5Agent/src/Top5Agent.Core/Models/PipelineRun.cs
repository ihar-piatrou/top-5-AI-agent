namespace Top5Agent.Core.Models;

public class PipelineRun
{
    public Guid Id { get; set; }
    public string? TriggerReason { get; set; }
    public string Status { get; set; } = PipelineRunStatus.Running;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public static class PipelineRunStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
