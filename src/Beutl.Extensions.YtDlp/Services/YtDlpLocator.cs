using System.Diagnostics;
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

    // Returns a value suitable for `--ffmpeg-location` (a directory or the binary itself), or null when
    // no ffmpeg that actually starts was found. yt-dlp treats an ffmpeg it cannot start as missing and
    // then leaves video and audio unmerged, so a file that merely exists is not enough.
    public static string? ResolveFFmpegLocation(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            return File.Exists(expanded) || Directory.Exists(expanded) ? expanded : null;
        }

        foreach (string candidate in GetFFmpegCandidates())
        {
            if (File.Exists(candidate) && CanLaunchFFmpeg(candidate))
            {
                return Path.GetDirectoryName(candidate);
            }
        }

        return null;
    }

    // Same places Beutl's own FFmpegBinaryLocator probes, then PATH.
    private static IEnumerable<string> GetFFmpegCandidates()
    {
        // Beutl's BtbN install (Windows/Linux) flattens bin/ and lib/ into ~/.beutl/ffmpeg.
        string beutlFFmpeg = Path.Combine(BeutlEnvironment.GetHomeDirectoryPath(), "ffmpeg");
        yield return Path.Combine(beutlFFmpeg, FFmpegFileName);
        yield return Path.Combine(beutlFFmpeg, "bin", FFmpegFileName);

        if (OperatingSystem.IsMacOS())
        {
            foreach (string dir in GetHomebrewFFmpegDirectories())
            {
                yield return Path.Combine(dir, FFmpegFileName);
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            string rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "win-arm64",
                Architecture.X86 => "win-x86",
                _ => "win-x64",
            };
            yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", FFmpegFileName);
        }

        if (FindOnPath(FFmpegFileName) is { } onPath)
        {
            yield return onPath;
        }
    }

    // On Linux, Beutl's shared ffmpeg has RPATH $ORIGIN/../lib, but its libraries sit next to the
    // binary, so it only starts with that directory on LD_LIBRARY_PATH. yt-dlp passes the variable on to
    // the ffmpeg/ffprobe it runs. Returns null when no extra library path is needed.
    internal static string? GetFFmpegLibraryDirectory(string? ffmpegLocation)
    {
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(ffmpegLocation)) return null;

        string? directory = Directory.Exists(ffmpegLocation) ? ffmpegLocation : Path.GetDirectoryName(ffmpegLocation);
        if (directory == null || !Directory.Exists(directory)) return null;

        return Directory.EnumerateFiles(directory, "libavcodec.so*").Any() ? directory : null;
    }

    public static void ApplyFFmpegEnvironment(ProcessStartInfo startInfo, string? ffmpegLocation)
    {
        if (GetFFmpegLibraryDirectory(ffmpegLocation) is not { } libraryDirectory) return;

        string? current = startInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out string? value) ? value : null;
        startInfo.Environment["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(current)
            ? libraryDirectory
            : libraryDirectory + Path.PathSeparator + current;
    }

    private static bool CanLaunchFFmpeg(string path)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            ArgumentList = { "-version" },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        ApplyFFmpegEnvironment(startInfo, path);

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null) return false;

            // Drain the pipes so the long configuration banner cannot block the child.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
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
