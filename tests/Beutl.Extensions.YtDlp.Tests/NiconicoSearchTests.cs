using System.Net;
using System.Text.Json;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class NiconicoSearchTests
{
    [Test]
    public void SearchAsync_HttpTimeoutIsReportedAsFailure()
    {
        using var client = new HttpClient(new PendingHandler()) { Timeout = TimeSpan.FromMilliseconds(20) };

        var error = Assert.ThrowsAsync<TimeoutException>(async () =>
            await NiconicoSearch.SearchAsync(client, "test", 1).WaitAsync(TimeSpan.FromSeconds(5)))!;

        Assert.That(error.InnerException, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void SearchAsync_CallerCancellationRemainsCancellation()
    {
        using var client = new HttpClient(new PendingHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var error = Assert.CatchAsync<OperationCanceledException>(async () =>
            await NiconicoSearch.SearchAsync(client, "test", 1, cts.Token).WaitAsync(TimeSpan.FromSeconds(5)))!;

        Assert.That(error.CancellationToken, Is.EqualTo(cts.Token));
    }

    [TestCase(null, null)]
    [TestCase("--cookies-from-browser firefox", null)]
    [TestCase("--proxy http://localhost:8080", "http://localhost:8080")]
    [TestCase("--proxy=127.0.0.1:3128", "127.0.0.1:3128")]
    [TestCase("--proxy \"\"", "")]
    [TestCase("--proxy=http://first:8080 --proxy \"socks5h://last:1080\"", "socks5h://last:1080")]
    [TestCase("--proxy http://first:8080 --proxy=", "")]
    [TestCase("-- --proxy http://ignored:8080", null)]
    public void GetProxy_UsesExistingArgumentSplittingAndLastOption(string? arguments, string? expected)
    {
        Assert.That(NiconicoSearch.GetProxy(arguments), Is.EqualTo(expected));
    }

    [Test]
    public void CreateProxyHandler_UnspecifiedProxyPreservesDefaultRouting()
    {
        using var handler = NiconicoSearch.CreateProxyHandler(null);

        Assert.That(handler.UseProxy, Is.True);
        Assert.That(handler.Proxy, Is.Null);
    }

    [Test]
    public void CreateProxyHandler_EmptyProxyDisablesDefaultRouting()
    {
        using var handler = NiconicoSearch.CreateProxyHandler("");

        Assert.That(handler.UseProxy, Is.False);
        Assert.That(handler.Proxy, Is.Null);
    }

    [TestCase("127.0.0.1:3128", "http://127.0.0.1:3128/")]
    [TestCase("https://proxy.example:8443", "https://proxy.example:8443/")]
    [TestCase("socks4a://proxy.example:1080", "socks4a://proxy.example:1080/")]
    [TestCase("socks5h://proxy.example:1080", "socks5://proxy.example:1080/")]
    [TestCase("socks5://proxy.example", "socks5://proxy.example:1080/")]
    public void CreateProxyHandler_UsesConfiguredProxy(string address, string expected)
    {
        using var handler = NiconicoSearch.CreateProxyHandler(address);

        Assert.That(handler.UseProxy, Is.True);
        Assert.That(handler.Proxy!.GetProxy(new Uri("https://snapshot.search.nicovideo.jp")), Is.EqualTo(new Uri(expected)));
    }

    [TestCase("user%40name:pa%3Ass", "user@name", "pa:ss")]
    [TestCase(":password", "", "password")]
    public void CreateProxyHandler_PreservesCredentialsWithoutKeepingThemInTheAddress(
        string userInfo, string expectedUserName, string expectedPassword)
    {
        using var handler = NiconicoSearch.CreateProxyHandler($"http://{userInfo}@proxy.example:8080");
        Uri address = handler.Proxy!.GetProxy(new Uri("https://snapshot.search.nicovideo.jp"))!;
        NetworkCredential credentials = handler.Proxy.Credentials!.GetCredential(address, "Basic")!;

        Assert.That(address.UserInfo, Is.Empty);
        Assert.That(credentials.UserName, Is.EqualTo(expectedUserName));
        Assert.That(credentials.Password, Is.EqualTo(expectedPassword));
    }

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

    private sealed class PendingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
