using System.Text.Json.Serialization;

namespace Top5Agent.Core.DTOs;

public class IdeaJson
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("niche")]
    public string Niche { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("topicCategory")]
    public string TopicCategory { get; set; } = string.Empty;

    [JsonPropertyName("whyItWorks")]
    public string WhyItWorks { get; set; } = string.Empty;
}
