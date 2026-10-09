namespace Beutl.Extensions.YtDlp.Models;

// yt-dlp reports every codec/protocol variant of each resolution. For a picker we only want one
// entry per resolution (and frame-rate class), choosing the codec that edits best.
public static class FormatCatalog
{
    public static IReadOnlyList<YtDlpFormat> Summarize(IEnumerable<YtDlpFormat> formats)
    {
        var list = formats.ToList();

        IEnumerable<YtDlpFormat> videos = list
            .Where(f => f.HasVideo)
            .GroupBy(f => (Height: f.Height ?? 0, HighFrameRate: IsHighFrameRate(f.Fps)))
            .Select(g => g.OrderBy(Rank).ThenByDescending(f => f.Tbr ?? 0).ThenByDescending(f => f.FileSize ?? 0).First())
            .OrderByDescending(f => f.Height ?? 0)
            .ThenByDescending(f => IsHighFrameRate(f.Fps));

        IEnumerable<YtDlpFormat> audios = list
            .Where(f => !f.HasVideo && f.HasAudio)
            .GroupBy(f => f.Ext ?? "")
            .Select(g => g.OrderByDescending(f => f.Tbr ?? 0).ThenByDescending(f => f.FileSize ?? 0).First())
            .OrderBy(f => AudioContainerRank(f.Ext))
            .ThenByDescending(f => f.Tbr ?? 0);

        return videos.Concat(audios).ToArray();
    }

    public static bool IsHighFrameRate(double? fps) => fps is >= 50;

    // Lower is better: H.264 decodes everywhere, then AV1/VP9, and mp4 containers over webm.
    private static (int Codec, int Container) Rank(YtDlpFormat format)
    {
        int codec = CodecFamily(format.VCodec) switch
        {
            "H.264" => 0,
            "AV1" => 1,
            "VP9" => 2,
            "H.265" => 3,
            _ => 4,
        };
        int container = string.Equals(format.Ext, "mp4", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        return (codec, container);
    }

    private static int AudioContainerRank(string? ext) => ext?.ToLowerInvariant() switch
    {
        "m4a" => 0,
        "mp3" => 1,
        "webm" or "opus" => 2,
        _ => 3,
    };

    // "avc1.640028" -> "H.264", "vp09.00.41.08" -> "VP9", "mp4a.40.2" -> "AAC".
    public static string CodecFamily(string? codec)
    {
        if (string.IsNullOrEmpty(codec) || codec == "none") return "";

        string lower = codec.ToLowerInvariant();
        if (lower.StartsWith("avc") || lower.StartsWith("h264")) return "H.264";
        if (lower.StartsWith("av01") || lower.StartsWith("av1")) return "AV1";
        if (lower.StartsWith("vp09") || lower.StartsWith("vp9")) return "VP9";
        if (lower.StartsWith("vp08") || lower.StartsWith("vp8")) return "VP8";
        if (lower.StartsWith("hev1") || lower.StartsWith("hvc1") || lower.StartsWith("h265") || lower.StartsWith("hevc")) return "H.265";
        if (lower.StartsWith("mp4a")) return "AAC";
        if (lower.StartsWith("opus")) return "Opus";
        if (lower.StartsWith("mp3")) return "MP3";
        if (lower.StartsWith("vorbis")) return "Vorbis";
        if (lower.StartsWith("flac")) return "FLAC";
        if (lower.StartsWith("ac-3") || lower.StartsWith("ac3")) return "AC3";
        if (lower.StartsWith("ec-3") || lower.StartsWith("eac3")) return "E-AC3";

        int dot = codec.IndexOf('.');
        return dot > 0 ? codec[..dot] : codec;
    }
}
