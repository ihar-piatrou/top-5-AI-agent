using System.Text.Json.Serialization;

namespace Top5Agent.Core.DTOs;

public class ScriptJson
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("hook")]
    public string Hook { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public ScriptJsonItem[] Items { get; set; } = [];

    [JsonPropertyName("outro")]
    public string Outro { get; set; } = string.Empty;
}

public class ScriptJsonItem
{
    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("narration")]
    public string Narration { get; set; } = string.Empty;

    [JsonPropertyName("verify_claims")]
    public string[] VerifyClaims { get; set; } = [];

    [JsonPropertyName("media")]
    public ScriptJsonMedia[] Media { get; set; } = [];
}

public class ScriptJsonMedia
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // photo|video

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; }
}
