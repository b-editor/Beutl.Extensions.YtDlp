using System.IO.Compression;
using System.Security.Cryptography;
using Beutl.Logging;
using Microsoft.Extensions.Logging;
using Reactive.Bindings;

namespace Beutl.Extensions.YtDlp.Services;

// Downloads the latest unpackaged yt-dlp build from GitHub and installs it into ~/.beutl/yt-dlp.
public sealed class YtDlpInstaller
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpInstaller>();
    private static readonly TimeSpan s_httpTimeout = TimeSpan.FromMinutes(5);

    public ReactiveProperty<double> Progress { get; } = new(0);

    public ReactiveProperty<double> ProgressMax { get; } = new(1);

    public ReactiveProperty<bool> IsIndeterminate { get; } = new();

    public ReactiveProperty<string> Status { get; } = new("準備中");

    public ReactiveProperty<string> Error { get; } = new("");

    public ReactiveProperty<bool> IsCompleted { get; } = new();

    public ReactiveProperty<string?> InstalledVersion { get; } = new();

    public Task InstallAsync(CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            string home = BeutlEnvironment.GetHomeDirectoryPath();
            string installDirectory = YtDlpLocator.InstallDirectory;
            string stamp = Guid.NewGuid().ToString("N");
            string tempZip = Path.Combine(home, $".yt-dlp-{stamp}.zip");
            string? tempDirectory = Path.Combine(home, $".yt-dlp-{stamp}.tmp");
            string? backupDirectory = null;
            bool swapped = false;
            try
            {
                string assetName = YtDlpReleaseAssets.GetAssetName()
                    ?? throw new PlatformNotSupportedException(
                        "この OS / CPU 向けの yt-dlp は配布されていません。手動でインストールし、設定で実行ファイルのパスを指定してください。");

                Directory.CreateDirectory(home);

                using var client = new HttpClient { Timeout = s_httpTimeout };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Beutl.Extensions.YtDlp");

                Status.Value = "チェックサムを取得中 (1/5)";
                IsIndeterminate.Value = true;
                string? expectedHash = null;
                try
                {
                    string sums = await client.GetStringAsync(
                        YtDlpReleaseAssets.GetDownloadUri(YtDlpReleaseAssets.ChecksumsAssetName), ct);
                    expectedHash = YtDlpReleaseAssets.FindChecksum(sums, assetName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Missing checksums degrade to an unverified install rather than blocking it.
                    _logger.LogWarning(ex, "Failed to fetch yt-dlp checksums");
                }

                Status.Value = $"ダウンロード中 (2/5): {assetName}";
                await DownloadFileAsync(client, YtDlpReleaseAssets.GetDownloadUri(assetName), tempZip, ct);

                Status.Value = "検証中 (3/5)";
                IsIndeterminate.Value = true;
                if (expectedHash != null)
                {
                    string actualHash = await ComputeSha256Async(tempZip, ct);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("ダウンロードしたファイルのチェックサムが一致しません。");
                    }
                }
                else
                {
                    _logger.LogWarning("Installing yt-dlp without checksum verification");
                }

                Status.Value = "展開中 (4/5)";
                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, tempDirectory), ct);
                TryDeleteFile(tempZip);
                PreparePayload(tempDirectory);

                // Swap the finished payload in; keep the old install until the new one proves to work.
                if (Directory.Exists(installDirectory))
                {
                    backupDirectory = Path.Combine(home, $".yt-dlp-backup-{stamp}");
                    Directory.Move(installDirectory, backupDirectory);
                }

                Directory.Move(tempDirectory, installDirectory);
                tempDirectory = null;
                swapped = true;

                // The first launch of freshly written binaries can take a while (the OS inspects them once).
                Status.Value = "動作確認中 (5/5)";
                string version = await new YtDlpRunner(YtDlpLocator.ManagedExecutablePath).GetVersionAsync(ct);
                InstalledVersion.Value = version;

                if (backupDirectory != null)
                {
                    TryDeleteDirectory(backupDirectory);
                    backupDirectory = null;
                }

                IsIndeterminate.Value = false;
                Progress.Value = ProgressMax.Value;
                Status.Value = $"完了 (yt-dlp {version})";
                _logger.LogInformation("Installed yt-dlp {Version} to {Path}", version, installDirectory);
            }
            catch (OperationCanceledException)
            {
                RestoreBackup(installDirectory, backupDirectory, swapped);
                IsIndeterminate.Value = false;
                Status.Value = "キャンセルしました";
                _logger.LogInformation("yt-dlp installation canceled");
            }
            catch (Exception ex)
            {
                bool restored = RestoreBackup(installDirectory, backupDirectory, swapped);
                IsIndeterminate.Value = false;
                Status.Value = "失敗";
                Error.Value = restored ? $"{ex.Message}\n以前のインストールに戻しました。" : ex.Message;
                _logger.LogError(ex, "Failed to install yt-dlp");
            }
            finally
            {
                TryDeleteFile(tempZip);
                if (tempDirectory != null) TryDeleteDirectory(tempDirectory);
                IsCompleted.Value = true;
            }
        }, CancellationToken.None);
    }

    // Normalizes an extracted zip so the executable sits at <dir>/yt-dlp(.exe) next to its _internal folder.
    internal static void PreparePayload(string directory)
    {
        string payloadRoot = LocatePayloadRoot(directory);
        string executable = LocateExecutable(payloadRoot)
            ?? throw new InvalidOperationException("展開したアーカイブに yt-dlp の実行ファイルが見つかりません。");

        string destination = Path.Combine(directory, YtDlpLocator.ExecutableFileName);
        if (!string.Equals(payloadRoot, directory, StringComparison.Ordinal))
        {
            // Hoist a single nested folder ("yt-dlp_linux/…") up to the install root. The executable
            // goes first under its final name because the folder is often named the same as it.
            File.Move(executable, destination, overwrite: true);
            foreach (string entry in Directory.EnumerateFileSystemEntries(payloadRoot).ToArray())
            {
                string target = Path.Combine(directory, Path.GetFileName(entry));
                if (Directory.Exists(entry)) Directory.Move(entry, target);
                else File.Move(entry, target, overwrite: true);
            }

            Directory.Delete(payloadRoot, recursive: true);
        }
        else if (!string.Equals(executable, destination, StringComparison.Ordinal))
        {
            File.Move(executable, destination, overwrite: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string LocatePayloadRoot(string directory)
    {
        if (LocateExecutable(directory) != null) return directory;

        string[] subdirectories = Directory.GetDirectories(directory);
        if (subdirectories.Length == 1 && LocateExecutable(subdirectories[0]) != null)
        {
            return subdirectories[0];
        }

        return directory;
    }

    // "yt-dlp_macos", "yt-dlp_linux_aarch64", "yt-dlp.exe" … : a top-level file named yt-dlp*.
    private static string? LocateExecutable(string directory)
    {
        return Directory.EnumerateFiles(directory)
            .Where(f =>
            {
                string name = Path.GetFileName(f);
                if (!name.StartsWith("yt-dlp", StringComparison.OrdinalIgnoreCase)) return false;
                string extension = Path.GetExtension(name);
                return OperatingSystem.IsWindows()
                    ? extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    : extension.Length == 0;
            })
            .OrderBy(f => Path.GetFileName(f).Length)
            .FirstOrDefault();
    }

    private bool RestoreBackup(string installDirectory, string? backupDirectory, bool swapped)
    {
        if (backupDirectory == null || !Directory.Exists(backupDirectory)) return false;

        try
        {
            if (swapped && Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }

            Directory.Move(backupDirectory, installDirectory);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore the previous yt-dlp installation from {Backup}", backupDirectory);
            return false;
        }
    }

    private async Task DownloadFileAsync(HttpClient client, Uri uri, string path, CancellationToken ct)
    {
        _logger.LogInformation("Downloading {Url} to {Path}", uri, path);
        using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;
        IsIndeterminate.Value = contentLength is null;
        ProgressMax.Value = contentLength ?? 1;
        Progress.Value = 0;

        await using Stream source = await response.Content.ReadAsStreamAsync(ct);
        await using FileStream destination = File.Create(path);

        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
            if (contentLength is not null)
            {
                Progress.Value = total;
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary file {Path}", path);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete directory {Path}", path);
        }
    }
}
