namespace Top5Agent.Pipeline;

internal static class MediaFileNaming
{
    // Position 0 (hook) → "00", 1–5 → "01"–"05", 99 (outro) → "06"
    public static string SectionPrefix(int position) => position switch
    {
        0  => "00",
        99 => "06",
        _  => position.ToString("D2")
    };

    public static string ShortName(string? title, int position)
    {
        var name = string.IsNullOrWhiteSpace(title) ? $"section_{position}" : title;
        var sanitized = Sanitize(name).Replace(' ', '_').ToLowerInvariant();
        if (sanitized.Length > 20)
            sanitized = sanitized[..20].TrimEnd('_');
        return sanitized;
    }

    // 01_video_01_{shortName}.mp4
    public static string VideoFileName(int position, int videoIndex, string? title)
        => $"{SectionPrefix(position)}_video_{videoIndex:D2}_{ShortName(title, position)}.mp4";

    // 01_avatar_{shortName}.mp4
    public static string AvatarFileName(int position, string? title)
        => $"{SectionPrefix(position)}_avatar_{ShortName(title, position)}.mp4";

    // 01_audio_{shortName}.mp3
    public static string AudioFileName(int position, string? title)
        => $"{SectionPrefix(position)}_audio_{ShortName(title, position)}.mp3";

    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return clean.Trim().TrimEnd('.');
    }
}
