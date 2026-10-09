using System.Text.Json;
using static Beutl.Extensions.YtDlp.Models.JsonElementHelpers;

namespace Beutl.Extensions.YtDlp.Models;

public sealed class YtDlpVideoInfo
{
    public string? Id { get; init; }

    public string? Title { get; init; }

    public string? Uploader { get; init; }

    public string? WebpageUrl { get; init; }

    public string? Extractor { get; init; }

    public string? Thumbnail { get; init; }

    public TimeSpan? Duration { get; init; }

    public bool IsLive { get; init; }

    public bool IsPlaylist { get; init; }

    public int? PlaylistCount { get; init; }

    public IReadOnlyList<YtDlpFormat> Formats { get; init; } = [];

    public string DurationText => Duration is { } d ? FormatDuration(d) : (IsLive ? "ライブ配信" : "");

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    // Parses the output of `yt-dlp --dump-single-json`.
    public static YtDlpVideoInfo Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string? type = GetString(root, "_type");
        bool isPlaylist = type is "playlist" or "multi_video";

        var formats = new List<YtDlpFormat>();
        if (root.TryGetProperty("formats", out JsonElement formatsElement) && formatsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement f in formatsElement.EnumerateArray())
            {
                string? formatId = GetString(f, "format_id");
                if (string.IsNullOrEmpty(formatId)) continue;

                formats.Add(new YtDlpFormat
                {
                    FormatId = formatId,
                    Ext = GetString(f, "ext"),
                    Resolution = GetString(f, "resolution"),
                    Width = GetInt32(f, "width"),
                    Height = GetInt32(f, "height"),
                    Fps = GetDouble(f, "fps"),
                    VCodec = GetString(f, "vcodec"),
                    ACodec = GetString(f, "acodec"),
                    FileSize = GetInt64(f, "filesize") ?? GetInt64(f, "filesize_approx"),
                    Tbr = GetDouble(f, "tbr"),
                    FormatNote = GetString(f, "format_note"),
                    Protocol = GetString(f, "protocol"),
                });
            }
        }

        double? durationSeconds = GetDouble(root, "duration");
        int? playlistCount = GetInt32(root, "playlist_count");
        if (playlistCount == null && root.TryGetProperty("entries", out JsonElement entries) && entries.ValueKind == JsonValueKind.Array)
        {
            playlistCount = entries.GetArrayLength();
        }

        return new YtDlpVideoInfo
        {
            Id = GetString(root, "id"),
            Title = GetString(root, "title"),
            Uploader = GetString(root, "uploader") ?? GetString(root, "channel") ?? GetString(root, "uploader_id"),
            WebpageUrl = GetString(root, "webpage_url"),
            Extractor = GetString(root, "extractor_key") ?? GetString(root, "extractor"),
            Thumbnail = GetString(root, "thumbnail"),
            Duration = durationSeconds is { } s && s > 0 ? TimeSpan.FromSeconds(s) : null,
            IsLive = GetBool(root, "is_live") ?? false,
            IsPlaylist = isPlaylist,
            PlaylistCount = playlistCount,
            Formats = formats,
        };
    }
}
