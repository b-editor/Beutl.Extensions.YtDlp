using System.Runtime.InteropServices;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpReleaseAssetsTests
{
    [TestCase(HostPlatform.Windows, Architecture.X64, "yt-dlp_win.zip")]
    [TestCase(HostPlatform.Windows, Architecture.X86, "yt-dlp_win_x86.zip")]
    [TestCase(HostPlatform.Windows, Architecture.Arm64, "yt-dlp_win_arm64.zip")]
    [TestCase(HostPlatform.MacOS, Architecture.Arm64, "yt-dlp_macos.zip")]
    [TestCase(HostPlatform.MacOS, Architecture.X64, "yt-dlp_macos.zip")]
    [TestCase(HostPlatform.Linux, Architecture.X64, "yt-dlp_linux.zip")]
    [TestCase(HostPlatform.Linux, Architecture.Arm64, "yt-dlp_linux_aarch64.zip")]
    [TestCase(HostPlatform.Linux, Architecture.Arm, "yt-dlp_linux_armv7l.zip")]
    public void GetAssetName_MapsSupportedPlatformsToUnpackagedBuilds(HostPlatform platform, Architecture architecture, string expected)
    {
        Assert.That(YtDlpReleaseAssets.GetAssetName(platform, architecture), Is.EqualTo(expected));
    }

    [TestCase(HostPlatform.Linux, Architecture.Wasm)]
    [TestCase(HostPlatform.Other, Architecture.X64)]
    public void GetAssetName_ReturnsNullForUnsupportedPlatforms(HostPlatform platform, Architecture architecture)
    {
        Assert.That(YtDlpReleaseAssets.GetAssetName(platform, architecture), Is.Null);
    }

    [Test]
    public void GetDownloadUri_PointsAtLatestRelease()
    {
        Assert.That(
            YtDlpReleaseAssets.GetDownloadUri("yt-dlp_macos.zip").ToString(),
            Is.EqualTo("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos.zip"));
    }

    [Test]
    public void FindChecksum_ReadsMatchingLine()
    {
        const string sums = """
            0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  yt-dlp
            ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789  yt-dlp_macos
            fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210 *yt-dlp.exe
            """;

        Assert.That(YtDlpReleaseAssets.FindChecksum(sums, "yt-dlp_macos"),
            Is.EqualTo("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"));
        Assert.That(YtDlpReleaseAssets.FindChecksum(sums, "yt-dlp.exe"),
            Is.EqualTo("fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210"));
        Assert.That(YtDlpReleaseAssets.FindChecksum(sums, "yt-dlp"),
            Is.EqualTo("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
        Assert.That(YtDlpReleaseAssets.FindChecksum(sums, "yt-dlp_linux"), Is.Null);
    }
}
