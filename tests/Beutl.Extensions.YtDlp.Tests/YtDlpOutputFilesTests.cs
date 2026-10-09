using System.Diagnostics;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

// The checks around a finished download: the reported files must exist, and Beutl's Linux ffmpeg
// needs its own directory on LD_LIBRARY_PATH.
public class YtDlpOutputFilesTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string CreateFile(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Test]
    public void FindMissingOutputError_ExistingFiles_ReturnsNull()
    {
        string file = CreateFile("【Music】Title [abc].mp4");

        Assert.That(YtDlpRunner.FindMissingOutputError([file]), Is.Null);
    }

    [Test]
    public void FindMissingOutputError_UnmergedParts_PointsAtFFmpeg()
    {
        // What yt-dlp leaves when it cannot run ffmpeg: the parts, and a reported merged path that is not there.
        CreateFile("Me at the zoo [jNQXAC9IVRw].f133.mp4");
        CreateFile("Me at the zoo [jNQXAC9IVRw].f140.m4a");
        string merged = Path.Combine(_root, "Me at the zoo [jNQXAC9IVRw].mp4");

        string? error = YtDlpRunner.FindMissingOutputError([merged]);

        Assert.That(error, Does.Contain("結合できませんでした").And.Contain("FFmpeg"));
    }

    [Test]
    public void FindMissingOutputError_NoFileAtAll_ReportsThePath()
    {
        string missing = Path.Combine(_root, "missing.mp4");

        Assert.That(YtDlpRunner.FindMissingOutputError([missing]), Does.Contain(missing));
    }

    [Test]
    public void ApplyFFmpegEnvironment_AddsBeutlsSharedLibrariesOnLinuxOnly()
    {
        string ffmpeg = CreateFile(YtDlpLocator.FFmpegFileName);
        CreateFile("libavcodec.so.62");
        var startInfo = new ProcessStartInfo("yt-dlp");
        startInfo.Environment["LD_LIBRARY_PATH"] = "/existing";

        YtDlpLocator.ApplyFFmpegEnvironment(startInfo, _root);

        if (OperatingSystem.IsLinux())
        {
            Assert.That(YtDlpLocator.GetFFmpegLibraryDirectory(ffmpeg), Is.EqualTo(_root));
            Assert.That(startInfo.Environment["LD_LIBRARY_PATH"], Is.EqualTo(_root + Path.PathSeparator + "/existing"));
        }
        else
        {
            Assert.That(YtDlpLocator.GetFFmpegLibraryDirectory(ffmpeg), Is.Null);
            Assert.That(startInfo.Environment["LD_LIBRARY_PATH"], Is.EqualTo("/existing"));
        }
    }

    [Test]
    public void GetFFmpegLibraryDirectory_WithoutSharedLibraries_ReturnsNull()
    {
        string ffmpeg = CreateFile(YtDlpLocator.FFmpegFileName);

        Assert.That(YtDlpLocator.GetFFmpegLibraryDirectory(ffmpeg), Is.Null);
    }
}
