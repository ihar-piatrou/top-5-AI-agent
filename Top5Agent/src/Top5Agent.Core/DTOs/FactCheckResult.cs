using System.Text.Json.Serialization;

namespace Top5Agent.Core.DTOs;

public class FactCheckResult
{
    [JsonPropertyName("claim")]
    public string Claim { get; set; } = string.Empty;

    [JsonPropertyName("verdict")]
    public string Verdict { get; set; } = string.Empty; // supported|unsupported|uncertain

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("source_title")]
    public string? SourceTitle { get; set; }

    [JsonPropertyName("rewrite")]
    public string? Rewrite { get; set; }
}
