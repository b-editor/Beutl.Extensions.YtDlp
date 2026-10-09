using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

// End-to-end checks against the real yt-dlp binary. They need network access, write to the Beutl
// home directory (~/.beutl/yt-dlp or $BEUTL_HOME) and depend on a third-party site, so they only
// run when selected explicitly:
//   dotnet test --filter "FullyQualifiedName~YtDlpIntegrationTests"
[Explicit("Requires network access and installs yt-dlp into the Beutl home directory")]
public class YtDlpIntegrationTests
{
    // "Me at the zoo" - the first YouTube video (19 seconds).
    private const string TestUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw";

    [Test, Order(1)]
    public async Task Install_DownloadsAndVerifiesBinary()
    {
        var installer = new YtDlpInstaller();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        await installer.InstallAsync(cts.Token);

        Assert.That(installer.IsCompleted.Value, Is.True);
        Assert.That(installer.Error.Value, Is.Empty, installer.Error.Value);
        Assert.That(File.Exists(YtDlpLocator.ManagedExecutablePath), Is.True);
        Assert.That(installer.InstalledVersion.Value, Is.Not.Null.And.Not.Empty);
        TestContext.Out.WriteLine($"Installed yt-dlp {installer.InstalledVersion.Value} at {YtDlpLocator.ManagedExecutablePath}");
    }

    [Test, Order(2)]
    public async Task FetchInfo_ReturnsMetadataAndFormats()
    {
        YtDlpRunner runner = CreateRunner();

        YtDlpVideoInfo info = await runner.FetchInfoAsync(TestUrl, YtDlpLocator.ResolveFFmpegLocation(null), null);

        TestContext.Out.WriteLine($"{info.Title} / {info.Uploader} / {info.DurationText} / {info.Formats.Count} formats");
        Assert.That(info.IsPlaylist, Is.False);
        Assert.That(info.Title, Is.Not.Null.And.Not.Empty);
        Assert.That(info.Duration, Is.Not.Null);
        Assert.That(info.Formats, Is.Not.Empty);
    }

    [Test, Order(3)]
    public async Task Download_AudioOnly_ReportsProgressAndFinalPath()
    {
        YtDlpRunner runner = CreateRunner();
        string outputDirectory = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        var events = new List<YtDlpOutputEvent>();
        var log = new List<string>();

        try
        {
            var request = new YtDlpDownloadRequest(
                TestUrl, outputDirectory, FormatPreset.AudioOnly, null,
                YtDlpLocator.ResolveFFmpegLocation(null), null);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            IReadOnlyList<string> files = await runner.DownloadAsync(
                request,
                ev => { lock (events) events.Add(ev); },
                line => { lock (log) log.Add(line); },
                cts.Token);

            TestContext.Out.WriteLine(string.Join(Environment.NewLine, log));
            TestContext.Out.WriteLine($"Files: {string.Join(", ", files)}");
            TestContext.Out.WriteLine($"Events: {events.Count} (progress: {events.OfType<DownloadProgressEvent>().Count()})");

            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(File.Exists(files[0]), Is.True, files[0]);
            Assert.That(Path.GetExtension(files[0]), Is.EqualTo(".m4a"));
            Assert.That(Path.GetDirectoryName(files[0]), Is.EqualTo(outputDirectory));
            Assert.That(events.OfType<DownloadProgressEvent>(), Is.Not.Empty);
            Assert.That(events.OfType<DownloadProgressEvent>().Any(e => e.IsFinished), Is.True);
            Assert.That(events.OfType<PostProcessEvent>(), Is.Not.Empty, "post-processor progress is emitted on stderr in quiet mode");
            Assert.That(log, Has.None.StartsWith(YtDlpArguments.PostProcessPrefix));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test, Order(4)]
    public async Task Search_YouTube_ReturnsFlatResults()
    {
        YtDlpRunner runner = CreateRunner();

        IReadOnlyList<YtDlpSearchResult> results = await runner.SearchAsync(
            SearchProvider.YouTube, "me at the zoo", 3, YtDlpLocator.ResolveFFmpegLocation(null), null);

        foreach (YtDlpSearchResult r in results)
        {
            TestContext.Out.WriteLine($"{r.DisplayTitle} | {r.Subtitle} | {r.Url} | {r.ThumbnailUrl}");
        }

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results, Has.All.Property(nameof(YtDlpSearchResult.Url)).StartsWith("https://"));
        Assert.That(results, Has.All.Property(nameof(YtDlpSearchResult.Title)).Not.Empty);
        Assert.That(results, Has.Some.Property(nameof(YtDlpSearchResult.ThumbnailUrl)).Not.Null);
    }

    // With a usable ffmpeg the streams are merged; without one, single-file formats are picked. Either
    // way exactly one playable file must exist at the reported path, with no .fNNN parts left behind.
    // YouTube often serves no single-file format at all, so without ffmpeg the download may instead
    // fail with the message that asks for FFmpeg.
    [Test, Order(5)]
    public async Task Download_Best_ProducesOneFileAtTheReportedPath()
    {
        YtDlpRunner runner = CreateRunner();
        string outputDirectory = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        string? ffmpeg = YtDlpLocator.ResolveFFmpegLocation(null);
        var log = new List<string>();

        try
        {
            var request = new YtDlpDownloadRequest(TestUrl, outputDirectory, FormatPreset.Best, null, ffmpeg, null);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            IReadOnlyList<string> files;
            try
            {
                files = await runner.DownloadAsync(
                    request, null, line => { lock (log) log.Add(line); }, cts.Token);
            }
            catch (YtDlpException ex) when (ffmpeg == null)
            {
                TestContext.Out.WriteLine(ex.Message);
                Assert.That(ex.Message, Does.Contain("FFmpeg"));
                Assert.That(Directory.Exists(outputDirectory) ? Directory.GetFiles(outputDirectory) : [], Is.Empty);
                return;
            }

            TestContext.Out.WriteLine($"ffmpeg: {ffmpeg ?? "(none)"}");
            TestContext.Out.WriteLine(string.Join(Environment.NewLine, log));
            Assert.That(files, Has.Count.EqualTo(1));
            Assert.That(File.Exists(files[0]), Is.True, files[0]);
            Assert.That(Directory.GetFiles(outputDirectory), Is.EqualTo(new[] { files[0] }));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static YtDlpRunner CreateRunner()
    {
        string? executable = YtDlpLocator.ResolveExecutable(null);
        Assume.That(executable, Is.Not.Null, "yt-dlp is not installed; run Install_DownloadsAndVerifiesBinary first");
        return new YtDlpRunner(executable!);
    }
}
