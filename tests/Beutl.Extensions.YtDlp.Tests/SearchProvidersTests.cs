using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class SearchProvidersTests
{
    [TestCase(SearchProvider.YouTube, "ytsearch")]
    [TestCase(SearchProvider.SoundCloud, "scsearch")]
    public void GetPrefix_MapsProviders(SearchProvider provider, string expected)
    {
        Assert.That(SearchProviders.GetPrefix(provider), Is.EqualTo(expected));
    }

    [Test]
    public void BuildQueryUrl_YouTubeUsesResultsPageWithVideoFilter()
    {
        Assert.That(SearchProviders.BuildQueryUrl(SearchProvider.YouTube, "  me at the zoo ", 20),
            Is.EqualTo("https://www.youtube.com/results?search_query=me%20at%20the%20zoo&sp=EgIQAQ%253D%253D"));
        Assert.That(SearchProviders.BuildQueryUrl(SearchProvider.YouTube, "ヒカキン", 5),
            Is.EqualTo("https://www.youtube.com/results?search_query=%E3%83%92%E3%82%AB%E3%82%AD%E3%83%B3&sp=EgIQAQ%253D%253D"));
    }

    [Test]
    public void BuildQueryUrl_SoundCloudTrimsQueryAndClampsCount()
    {
        Assert.That(SearchProviders.BuildQueryUrl(SearchProvider.SoundCloud, " lofi ", 0),
            Is.EqualTo("scsearch1:lofi"));
        Assert.That(SearchProviders.BuildQueryUrl(SearchProvider.SoundCloud, "x", 1000),
            Is.EqualTo("scsearch100:x"));
    }

    [Test]
    public void BuildQueryUrl_RejectsEmptyQuery()
    {
        Assert.Throws<ArgumentException>(() => SearchProviders.BuildQueryUrl(SearchProvider.YouTube, "  ", 10));
    }

    [Test]
    public void BuildSearchArguments_UsesFlatPlaylistAndPseudoUrl()
    {
        IReadOnlyList<string> args = YtDlpArguments.BuildSearchArguments(
            SearchProvider.YouTube, "beutl", 5, "/opt/ffmpeg/bin", "--proxy http://localhost:8080");

        Assert.That(args, Does.Contain("--dump-single-json"));
        Assert.That(args, Does.Contain("--flat-playlist"));
        Assert.That(args, Does.Not.Contain("--no-playlist"));
        Assert.That(args[args.ToList().IndexOf("--playlist-end") + 1], Is.EqualTo("5"));
        Assert.That(args, Does.Contain("--proxy"));
        Assert.That(args.TakeLast(2), Is.EqualTo(new[] { "--", "https://www.youtube.com/results?search_query=beutl&sp=EgIQAQ%253D%253D" }));
    }
}
