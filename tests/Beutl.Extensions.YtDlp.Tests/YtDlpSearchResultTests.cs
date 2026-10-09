using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpSearchResultTests
{
    // Shape of `yt-dlp -J --flat-playlist "ytsearch2:..."` (trimmed to the fields we read).
    private const string YouTubeJson = """
        {
          "_type": "playlist", "id": "me at the zoo", "extractor_key": "YoutubeSearch",
          "entries": [
            {
              "_type": "url", "ie_key": "Youtube", "id": "jNQXAC9IVRw", "title": "Me at the zoo",
              "url": "https://www.youtube.com/watch?v=jNQXAC9IVRw", "duration": 19, "view_count": 421238529,
              "uploader": "jawed", "channel": "jawed", "live_status": null,
              "thumbnails": [
                { "url": "https://i.ytimg.com/vi/jNQXAC9IVRw/hqdefault.jpg", "width": 480, "height": 270 },
                { "url": "https://i.ytimg.com/vi/jNQXAC9IVRw/maxres.jpg", "width": 1280, "height": 720 }
              ]
            },
            {
              "_type": "url", "ie_key": "Youtube", "id": "live1", "title": "Live now",
              "url": "https://www.youtube.com/watch?v=live1", "duration": null, "view_count": 1234,
              "channel": "Channel", "live_status": "is_live", "thumbnails": []
            },
            { "_type": "url", "ie_key": "Youtube", "id": "nourl", "title": "no url" }
          ]
        }
        """;

    private const string SoundCloudJson = """
        {
          "_type": "playlist", "extractor_key": "SoundcloudSearch",
          "entries": [
            {
              "_type": "url", "ie_key": "Soundcloud", "id": "1068221248", "title": "She Neva Know Lofi Ver",
              "url": "https://api.soundcloud.com/tracks/soundcloud%3Atracks%3A1068221248",
              "webpage_url": "https://soundcloud.com/tien/she-neva-know-lofi-ver",
              "duration": 165.218, "view_count": 2701864, "uploader": "Tien Pham",
              "thumbnails": [
                { "url": "https://i1.sndcdn.com/a-mini.jpg", "width": 16, "height": 16 },
                { "url": "https://i1.sndcdn.com/a-t500x500.jpg", "width": 500, "height": 500 },
                { "url": "https://i1.sndcdn.com/a-original.jpg" }
              ]
            }
          ]
        }
        """;

    [Test]
    public void ParseList_ReadsYouTubeFlatEntriesAndSkipsEntriesWithoutUrl()
    {
        IReadOnlyList<YtDlpSearchResult> results = YtDlpSearchResult.ParseList(YouTubeJson);

        Assert.That(results, Has.Count.EqualTo(2));

        YtDlpSearchResult first = results[0];
        Assert.That(first.Id, Is.EqualTo("jNQXAC9IVRw"));
        Assert.That(first.Title, Is.EqualTo("Me at the zoo"));
        Assert.That(first.Url, Is.EqualTo("https://www.youtube.com/watch?v=jNQXAC9IVRw"));
        Assert.That(first.Uploader, Is.EqualTo("jawed"));
        Assert.That(first.Duration, Is.EqualTo(TimeSpan.FromSeconds(19)));
        Assert.That(first.ViewCount, Is.EqualTo(421238529));
        Assert.That(first.ThumbnailUrl, Is.EqualTo("https://i.ytimg.com/vi/jNQXAC9IVRw/hqdefault.jpg"),
            "the largest thumbnail up to 640px wide is preferred over the 1280px one");
        Assert.That(first.IsLive, Is.False);
        Assert.That(first.Extractor, Is.EqualTo("Youtube"));
        Assert.That(first.Subtitle, Is.EqualTo("jawed · 0:19 · 4.2億回"));

        YtDlpSearchResult live = results[1];
        Assert.That(live.IsLive, Is.True);
        Assert.That(live.Duration, Is.Null);
        Assert.That(live.Uploader, Is.EqualTo("Channel"), "channel is used when uploader is missing");
        Assert.That(live.ThumbnailUrl, Is.Null);
        Assert.That(live.Subtitle, Is.EqualTo("Channel · ライブ配信 · 1,234回"));
    }

    [Test]
    public void ParseList_PrefersWebpageUrlAndBoundedThumbnailForSoundCloud()
    {
        IReadOnlyList<YtDlpSearchResult> results = YtDlpSearchResult.ParseList(SoundCloudJson);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Url, Is.EqualTo("https://soundcloud.com/tien/she-neva-know-lofi-ver"));
        Assert.That(results[0].ThumbnailUrl, Is.EqualTo("https://i1.sndcdn.com/a-t500x500.jpg"));
        Assert.That(results[0].Duration, Is.EqualTo(TimeSpan.FromSeconds(165.218)));
        Assert.That(results[0].Subtitle, Is.EqualTo("Tien Pham · 2:45 · 270.2万回"));
    }

    [Test]
    public void ParseList_SkipsChannelsPlaylistsAndProfiles()
    {
        const string json = """
            {
              "_type": "playlist",
              "entries": [
                { "_type": "url", "ie_key": "YoutubeTab", "id": "UCX6OQ3DkcsbYNE6H8uQQuVA", "title": "MrBeast", "url": "https://www.youtube.com/channel/UCX6OQ3DkcsbYNE6H8uQQuVA", "channel_follower_count": 1 },
                { "_type": "url", "ie_key": "YoutubeTab", "id": "PL123", "title": "Playlist", "url": "https://www.youtube.com/playlist?list=PL123", "playlist_count": 12 },
                { "_type": "url", "ie_key": "Youtube", "id": "@handle", "title": "Handle page", "url": "https://www.youtube.com/@mrbeast" },
                { "_type": "playlist", "ie_key": "Youtube", "id": "mix", "title": "Mix", "url": "https://www.youtube.com/watch?v=abc&list=RD1", "entries": [] },
                { "_type": "url", "ie_key": "Youtube", "id": "abc", "title": "Video", "url": "https://www.youtube.com/watch?v=abc", "duration": 10 },
                { "_type": "url", "ie_key": "Youtube", "id": "short", "title": "Short", "url": "https://www.youtube.com/shorts/xyz", "duration": 20 },
                { "_type": "url", "ie_key": "SoundcloudSet", "id": "set", "title": "Set", "url": "https://api.soundcloud.com/playlists/1", "webpage_url": "https://soundcloud.com/user/sets/best-of" },
                { "_type": "url", "ie_key": "SoundcloudUser", "id": "user", "title": "User", "url": "https://soundcloud.com/lofi_girl" },
                { "_type": "url", "ie_key": "Soundcloud", "id": "track", "title": "Track", "url": "https://api.soundcloud.com/tracks/1", "webpage_url": "https://soundcloud.com/lofi_girl/slow-arrival", "duration": 100 }
              ]
            }
            """;

        IReadOnlyList<YtDlpSearchResult> results = YtDlpSearchResult.ParseList(json);

        Assert.That(results.Select(r => r.Id), Is.EqualTo(new[] { "abc", "short", "track" }));
    }

    [Test]
    public void ParseList_SingleVideoResultIsWrapped()
    {
        IReadOnlyList<YtDlpSearchResult> results = YtDlpSearchResult.ParseList(
            """{ "id": "x", "title": "Single", "webpage_url": "https://example.com/x", "thumbnail": "https://example.com/t.jpg" }""");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Url, Is.EqualTo("https://example.com/x"));
        Assert.That(results[0].ThumbnailUrl, Is.EqualTo("https://example.com/t.jpg"));
    }

    [Test]
    public void ParseList_NoEntriesAndNoUrl_ReturnsEmpty()
    {
        Assert.That(YtDlpSearchResult.ParseList("""{ "_type": "playlist", "entries": [] }"""), Is.Empty);
        Assert.That(YtDlpSearchResult.ParseList("""{ "id": "x" }"""), Is.Empty);
    }

    [Test]
    public void DisplayTitle_FallsBackToUrl()
    {
        var result = new YtDlpSearchResult { Url = "https://example.com/v", Title = " " };

        Assert.That(result.DisplayTitle, Is.EqualTo("https://example.com/v"));
    }

    [Test]
    public void FromVideoInfo_CopiesFieldsAndPrefersWebpageUrl()
    {
        var info = new YtDlpVideoInfo
        {
            Id = "abc", Title = "Title", Uploader = "Up", WebpageUrl = "https://example.com/watch?v=abc",
            Extractor = "Youtube", Thumbnail = "https://example.com/t.jpg", Duration = TimeSpan.FromSeconds(65),
        };

        YtDlpSearchResult result = YtDlpSearchResult.FromVideoInfo(info, "https://typed.example/abc");

        Assert.That(result.Id, Is.EqualTo("abc"));
        Assert.That(result.Title, Is.EqualTo("Title"));
        Assert.That(result.Url, Is.EqualTo("https://example.com/watch?v=abc"));
        Assert.That(result.Uploader, Is.EqualTo("Up"));
        Assert.That(result.Duration, Is.EqualTo(TimeSpan.FromSeconds(65)));
        Assert.That(result.ViewCount, Is.Null);
        Assert.That(result.ThumbnailUrl, Is.EqualTo("https://example.com/t.jpg"));
        Assert.That(result.Extractor, Is.EqualTo("Youtube"));
        Assert.That(result.Subtitle, Is.EqualTo("Up · 1:05"));
    }

    [Test]
    public void FromVideoInfo_FallsBackToTypedUrlAndKeepsLiveFlag()
    {
        var info = new YtDlpVideoInfo { Title = "Live", IsLive = true };

        YtDlpSearchResult result = YtDlpSearchResult.FromVideoInfo(info, "https://typed.example/live");

        Assert.That(result.Url, Is.EqualTo("https://typed.example/live"));
        Assert.That(result.IsLive, Is.True);
        Assert.That(result.Duration, Is.Null);
        Assert.That(result.Subtitle, Is.EqualTo("ライブ配信"));
    }

    [TestCase(999, "999回")]
    [TestCase(12_345, "1.2万回")]
    [TestCase(150_000_000, "1.5億回")]
    public void FormatViewCount_UsesJapaneseUnits(long views, string expected)
    {
        Assert.That(YtDlpSearchResult.FormatViewCount(views), Is.EqualTo(expected));
    }
}
