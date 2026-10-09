using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpVideoInfoTests
{
    private const string VideoJson = """
        {
          "id": "abc123",
          "title": "テスト動画",
          "uploader": "Uploader",
          "duration": 125.5,
          "thumbnail": "https://example.com/thumb.jpg",
          "webpage_url": "https://example.com/watch?v=abc123",
          "extractor_key": "Youtube",
          "is_live": false,
          "formats": [
            { "format_id": "140", "ext": "m4a", "vcodec": "none", "acodec": "mp4a.40.2", "tbr": 129.5, "filesize": 2000000 },
            { "format_id": "137", "ext": "mp4", "width": 1920, "height": 1080, "fps": 30, "vcodec": "avc1.640028", "acodec": "none", "filesize_approx": 50000000 },
            { "format_id": "18", "ext": "mp4", "width": 640, "height": 360, "fps": 30, "vcodec": "avc1.42001E", "acodec": "mp4a.40.2" },
            { "ext": "mp4" }
          ]
        }
        """;

    [Test]
    public void Parse_ReadsBasicFieldsAndFormats()
    {
        YtDlpVideoInfo info = YtDlpVideoInfo.Parse(VideoJson);

        Assert.That(info.Id, Is.EqualTo("abc123"));
        Assert.That(info.Title, Is.EqualTo("テスト動画"));
        Assert.That(info.Uploader, Is.EqualTo("Uploader"));
        Assert.That(info.Duration, Is.EqualTo(TimeSpan.FromSeconds(125.5)));
        Assert.That(info.DurationText, Is.EqualTo("2:05"));
        Assert.That(info.Extractor, Is.EqualTo("Youtube"));
        Assert.That(info.IsPlaylist, Is.False);
        Assert.That(info.IsLive, Is.False);
        Assert.That(info.Formats, Has.Count.EqualTo(3), "formats without a format_id are skipped");
    }

    [Test]
    public void Parse_FormatFlagsAndSelectors()
    {
        YtDlpVideoInfo info = YtDlpVideoInfo.Parse(VideoJson);
        YtDlpFormat audio = info.Formats.Single(f => f.FormatId == "140");
        YtDlpFormat video = info.Formats.Single(f => f.FormatId == "137");
        YtDlpFormat combined = info.Formats.Single(f => f.FormatId == "18");

        Assert.That(audio.HasVideo, Is.False);
        Assert.That(audio.HasAudio, Is.True);
        Assert.That(audio.ToFormatSelector(), Is.EqualTo("140"));
        Assert.That(audio.FileSize, Is.EqualTo(2000000));

        Assert.That(video.HasVideo, Is.True);
        Assert.That(video.HasAudio, Is.False);
        Assert.That(video.ToFormatSelector(), Is.EqualTo("137+ba/137"));
        Assert.That(video.FileSize, Is.EqualTo(50000000), "filesize_approx is used when filesize is missing");
        Assert.That(video.DisplayName, Does.Contain("1920x1080").And.Contain("音声を自動結合"), "video-only streams are merged with the best audio, so the label must not say video-only");

        Assert.That(combined.ToFormatSelector(), Is.EqualTo("18"));
    }

    [Test]
    public void Parse_DetectsPlaylist()
    {
        YtDlpVideoInfo info = YtDlpVideoInfo.Parse("""
            { "_type": "playlist", "id": "PL1", "title": "List", "entries": [ {"id": "a"}, {"id": "b"} ] }
            """);

        Assert.That(info.IsPlaylist, Is.True);
        Assert.That(info.PlaylistCount, Is.EqualTo(2));
    }

    [Test]
    public void Parse_LiveStreamHasNoDuration()
    {
        YtDlpVideoInfo info = YtDlpVideoInfo.Parse("""{ "id": "x", "title": "Live", "is_live": true, "duration": null }""");

        Assert.That(info.IsLive, Is.True);
        Assert.That(info.Duration, Is.Null);
        Assert.That(info.DurationText, Is.EqualTo("ライブ配信"));
    }

    [Test]
    public void FormatDuration_UsesHoursWhenNeeded()
    {
        Assert.That(YtDlpVideoInfo.FormatDuration(TimeSpan.FromSeconds(59)), Is.EqualTo("0:59"));
        Assert.That(YtDlpVideoInfo.FormatDuration(TimeSpan.FromSeconds(3725)), Is.EqualTo("1:02:05"));
    }

    [Test]
    public void FormatBytes_ScalesUnits()
    {
        Assert.That(YtDlpFormat.FormatBytes(512), Is.EqualTo("512 B"));
        Assert.That(YtDlpFormat.FormatBytes(1536), Is.EqualTo("1.5 KB"));
        Assert.That(YtDlpFormat.FormatBytes(50_000_000), Is.EqualTo("47.7 MB"));
    }
}

public class YtDlpFormatLabelTests
{
    [Test]
    public void ShortName_AndDetails_ForVideoOnlyStream()
    {
        var format = new YtDlpFormat
        {
            FormatId = "137", Ext = "mp4", Width = 1920, Height = 1080, Fps = 30,
            VCodec = "avc1.640028", ACodec = "none", FileSize = 50_000_000, FormatNote = "1080p",
        };

        Assert.That(format.ShortName, Is.EqualTo("1080p"));
        Assert.That(format.Details, Is.EqualTo("H.264 · mp4 · 47.7 MB"));
    }

    [Test]
    public void ShortName_MarksHighFrameRate()
    {
        var format = new YtDlpFormat { FormatId = "299", Ext = "mp4", Height = 1080, Fps = 59.94, VCodec = "avc1.64002a", ACodec = "none" };

        Assert.That(format.ShortName, Is.EqualTo("1080p60"));
        Assert.That(format.Details, Is.EqualTo("H.264 · mp4"));
    }

    [Test]
    public void ShortName_AndDetails_ForAudioOnlyStream()
    {
        var format = new YtDlpFormat { FormatId = "140", Ext = "m4a", VCodec = "none", ACodec = "mp4a.40.2", Tbr = 129.4 };

        Assert.That(format.ShortName, Is.EqualTo("音声のみ m4a"));
        Assert.That(format.Details, Is.EqualTo("AAC · 129kbps"));
    }

    [Test]
    public void ShortName_FallsBackWhenHeightIsUnknown()
    {
        var format = new YtDlpFormat { FormatId = "hls-1", Ext = "mp4", Resolution = "640x360", VCodec = "avc1", ACodec = "mp4a" };

        Assert.That(format.ShortName, Is.EqualTo("640x360"));
        Assert.That(format.Details, Is.EqualTo("H.264 · mp4"));
    }
}
