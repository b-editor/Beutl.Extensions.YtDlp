using System.Globalization;
using System.Net;
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
        string query, int count, string? extraArguments = null, CancellationToken ct = default)
    {
        string? proxy = GetProxy(extraArguments);
        using HttpClient? proxyClient = proxy == null ? null : CreateClient(proxy);
        return await SearchAsync(proxyClient ?? s_client, query, count, ct);
    }

    internal static async Task<IReadOnlyList<YtDlpSearchResult>> SearchAsync(
        HttpClient client, string query, int count, CancellationToken ct = default)
    {
        string json;
        try
        {
            json = await client.GetStringAsync(BuildQueryUrl(query, count), ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("ニコニコ動画の検索がタイムアウトしました。", ex);
        }

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

    internal static string? GetProxy(string? extraArguments)
    {
        IReadOnlyList<string> args = YtDlpArguments.SplitArguments(extraArguments);
        string? proxy = null;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--") break;
            if (args[i] == "--proxy")
            {
                if (++i == args.Count) throw new FormatException("--proxy の値を指定してください。");
                proxy = args[i];
            }
            else if (args[i].StartsWith("--proxy=", StringComparison.Ordinal))
            {
                proxy = args[i][8..];
            }
        }

        return proxy;
    }

    internal static HttpClientHandler CreateProxyHandler(string? proxyAddress)
    {
        var handler = new HttpClientHandler();
        // No option keeps the system/environment default; --proxy "" explicitly bypasses it.
        if (proxyAddress == null) return handler;
        handler.UseProxy = proxyAddress.Length > 0;
        if (!handler.UseProxy) return handler;

        var proxy = new WebProxy(proxyAddress);
        var address = new UriBuilder(proxy.Address!);
        string userInfo = proxy.Address!.UserInfo;
        // HttpClient's SOCKS5 implementation sends hostnames to the proxy, matching socks5h.
        if (address.Scheme == "socks5h") address.Scheme = "socks5";
        if (address.Scheme.StartsWith("socks", StringComparison.Ordinal) && address.Port < 0) address.Port = 1080;
        address.UserName = address.Password = "";
        proxy.Address = address.Uri;
        if (userInfo.Length > 0)
        {
            string[] credentials = userInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(Uri.UnescapeDataString(credentials[0]),
                credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "");
        }

        handler.Proxy = proxy;
        return handler;
    }

    private static HttpClient CreateClient(string? proxy = null)
    {
        var client = new HttpClient(CreateProxyHandler(proxy)) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Beutl.Extensions.YtDlp");
        return client;
    }
}
