using System.Runtime.InteropServices;

namespace Beutl.Extensions.YtDlp.Services;

// Finds the yt-dlp and ffmpeg executables. Explicit settings win; otherwise the extension-managed
// install under ~/.beutl is preferred over anything found on PATH.
public static class YtDlpLocator
{
    public static string ExecutableFileName => OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    public static string FFmpegFileName => OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    public static string InstallDirectory => Path.Combine(BeutlEnvironment.GetHomeDirectoryPath(), "yt-dlp");

    public static string ManagedExecutablePath => Path.Combine(InstallDirectory, ExecutableFileName);

    // The unpackaged builds ship a "_internal" folder next to the executable; a lone file is the
    // single-file build that unpacks itself on every launch.
    public static bool IsUnpackagedBuild(string executablePath)
    {
        string? directory = Path.GetDirectoryName(executablePath);
        return directory != null && Directory.Exists(Path.Combine(directory, "_internal"));
    }

    public static string? ResolveExecutable(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            // A configured path that does not resolve is reported as "not found" instead of silently
            // falling back, so a typo in the settings stays visible.
            return ResolveConfiguredFile(configuredPath, ExecutableFileName);
        }

        if (File.Exists(ManagedExecutablePath))
        {
            return ManagedExecutablePath;
        }

        return FindOnPath(ExecutableFileName);
    }

    // Returns a value suitable for `--ffmpeg-location` (a directory or the binary itself), or null to
    // let yt-dlp search PATH on its own.
    public static string? ResolveFFmpegLocation(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            return File.Exists(expanded) || Directory.Exists(expanded) ? expanded : null;
        }

        string beutlFFmpeg = Path.Combine(BeutlEnvironment.GetHomeDirectoryPath(), "ffmpeg");
        if (File.Exists(Path.Combine(beutlFFmpeg, FFmpegFileName)))
        {
            return beutlFFmpeg;
        }

        if (OperatingSystem.IsMacOS())
        {
            foreach (string dir in GetHomebrewFFmpegDirectories())
            {
                if (File.Exists(Path.Combine(dir, FFmpegFileName)))
                {
                    return dir;
                }
            }
        }

        string? onPath = FindOnPath(FFmpegFileName);
        return onPath != null ? Path.GetDirectoryName(onPath) : null;
    }

    private static string? ResolveConfiguredFile(string configuredPath, string fileName)
    {
        string expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (File.Exists(expanded)) return expanded;

        string inDirectory = Path.Combine(expanded, fileName);
        return Directory.Exists(expanded) && File.Exists(inDirectory) ? inDirectory : null;
    }

    private static IEnumerable<string> GetHomebrewFFmpegDirectories()
    {
        // Beutl installs `ffmpeg@8` through Homebrew on macOS; that formula is keg-only.
        string prefix = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "/opt/homebrew" : "/usr/local";
        yield return $"{prefix}/opt/ffmpeg@8/bin";
        yield return $"{prefix}/opt/ffmpeg/bin";
        yield return $"{prefix}/bin";
    }

    internal static string? FindOnPath(string fileName)
    {
        foreach (string dir in GetSearchDirectories())
        {
            try
            {
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // An invalid PATH entry is skipped rather than aborting the whole search.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return dir;
            }
        }

        // GUI apps on macOS/Linux start with a minimal PATH, so add the usual install locations.
        if (!OperatingSystem.IsWindows())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return Path.Combine(home, ".local", "bin");
            yield return "/usr/bin";
        }
    }
}
