using Beutl.Extensions.YtDlp.Settings;
using Beutl.Logging;
using Microsoft.Extensions.Logging;
using Reactive.Bindings;

namespace Beutl.Extensions.YtDlp.Services;

// Shared, observable view of where yt-dlp/ffmpeg live and which yt-dlp version is installed.
public sealed class YtDlpEnvironment(YtDlpSettings settings)
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpEnvironment>();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public YtDlpSettings Settings { get; } = settings;

    public ReactiveProperty<string?> ExecutablePath { get; } = new();

    public ReactiveProperty<string?> FFmpegLocation { get; } = new();

    public ReactiveProperty<string?> Version { get; } = new();

    public ReactiveProperty<bool> IsAvailable { get; } = new();

    // True when the extension-managed install is the single-file build (slow to start).
    public ReactiveProperty<bool> IsSlowBuild { get; } = new();

    public ReactiveProperty<bool> IsRefreshing { get; } = new();

    // False until the first detection has finished; lets the UI avoid showing "not found" while
    // `yt-dlp --version` is still running.
    public ReactiveProperty<bool> HasChecked { get; } = new();

    public Task RefreshAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            await _refreshLock.WaitAsync(ct);
            IsRefreshing.Value = true;
            try
            {
                string? executable = YtDlpLocator.ResolveExecutable(Settings.ExecutablePath);
                string? ffmpeg = YtDlpLocator.ResolveFFmpegLocation(Settings.FFmpegPath);
                string? version = null;

                if (executable != null)
                {
                    try
                    {
                        version = await new YtDlpRunner(executable).GetVersionAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // A binary that cannot report its version is treated as unavailable.
                        _logger.LogWarning(ex, "yt-dlp at {Path} could not be executed", executable);
                        executable = null;
                    }
                }

                ExecutablePath.Value = executable;
                FFmpegLocation.Value = ffmpeg;
                Version.Value = version;
                IsAvailable.Value = executable != null;
                IsSlowBuild.Value = executable != null
                                    && string.Equals(executable, YtDlpLocator.ManagedExecutablePath, StringComparison.Ordinal)
                                    && !YtDlpLocator.IsUnpackagedBuild(executable);

                _logger.LogInformation(
                    "yt-dlp: {Executable} ({Version}), ffmpeg: {FFmpeg}",
                    executable ?? "(not found)", version ?? "-", ffmpeg ?? "(PATH)");
            }
            finally
            {
                IsRefreshing.Value = false;
                HasChecked.Value = true;
                _refreshLock.Release();
            }
        }, ct);
    }

    public YtDlpRunner? CreateRunner()
    {
        return ExecutablePath.Value is { } path ? new YtDlpRunner(path) : null;
    }
}
