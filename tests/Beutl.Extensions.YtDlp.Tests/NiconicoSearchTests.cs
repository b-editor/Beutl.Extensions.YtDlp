using System.Text.Json;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class NiconicoSearchTests
{
    [TestCase(0, 1)]
    [TestCase(20, 20)]
    [TestCase(1000, 100)]
    public void BuildQueryUrl_EncodesQueryAndClampsCount(int count, int expectedCount)
    {
        string url = NiconicoSearch.BuildQueryUrl(" 初音ミク & music/# ", count);

        Assert.That(url, Does.StartWith("https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search?"));
        Assert.That(url, Does.Contain("q=%E5%88%9D%E9%9F%B3%E3%83%9F%E3%82%AF%20%26%20music%2F%23&"));
        Assert.That(url, Does.Contain($"&_limit={expectedCount}&"));
        Assert.That(url, Does.Contain("&_context=Beutl.Extensions.YtDlp"));
    }

    [Test]
    public void BuildQueryUrl_RejectsEmptyQuery()
    {
        Assert.Throws<ArgumentException>(() => NiconicoSearch.BuildQueryUrl(" ", 20));
    }

    [Test]
    public void ParseList_MapsSnapshotMetadataToDownloadableVideos()
    {
        const string json = """
            {
              "meta": { "status": 200, "totalCount": 2 },
              "data": [
                {
                  "contentId": "sm15630734", "title": "『初音ミク』千本桜『オリジナル曲PV』",
                  "lengthSeconds": 244, "viewCounter": 18792981,
                  "thumbnailUrl": "https://nicovideo.cdn.nimg.jp/thumbnails/15630734/15630734.34480305"
                },
                { "contentId": "sm9", "title": "Video", "lengthSeconds": null, "thumbnailUrl": null },
                { "title": "No video ID" }
              ]
            }
            """;

        var results = NiconicoSearch.ParseList(json);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(results[0].Id, Is.EqualTo("sm15630734"));
            Assert.That(results[0].Title, Is.EqualTo("『初音ミク』千本桜『オリジナル曲PV』"));
            Assert.That(results[0].Url, Is.EqualTo("https://www.nicovideo.jp/watch/sm15630734"));
            Assert.That(results[0].Subtitle, Is.EqualTo("4:04 · 1879.3万回"));
            Assert.That(results[0].ThumbnailUrl, Is.EqualTo("https://nicovideo.cdn.nimg.jp/thumbnails/15630734/15630734.34480305"));
            Assert.That(results[0].Extractor, Is.EqualTo("Niconico"));
            Assert.That(results[1].Duration, Is.Null);
            Assert.That(results[1].ThumbnailUrl, Is.Null);
        });
    }

    [Test]
    public void ParseList_EmptySearchReturnsNoResults()
    {
        Assert.That(NiconicoSearch.ParseList("""{ "meta": { "status": 200 }, "data": [] }"""), Is.Empty);
    }

    [Test]
    public void ParseList_ApiErrorIsNotAnEmptySearch()
    {
        Assert.Throws<JsonException>(() => NiconicoSearch.ParseList("""{ "meta": { "status": 400 } }"""));
    }
}
