using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Beutl.Extensions.YtDlp.Models.JsonElementHelpers;

namespace Beutl.Extensions.YtDlp.Models;

// One entry of a `--flat-playlist` search result.
public sealed partial class YtDlpSearchResult
{
    public string? Id { get; init; }

    public string? Title { get; init; }

    public required string Url { get; init; }

    public string? Uploader { get; init; }

    public TimeSpan? Duration { get; init; }

    public long? ViewCount { get; init; }

    public string? ThumbnailUrl { get; init; }

    public bool IsLive { get; init; }

    public string? Extractor { get; init; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Url : Title;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Uploader)) parts.Add(Uploader);
            if (IsLive) parts.Add("ライブ配信");
            else if (Duration is { } d) parts.Add(YtDlpVideoInfo.FormatDuration(d));
            if (ViewCount is { } views) parts.Add(FormatViewCount(views));
            return string.Join(" · ", parts);
        }
    }

    public static string FormatViewCount(long views)
    {
        if (views >= 100_000_000)
        {
            return $"{(views / 100_000_000.0).ToString("0.#", CultureInfo.InvariantCulture)}億回";
        }

        if (views >= 10_000)
        {
            return $"{(views / 10_000.0).ToString("0.#", CultureInfo.InvariantCulture)}万回";
        }

        return $"{views.ToString("N0", CultureInfo.InvariantCulture)}回";
    }

    // Builds a list row from a fully extracted video (the URL-input path).
    public static YtDlpSearchResult FromVideoInfo(YtDlpVideoInfo info, string fallbackUrl)
    {
        return new YtDlpSearchResult
        {
            Id = info.Id,
            Title = info.Title,
            Url = string.IsNullOrEmpty(info.WebpageUrl) ? fallbackUrl : info.WebpageUrl,
            Uploader = info.Uploader,
            Duration = info.Duration,
            ViewCount = null,
            ThumbnailUrl = info.Thumbnail,
            IsLive = info.IsLive,
            Extractor = info.Extractor,
        };
    }

    // Parses the output of `yt-dlp --dump-single-json --flat-playlist "<prefix>N:<query>"`.
    public static IReadOnlyList<YtDlpSearchResult> ParseList(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            // A single video is returned when the pseudo-URL resolved to one item.
            return (GetString(root, "webpage_url") ?? GetString(root, "url")) is { } single
                ? [ParseEntry(root, single)]
                : [];
        }

        var results = new List<YtDlpSearchResult>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;

            // SoundCloud's `url` is an API endpoint; `webpage_url` is the page yt-dlp accepts as input.
            string? url = GetString(entry, "webpage_url") ?? GetString(entry, "url");
            if (string.IsNullOrEmpty(url)) continue;
            if (!IsVideoEntry(entry, url)) continue;

            results.Add(ParseEntry(entry, url));
        }

        return results;
    }

    [GeneratedRegex(@"youtube\.com/(channel/|c/|user/|@|playlist\?|results\?|hashtag/)", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeNonVideoUrlRegex();

    // "soundcloud.com/<user>" (a profile) or "soundcloud.com/<user>/sets/<set>" (a playlist).
    [GeneratedRegex(@"^https?://(www\.)?soundcloud\.com/[^/?#]+(/sets/[^/?#]+)?/?(\?.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SoundCloudNonTrackUrlRegex();

    // Search pages mix channels, playlists and user profiles into the results; only single videos
    // (or tracks) can be downloaded as one element.
    internal static bool IsVideoEntry(JsonElement entry, string url)
    {
        string? type = GetString(entry, "_type");
        if (type is "playlist" or "multi_video") return false;
        if (entry.TryGetProperty("entries", out _) || GetInt32(entry, "playlist_count") != null) return false;

        string? ieKey = GetString(entry, "ie_key");
        if (ieKey != null)
        {
            foreach (string suffix in new[] { "Tab", "Playlist", "Playlists", "Set", "User", "Channel", "Search", "SearchURL" })
            {
                if (ieKey.EndsWith(suffix, StringComparison.Ordinal)) return false;
            }
        }

        if (YouTubeNonVideoUrlRegex().IsMatch(url)) return false;
        if (SoundCloudNonTrackUrlRegex().IsMatch(url)) return false;

        return true;
    }

    private static YtDlpSearchResult ParseEntry(JsonElement entry, string url)
    {
        double? duration = GetDouble(entry, "duration");
        return new YtDlpSearchResult
        {
            Id = GetString(entry, "id"),
            Title = GetString(entry, "title"),
            Url = url,
            Uploader = GetString(entry, "uploader") ?? GetString(entry, "channel") ?? GetString(entry, "uploader_id"),
            Duration = duration is { } d && d > 0 ? TimeSpan.FromSeconds(d) : null,
            ViewCount = GetInt64(entry, "view_count"),
            ThumbnailUrl = PickThumbnail(entry),
            IsLive = GetString(entry, "live_status") == "is_live" || GetBool(entry, "is_live") == true,
            Extractor = GetString(entry, "ie_key") ?? GetString(entry, "extractor_key"),
        };
    }

    // Prefers the largest thumbnail that is still small enough for a list row.
    internal static string? PickThumbnail(JsonElement entry, int maxWidth = 640)
    {
        if (GetString(entry, "thumbnail") is { Length: > 0 } direct)
        {
            return direct;
        }

        if (!entry.TryGetProperty("thumbnails", out JsonElement thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? best = null;
        int bestWidth = -1;
        string? last = null;
        foreach (JsonElement thumbnail in thumbnails.EnumerateArray())
        {
            string? url = GetString(thumbnail, "url");
            if (string.IsNullOrEmpty(url)) continue;

            last = url;
            int width = GetInt32(thumbnail, "width") ?? 0;
            if (width <= maxWidth && width > bestWidth)
            {
                best = url;
                bestWidth = width;
            }
        }

        return best ?? last;
    }
}
