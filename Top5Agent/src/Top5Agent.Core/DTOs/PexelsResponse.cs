using System.Text.Json.Serialization;

namespace Top5Agent.Core.DTOs;

public class PexelsPhotoResponse
{
    [JsonPropertyName("photos")]
    public PexelsPhoto[] Photos { get; set; } = [];
}

public class PexelsPhoto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("photographer")]
    public string Photographer { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public PexelsPhotoSrc Src { get; set; } = new();
}

public class PexelsPhotoSrc
{
    [JsonPropertyName("original")]
    public string Original { get; set; } = string.Empty;

    [JsonPropertyName("large")]
    public string Large { get; set; } = string.Empty;
}

public class PexelsVideoResponse
{
    [JsonPropertyName("videos")]
    public PexelsVideo[] Videos { get; set; } = [];
}

public class PexelsVideo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user")]
    public PexelsUser User { get; set; } = new();

    [JsonPropertyName("video_files")]
    public PexelsVideoFile[] VideoFiles { get; set; } = [];
}

public class PexelsUser
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class PexelsVideoFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}
