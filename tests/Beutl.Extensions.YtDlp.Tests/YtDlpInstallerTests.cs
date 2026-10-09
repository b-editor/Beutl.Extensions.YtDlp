using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpInstallerTests
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

    private static string PlatformExecutableName(string stem) => OperatingSystem.IsWindows() ? stem + ".exe" : stem;

    [Test]
    public void PreparePayload_RenamesRootExecutableAndKeepsInternalFolder()
    {
        File.WriteAllText(Path.Combine(_root, PlatformExecutableName("yt-dlp_macos")), "bin");
        Directory.CreateDirectory(Path.Combine(_root, "_internal"));
        File.WriteAllText(Path.Combine(_root, "_internal", "Python"), "lib");

        YtDlpInstaller.PreparePayload(_root);

        string executable = Path.Combine(_root, YtDlpLocator.ExecutableFileName);
        Assert.That(File.Exists(executable), Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "_internal", "Python")), Is.True);
        Assert.That(YtDlpLocator.IsUnpackagedBuild(executable), Is.True);
        if (!OperatingSystem.IsWindows())
        {
            Assert.That(File.GetUnixFileMode(executable).HasFlag(UnixFileMode.UserExecute), Is.True);
        }
    }

    [Test]
    public void PreparePayload_HoistsSingleNestedFolder()
    {
        string nested = Path.Combine(_root, "yt-dlp_linux");
        Directory.CreateDirectory(Path.Combine(nested, "_internal"));
        File.WriteAllText(Path.Combine(nested, PlatformExecutableName("yt-dlp_linux")), "bin");
        File.WriteAllText(Path.Combine(nested, "_internal", "lib.so"), "lib");

        YtDlpInstaller.PreparePayload(_root);

        Assert.That(File.Exists(Path.Combine(_root, YtDlpLocator.ExecutableFileName)), Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "_internal", "lib.so")), Is.True);
        Assert.That(Directory.Exists(nested), Is.False);
    }

    [Test]
    public void PreparePayload_ThrowsWhenNoExecutableIsPresent()
    {
        File.WriteAllText(Path.Combine(_root, "README.txt"), "nothing here");

        Assert.Throws<InvalidOperationException>(() => YtDlpInstaller.PreparePayload(_root));
    }

    [Test]
    public void IsUnpackagedBuild_IsFalseForLoneExecutable()
    {
        string executable = Path.Combine(_root, YtDlpLocator.ExecutableFileName);
        File.WriteAllText(executable, "bin");

        Assert.That(YtDlpLocator.IsUnpackagedBuild(executable), Is.False);
    }
}
