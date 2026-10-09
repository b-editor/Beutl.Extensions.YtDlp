using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpArgumentsTests
{
    [Test]
    public void BuildInfoArguments_RequestsSingleJsonForOneVideo()
    {
        IReadOnlyList<string> args = YtDlpArguments.BuildInfoArguments("https://example.com/v", null, null);

        Assert.That(args, Does.Contain("--dump-single-json"));
        Assert.That(args, Does.Contain("--no-playlist"));
        Assert.That(args[args.ToList().IndexOf("--extractor-args") + 1], Is.EqualTo("youtube:skip=dash,hls"));
        Assert.That(args.TakeLast(2), Is.EqualTo(new[] { "--", "https://example.com/v" }));
        Assert.That(args, Does.Not.Contain("--ffmpeg-location"));
    }

    [Test]
    public void BuildInfoArguments_AppendsFFmpegLocationAndExtraArguments()
    {
        IReadOnlyList<string> args = YtDlpArguments.BuildInfoArguments(
            "https://example.com/v", "/opt/ffmpeg/bin", "--cookies-from-browser chrome");

        int index = args.ToList().IndexOf("--ffmpeg-location");
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(args[index + 1], Is.EqualTo("/opt/ffmpeg/bin"));
        Assert.That(args, Does.Contain("--cookies-from-browser"));
        Assert.That(args, Does.Contain("chrome"));
    }

    [Test]
    public void BuildDownloadArguments_UsesTemplatesAndOutputPaths()
    {
        var request = new YtDlpDownloadRequest(
            "https://example.com/v", "/tmp/out", FormatPreset.UpTo1080p, null, null, null);

        List<string> args = YtDlpArguments.BuildDownloadArguments(request).ToList();

        Assert.That(args, Does.Contain("--newline"));
        Assert.That(args, Does.Contain("--progress"));
        Assert.That(args[args.IndexOf("-P") + 1], Is.EqualTo("/tmp/out"));
        Assert.That(args[args.IndexOf("-o") + 1], Is.EqualTo(YtDlpArguments.OutputTemplate));
        Assert.That(args, Has.Some.StartsWith("download:" + YtDlpArguments.DownloadProgressPrefix));
        Assert.That(args, Has.Some.StartsWith("postprocess:" + YtDlpArguments.PostProcessPrefix));
        Assert.That(args, Has.Some.StartsWith("after_move:" + YtDlpArguments.OutputFilePrefix));
        Assert.That(args[args.IndexOf("-S") + 1], Does.StartWith("res:1080"));
        Assert.That(args.TakeLast(2), Is.EqualTo(new[] { "--", "https://example.com/v" }));
    }

    [Test]
    public void EveryCommand_ForcesUtf8Output()
    {
        var request = new YtDlpDownloadRequest(
            "https://example.com/v", "/tmp/out", FormatPreset.Best, null, null, null);
        IReadOnlyList<string>[] commands =
        [
            YtDlpArguments.BuildInfoArguments("https://example.com/v", null, null),
            YtDlpArguments.BuildSearchArguments(SearchProvider.YouTube, "beutl", 5, null, null),
            YtDlpArguments.BuildDownloadArguments(request),
        ];

        foreach (IReadOnlyList<string> args in commands)
        {
            int index = args.ToList().IndexOf("--encoding");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(args[index + 1], Is.EqualTo("utf-8"));
        }
    }

    [Test]
    public void BuildDownloadArguments_KeepsWarningsForTheLog()
    {
        var request = new YtDlpDownloadRequest(
            "https://example.com/v", "/tmp/out", FormatPreset.Best, null, "/opt/ffmpeg", null);

        Assert.That(YtDlpArguments.BuildDownloadArguments(request), Does.Not.Contain("--no-warnings"));
    }

    [Test]
    public void BuildDownloadArguments_WithFFmpeg_MergesVideoAndAudio()
    {
        var request = new YtDlpDownloadRequest(
            "https://example.com/v", "/tmp/out", FormatPreset.Best, null, "/opt/ffmpeg", null);

        List<string> args = YtDlpArguments.BuildDownloadArguments(request).ToList();

        Assert.That(args[args.IndexOf("-f") + 1], Is.EqualTo("bv*+ba/b"));
        Assert.That(args[args.IndexOf("--merge-output-format") + 1], Is.EqualTo("mp4"));
    }

    [TestCase(FormatPreset.Best, "res,")]
    [TestCase(FormatPreset.UpTo1080p, "res:1080,")]
    [TestCase(FormatPreset.UpTo720p, "res:720,")]
    public void BuildDownloadArguments_WithoutFFmpeg_PicksSingleFileFormats(FormatPreset preset, string sortPrefix)
    {
        var request = new YtDlpDownloadRequest("https://example.com/v", "/tmp/out", preset, null, null, null);

        List<string> args = YtDlpArguments.BuildDownloadArguments(request).ToList();

        Assert.That(args[args.IndexOf("-f") + 1], Is.EqualTo("b"));
        Assert.That(args[args.IndexOf("-S") + 1], Does.StartWith(sortPrefix));
        Assert.That(args, Does.Not.Contain("--merge-output-format"));
    }

    [Test]
    public void GetFormatArguments_AudioOnlyWithoutFFmpeg_KeepsTheOriginalAudio()
    {
        IReadOnlyList<string> args = YtDlpArguments.GetFormatArguments(FormatPreset.AudioOnly, null, canMerge: false);

        Assert.That(args, Is.EqualTo(new[] { "-f", "ba[ext=m4a]/ba/b" }));
    }

    [Test]
    public void GetFormatArguments_CustomIsUnchangedWithoutFFmpeg()
    {
        const string custom = "-f \"bv*[height<=480]+ba/b\" --merge-output-format mkv";

        Assert.That(
            YtDlpArguments.GetFormatArguments(FormatPreset.Custom, custom, canMerge: false),
            Is.EqualTo(YtDlpArguments.GetFormatArguments(FormatPreset.Custom, custom)));
    }

    [Test]
    public void GetFormatArguments_AudioOnlyExtractsM4a()
    {
        IReadOnlyList<string> args = YtDlpArguments.GetFormatArguments(FormatPreset.AudioOnly, null);

        Assert.That(args, Does.Contain("--extract-audio"));
        Assert.That(args[args.ToList().IndexOf("--audio-format") + 1], Is.EqualTo("m4a"));
        Assert.That(args, Does.Not.Contain("--merge-output-format"));
    }

    [Test]
    public void GetFormatArguments_CustomUsesUserArgumentsVerbatim()
    {
        IReadOnlyList<string> args = YtDlpArguments.GetFormatArguments(
            FormatPreset.Custom, "-f \"bv*[height<=480]+ba/b\" --merge-output-format mkv");

        Assert.That(args, Is.EqualTo(new[] { "-f", "bv*[height<=480]+ba/b", "--merge-output-format", "mkv" }));
    }

    [Test]
    public void ResolveFormat_WithoutItemFormat_ReturnsInputsUnchanged()
    {
        (FormatPreset preset, string? custom) = YtDlpArguments.ResolveFormat(FormatPreset.UpTo720p, "-f best", null);

        Assert.That(preset, Is.EqualTo(FormatPreset.UpTo720p));
        Assert.That(custom, Is.EqualTo("-f best"));
    }

    [Test]
    public void ResolveFormat_WithItemFormat_BecomesCustomSelector()
    {
        var video = new YtDlpFormat { FormatId = "137", VCodec = "avc1", ACodec = "none" };
        var audio = new YtDlpFormat { FormatId = "140", VCodec = "none", ACodec = "mp4a" };

        (FormatPreset preset, string? custom) = YtDlpArguments.ResolveFormat(FormatPreset.Best, null, video);
        Assert.That(preset, Is.EqualTo(FormatPreset.Custom));
        Assert.That(custom, Is.EqualTo("-f \"137+ba/137\" --merge-output-format mp4"));
        Assert.That(YtDlpArguments.SplitArguments(custom),
            Is.EqualTo(new[] { "-f", "137+ba/137", "--merge-output-format", "mp4" }));

        (_, custom) = YtDlpArguments.ResolveFormat(FormatPreset.Best, null, audio);
        Assert.That(custom, Is.EqualTo("-f \"140\" --merge-output-format mp4"));
    }

    [Test]
    public void SplitArguments_HandlesQuotesEscapesAndWhitespace()
    {
        Assert.That(YtDlpArguments.SplitArguments(null), Is.Empty);
        Assert.That(YtDlpArguments.SplitArguments("   "), Is.Empty);
        Assert.That(
            YtDlpArguments.SplitArguments("  -f   best  -o 'my file.mp4' --user-agent \"a \\\"b\\\" c\" d\\ e"),
            Is.EqualTo(new[] { "-f", "best", "-o", "my file.mp4", "--user-agent", "a \"b\" c", "d e" }));
    }

    [Test]
    public void SplitArguments_ThrowsOnUnterminatedQuote()
    {
        Assert.Throws<FormatException>(() => YtDlpArguments.SplitArguments("-f \"best"));
    }
}
