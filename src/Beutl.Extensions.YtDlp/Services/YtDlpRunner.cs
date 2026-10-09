using System.Diagnostics;
using System.Text;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Logging;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp.Services;

// Runs the yt-dlp executable as a child process.
public sealed class YtDlpRunner(string executablePath)
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpRunner>();

    public string ExecutablePath { get; } = executablePath;

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        ProcessResult result = await RunAsync(YtDlpArguments.BuildVersionArguments(), null, null, null, ct);
        result.EnsureSuccess();
        return result.StandardOutput.Trim();
    }

    public async Task<YtDlpVideoInfo> FetchInfoAsync(
        string url, string? ffmpegLocation, string? extraArguments, CancellationToken ct = default)
    {
        ProcessResult result = await RunAsync(
            YtDlpArguments.BuildInfoArguments(url, ffmpegLocation, extraArguments), ffmpegLocation, null, null, ct);
        result.EnsureSuccess();

        string json = result.StandardOutput.Trim();
        if (json.Length == 0)
        {
            throw new YtDlpException("yt-dlp から動画情報が返されませんでした。", result.ExitCode, result.StandardError);
        }

        return YtDlpVideoInfo.Parse(json);
    }

    public async Task<IReadOnlyList<YtDlpSearchResult>> SearchAsync(
        SearchProvider provider, string query, int count,
        string? ffmpegLocation, string? extraArguments, CancellationToken ct = default)
    {
        ProcessResult result = await RunAsync(
            YtDlpArguments.BuildSearchArguments(provider, query, count, ffmpegLocation, extraArguments),
            ffmpegLocation, null, null, ct);
        result.EnsureSuccess();

        string json = result.StandardOutput.Trim();
        if (json.Length == 0)
        {
            throw new YtDlpException("yt-dlp から検索結果が返されませんでした。", result.ExitCode, result.StandardError);
        }

        return YtDlpSearchResult.ParseList(json);
    }

    // Returns the final paths of every file yt-dlp produced (one per video).
    public async Task<IReadOnlyList<string>> DownloadAsync(
        YtDlpDownloadRequest request,
        Action<YtDlpOutputEvent>? onEvent,
        Action<string>? onLog,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(request.OutputDirectory);

        var files = new List<string>();

        void HandleLine(string line)
        {
            YtDlpOutputEvent? ev = YtDlpProgressParser.Parse(line);
            if (ev is OutputFileEvent file)
            {
                lock (files) files.Add(file.FilePath);
            }

            if (ev != null)
            {
                onEvent?.Invoke(ev);
            }
            else
            {
                onLog?.Invoke(line);
            }
        }

        // --print implies --quiet, and in quiet mode yt-dlp writes post-processor progress (and other
        // screen output) to stderr while download progress and --print go to stdout. Parse both.
        ProcessResult result = await RunAsync(
            YtDlpArguments.BuildDownloadArguments(request),
            request.FFmpegLocation,
            HandleLine,
            HandleLine,
            ct);
        if (result.ExitCode != 0
            && request.FFmpegLocation == null
            && result.StandardError.Contains("Requested format is not available", StringComparison.Ordinal))
        {
            // Without ffmpeg only single-file formats are requested, and YouTube now mostly serves video
            // and audio separately, so yt-dlp's own message would not tell the user what to do.
            throw new YtDlpException(
                "この動画は映像と音声が別々の形式でしか提供されていないため、結合に FFmpeg が必要です。FFmpeg をインストールするか、拡張機能の設定で FFmpeg の場所を指定してください。",
                result.ExitCode,
                result.StandardError);
        }

        result.EnsureSuccess();

        string[] reported;
        lock (files)
        {
            if (files.Count == 0)
            {
                throw new YtDlpException(
                    "yt-dlp が出力ファイルのパスを報告しませんでした。", result.ExitCode, result.StandardError);
            }

            reported = files.ToArray();
        }

        if (FindMissingOutputError(reported) is { } error)
        {
            throw new YtDlpException(error, result.ExitCode, result.StandardError);
        }

        return reported;
    }

    // yt-dlp exits successfully and still prints the merged path when it could not merge (no usable
    // ffmpeg), leaving only the "<name>.fNNN.<ext>" parts. Catch that here instead of reporting success.
    internal static string? FindMissingOutputError(IReadOnlyList<string> files)
    {
        foreach (string file in files)
        {
            if (File.Exists(file)) continue;

            string? directory = Path.GetDirectoryName(file);
            bool hasParts = directory != null
                            && Directory.Exists(directory)
                            && Directory.EnumerateFiles(directory, Path.GetFileNameWithoutExtension(file) + ".f*").Any();

            return hasParts
                ? $"映像と音声を結合できませんでした。FFmpeg を利用できるか確認してください。分割されたファイルは保存先に残っています: {Path.GetFileName(file)}"
                : $"yt-dlp が報告したファイルが見つかりません: {file}";
        }

        return null;
    }

    private async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? ffmpegLocation,
        Action<string>? onStandardOutputLine,
        Action<string>? onStandardErrorLine,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // `--encoding utf-8` (added to every command line) is what fixes the pipe encoding; these only
        // help a plain Python install, since the frozen release builds may ignore PYTHON* variables.
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";
        YtDlpLocator.ApplyFFmpegEnvironment(startInfo, ffmpegLocation);

        _logger.LogInformation("Running yt-dlp: {Arguments}", string.Join(' ', arguments));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"yt-dlp を起動できませんでした: {ExecutablePath}");
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        Task stdoutTask = PumpAsync(process.StandardOutput, line =>
        {
            if (onStandardOutputLine != null)
            {
                onStandardOutputLine(line);
            }
            else
            {
                lock (standardOutput) standardOutput.AppendLine(line);
            }
        });
        Task stderrTask = PumpAsync(process.StandardError, line =>
        {
            lock (standardError) standardError.AppendLine(line);
            onStandardErrorLine?.Invoke(line);
        });

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        finally
        {
            // Drain the pipes after exit (or kill) so the readers observe EOF and finish.
            await Task.WhenAll(stdoutTask, stderrTask);
        }

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            onLine(line);
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill yt-dlp process");
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public void EnsureSuccess()
        {
            if (ExitCode == 0) return;

            throw new YtDlpException(BuildMessage(), ExitCode, StandardError);
        }

        private string BuildMessage()
        {
            string[] lines = StandardError
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string? error = lines.LastOrDefault(l => l.StartsWith("ERROR:", StringComparison.Ordinal))
                            ?? lines.LastOrDefault();
            return error != null
                ? $"yt-dlp がエラーで終了しました (コード {ExitCode}): {error}"
                : $"yt-dlp がエラーで終了しました (コード {ExitCode})";
        }
    }
}
