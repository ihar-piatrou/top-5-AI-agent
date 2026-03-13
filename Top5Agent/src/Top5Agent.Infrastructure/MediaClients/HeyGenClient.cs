using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Top5Agent.Infrastructure.MediaClients;

public class HeyGenClient(HttpClient httpClient, ILogger<HeyGenClient> logger)
{
    public async Task<string> CreateAvatarVideoAsync(
        string avatarId, string voiceId, string text, bool useAvatarIvModel = false, CancellationToken ct = default)
    {
        logger.LogDebug("Submitting HeyGen avatar video, avatar={AvatarId}, useIv={UseIv}, chars={Len}", avatarId, useAvatarIvModel, text.Length);

        var body = new HeyGenVideoRequest
        {
            VideoInputs =
            [
                new HeyGenVideoInput
                {
                    Character = new HeyGenCharacter { AvatarId = avatarId, UseAvatarIvModel = useAvatarIvModel },
                    Voice = new HeyGenVoice { VoiceId = voiceId, InputText = text }
                }
            ]
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("v2/video/generate", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("HeyGen video generate {Status}: {Body}", (int)response.StatusCode, errorBody);
            throw new InvalidOperationException($"HeyGen video generate {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<HeyGenVideoResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from HeyGen video generate.");

        if (result.Error is not null)
            throw new InvalidOperationException($"HeyGen error: {result.Error}");

        return result.Data?.VideoId ?? throw new InvalidOperationException("HeyGen returned no video_id.");
    }

    public async Task<(string status, string? videoUrl, string? error)> GetVideoStatusAsync(
        string videoId, CancellationToken ct = default)
    {
        logger.LogDebug("Polling HeyGen video status for {VideoId}", videoId);

        var response = await httpClient.GetAsync($"v1/video_status.get?video_id={videoId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HeyGenStatusResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from HeyGen video status.");

        var status = result.Data?.Status ?? "pending";
        var url = result.Data?.VideoUrl;
        var error = result.Data?.Error;

        return (status, url, error);
    }

    public async Task<string> TextToSpeechAsync(string voiceId, string text, CancellationToken ct = default)
    {
        logger.LogDebug("Calling HeyGen TTS, voice={VoiceId}, chars={Len}", voiceId, text.Length);

        var body = new HeyGenTtsRequest { VoiceId = voiceId, Text = text };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("v1/audio/text_to_speech", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("HeyGen TTS {Status}: {Body}", (int)response.StatusCode, errorBody);
            throw new InvalidOperationException($"HeyGen TTS {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<HeyGenTtsResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from HeyGen TTS.");

        if (result.Error is not null)
            throw new InvalidOperationException($"HeyGen TTS error: {result.Error}");

        return result.Data?.AudioUrl ?? throw new InvalidOperationException("HeyGen TTS returned no audio_url.");
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    private sealed class HeyGenVideoRequest
    {
        [JsonPropertyName("video_inputs")] public HeyGenVideoInput[] VideoInputs { get; set; } = [];
    }

    private sealed class HeyGenVideoInput
    {
        [JsonPropertyName("character")] public HeyGenCharacter Character { get; set; } = new();
        [JsonPropertyName("voice")]     public HeyGenVoice Voice { get; set; } = new();
    }

    private sealed class HeyGenCharacter
    {
        [JsonPropertyName("type")]                public string Type { get; set; } = "avatar";
        [JsonPropertyName("avatar_id")]           public string AvatarId { get; set; } = string.Empty;
        [JsonPropertyName("use_avatar_iv_model")] public bool UseAvatarIvModel { get; set; }
    }

    private sealed class HeyGenVoice
    {
        [JsonPropertyName("type")]       public string Type { get; set; } = "text";
        [JsonPropertyName("voice_id")]   public string VoiceId { get; set; } = string.Empty;
        [JsonPropertyName("input_text")] public string InputText { get; set; } = string.Empty;
    }


    private sealed class HeyGenTtsRequest
    {
        [JsonPropertyName("voice_id")] public string VoiceId { get; set; } = string.Empty;
        [JsonPropertyName("text")]     public string Text { get; set; } = string.Empty;
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed class HeyGenVideoResponse
    {
        [JsonPropertyName("error")] public object? Error { get; set; }
        [JsonPropertyName("data")]  public HeyGenVideoData? Data { get; set; }
    }

    private sealed class HeyGenVideoData
    {
        [JsonPropertyName("video_id")] public string? VideoId { get; set; }
    }

    private sealed class HeyGenStatusResponse
    {
        [JsonPropertyName("data")] public HeyGenStatusData? Data { get; set; }
    }

    private sealed class HeyGenStatusData
    {
        [JsonPropertyName("status")]    public string Status { get; set; } = string.Empty;
        [JsonPropertyName("video_url")] public string? VideoUrl { get; set; }
        [JsonPropertyName("error")]     public string? Error { get; set; }
    }

    private sealed class HeyGenTtsResponse
    {
        [JsonPropertyName("error")] public object? Error { get; set; }
        [JsonPropertyName("data")]  public HeyGenTtsData? Data { get; set; }
    }

    private sealed class HeyGenTtsData
    {
        [JsonPropertyName("audio_url")] public string? AudioUrl { get; set; }
    }
}
