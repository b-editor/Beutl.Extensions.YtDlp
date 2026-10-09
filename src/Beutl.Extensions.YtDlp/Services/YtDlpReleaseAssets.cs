using System.Runtime.InteropServices;

namespace Beutl.Extensions.YtDlp.Services;

public enum HostPlatform
{
    Windows,
    MacOS,
    Linux,
    Other,
}

public static class YtDlpReleaseAssets
{
    public const string LatestReleaseBaseUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";

    public const string ChecksumsAssetName = "SHA2-256SUMS";

    public static HostPlatform CurrentPlatform =>
        OperatingSystem.IsWindows() ? HostPlatform.Windows
        : OperatingSystem.IsMacOS() ? HostPlatform.MacOS
        : OperatingSystem.IsLinux() ? HostPlatform.Linux
        : HostPlatform.Other;

    public static string? GetAssetName() => GetAssetName(CurrentPlatform, RuntimeInformation.OSArchitecture);

    // The "unpackaged" zip builds (PyInstaller onedir). The single-file executables unpack ~40 MB
    // into a temp directory on every launch, which costs several seconds per yt-dlp call; the
    // unpackaged builds start in well under a second. Returns null when no build exists.
    public static string? GetAssetName(HostPlatform platform, Architecture architecture)
    {
        return platform switch
        {
            HostPlatform.Windows => architecture switch
            {
                Architecture.X64 => "yt-dlp_win.zip",
                Architecture.X86 => "yt-dlp_win_x86.zip",
                Architecture.Arm64 => "yt-dlp_win_arm64.zip",
                _ => null,
            },
            // Universal binary.
            HostPlatform.MacOS => "yt-dlp_macos.zip",
            HostPlatform.Linux => architecture switch
            {
                Architecture.X64 => "yt-dlp_linux.zip",
                Architecture.Arm64 => "yt-dlp_linux_aarch64.zip",
                Architecture.Arm => "yt-dlp_linux_armv7l.zip",
                _ => null,
            },
            _ => null,
        };
    }

    public static Uri GetDownloadUri(string assetName) => new(LatestReleaseBaseUrl + assetName);

    // SHA2-256SUMS lines look like "<64 hex chars>  <file name>".
    public static string? FindChecksum(string checksumsText, string assetName)
    {
        foreach (string rawLine in checksumsText.Split('\n'))
        {
            string line = rawLine.Trim();
            int separator = line.IndexOf(' ');
            if (separator <= 0) continue;

            string name = line[separator..].Trim().TrimStart('*');
            if (string.Equals(name, assetName, StringComparison.Ordinal))
            {
                return line[..separator].ToLowerInvariant();
            }
        }

        return null;
    }
}
