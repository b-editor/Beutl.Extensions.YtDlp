using System.Globalization;
using System.Text.Json;
using Beutl.Extensions.YtDlp.Models;
using static Beutl.Extensions.YtDlp.Models.JsonElementHelpers;

namespace Beutl.Extensions.YtDlp.Services;

// yt-dlp's nicosearch currently returns no entries from Niconico's search page. The official
// snapshot API supplies list metadata in one request; extraction/downloads still go through yt-dlp.
public static class NiconicoSearch
{
    private static readonly HttpClient s_client = CreateClient();

    public static async Task<IReadOnlyList<YtDlpSearchResult>> SearchAsync(
        string query, int count, CancellationToken ct = default)
    {
        string json = await s_client.GetStringAsync(BuildQueryUrl(query, count), ct);
        return ParseList(json);
    }

    internal static string BuildQueryUrl(string query, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        count = Math.Clamp(count, SearchProviders.MinResultCount, SearchProviders.MaxResultCount);
        return "https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search"
               + $"?q={Uri.EscapeDataString(query.Trim())}&targets=title,tags,description"
               + "&fields=contentId,title,lengthSeconds,viewCounter,thumbnailUrl"
               + "&_sort=-viewCounter&_offset=0"
               + $"&_limit={count.ToString(CultureInfo.InvariantCulture)}&_context=Beutl.Extensions.YtDlp";
    }

    internal static IReadOnlyList<YtDlpSearchResult> ParseList(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("meta", out JsonElement meta) || GetInt32(meta, "status") != 200)
        {
            throw new JsonException("ニコニコ動画の検索結果を取得できませんでした。");
        }

        JsonElement entries = root.GetProperty("data");
        var results = new List<YtDlpSearchResult>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string? id = GetString(entry, "contentId");
            if (string.IsNullOrEmpty(id)) continue;

            double? duration = GetDouble(entry, "lengthSeconds");
            results.Add(new YtDlpSearchResult
            {
                Id = id,
                Title = GetString(entry, "title"),
                Url = $"https://www.nicovideo.jp/watch/{Uri.EscapeDataString(id)}",
                Duration = duration is > 0 ? TimeSpan.FromSeconds(duration.Value) : null,
                ViewCount = GetInt64(entry, "viewCounter"),
                ThumbnailUrl = GetString(entry, "thumbnailUrl"),
                Extractor = "Niconico",
            });
        }

        return results;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Beutl.Extensions.YtDlp");
        return client;
    }
}
