using System.ComponentModel.DataAnnotations;

namespace Beutl.Extensions.YtDlp.Models;

// Keyword search sites. Niconico uses its snapshot API; the others use yt-dlp search extractors.
public enum SearchProvider
{
    [Display(Name = "YouTube")]
    YouTube,

    [Display(Name = "SoundCloud")]
    SoundCloud,

    [Display(Name = "ニコニコ動画")]
    Niconico,

    [Display(Name = "Bilibili")]
    Bilibili,
}

public static class SearchProviders
{
    public const int MinResultCount = 1;

    public const int MaxResultCount = 100;

    public static string GetPrefix(SearchProvider provider)
    {
        return provider switch
        {
            SearchProvider.YouTube => "ytsearch",
            SearchProvider.SoundCloud => "scsearch",
            SearchProvider.Niconico => "nicosearch",
            SearchProvider.Bilibili => "bilisearch",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    // YouTube's "sp=EgIQAQ%3D%3D" is the search filter "Type: Video", which keeps channels, playlists
    // and shelves out of the results; the pseudo-URL form ("ytsearchN:") cannot express that.
    public const string YouTubeVideoFilter = "EgIQAQ%253D%253D";

    public static string BuildQueryUrl(SearchProvider provider, string query, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        count = Math.Clamp(count, MinResultCount, MaxResultCount);
        string trimmed = query.Trim();
        return provider switch
        {
            SearchProvider.YouTube =>
                $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(trimmed)}&sp={YouTubeVideoFilter}",
            _ => $"{GetPrefix(provider)}{count}:{trimmed}",
        };
    }
}
