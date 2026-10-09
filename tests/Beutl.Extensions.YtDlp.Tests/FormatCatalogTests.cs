using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Tests;

public class FormatCatalogTests
{
    private static YtDlpFormat Video(string id, int height, double fps, string vcodec, string ext, double? tbr = null, long? size = null)
        => new() { FormatId = id, Height = height, Width = height * 16 / 9, Fps = fps, VCodec = vcodec, ACodec = "none", Ext = ext, Tbr = tbr, FileSize = size };

    private static YtDlpFormat Audio(string id, string ext, string acodec, double tbr)
        => new() { FormatId = id, VCodec = "none", ACodec = acodec, Ext = ext, Tbr = tbr };

    [Test]
    public void Summarize_KeepsOneEntryPerResolutionPreferringH264ThenMp4()
    {
        YtDlpFormat[] formats =
        [
            Video("617", 1080, 60, "vp09.00.41.08", "mp4", tbr: 4000),
            Video("312", 1080, 60, "avc1.64002A", "mp4", tbr: 3500),
            Video("399", 1080, 60, "av01.0.09M.08", "mp4", tbr: 3000),
            Video("303", 1080, 60, "vp9", "webm", tbr: 4200),
            Video("137", 1080, 30, "avc1.640028", "mp4", tbr: 2500),
            Video("248", 1080, 30, "vp9", "webm", tbr: 2600),
            Video("22", 720, 30, "avc1.64001F", "mp4", tbr: 1200),
            Video("398", 720, 60, "av01.0.08M.08", "mp4", tbr: 1500),
            Video("302", 720, 60, "vp9", "webm", tbr: 1600),
            new() { FormatId = "sb0", VCodec = "none", ACodec = "none", Ext = "mhtml" },
        ];

        IReadOnlyList<YtDlpFormat> result = FormatCatalog.Summarize(formats);

        Assert.That(result.Select(f => f.FormatId), Is.EqualTo(new[] { "312", "137", "398", "22" }));
        Assert.That(result.Select(f => f.ShortName), Is.EqualTo(new[] { "1080p60", "1080p", "720p60", "720p" }));
        Assert.That(result[2].Details, Is.EqualTo("AV1 · mp4"), "AV1 wins when no H.264 exists at that frame rate");
    }

    [Test]
    public void Summarize_PrefersMp4ContainerWithinTheSameCodec()
    {
        YtDlpFormat[] formats =
        [
            Video("a", 480, 30, "vp9", "webm", tbr: 900),
            Video("b", 480, 30, "vp09.00.30.08", "mp4", tbr: 800),
        ];

        Assert.That(FormatCatalog.Summarize(formats).Single().FormatId, Is.EqualTo("b"));
    }

    [Test]
    public void Summarize_CollapsesAudioPerContainerAndAppendsAfterVideo()
    {
        YtDlpFormat[] formats =
        [
            Audio("251", "webm", "opus", 140),
            Audio("140", "m4a", "mp4a.40.2", 129),
            Audio("140-drc", "m4a", "mp4a.40.2", 129),
            Audio("139", "m4a", "mp4a.40.5", 48),
            Video("18", 360, 30, "avc1.42001E", "mp4", tbr: 500),
        ];

        IReadOnlyList<YtDlpFormat> result = FormatCatalog.Summarize(formats);

        Assert.That(result.Select(f => f.FormatId), Is.EqualTo(new[] { "18", "140", "251" }));
        Assert.That(result[1].ShortName, Is.EqualTo("音声のみ m4a"));
        Assert.That(result[1].Details, Is.EqualTo("AAC · 129kbps"));
        Assert.That(result[2].Details, Is.EqualTo("Opus · 140kbps"));
    }

    [TestCase("avc1.640028", "H.264")]
    [TestCase("av01.0.09M.08", "AV1")]
    [TestCase("vp09.00.41.08", "VP9")]
    [TestCase("vp9", "VP9")]
    [TestCase("hev1.1.6.L93.B0", "H.265")]
    [TestCase("mp4a.40.2", "AAC")]
    [TestCase("opus", "Opus")]
    [TestCase("something.else", "something")]
    [TestCase("none", "")]
    [TestCase(null, "")]
    public void CodecFamily_MapsKnownCodecs(string? codec, string expected)
    {
        Assert.That(FormatCatalog.CodecFamily(codec), Is.EqualTo(expected));
    }
}
