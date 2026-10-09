namespace Beutl.Extensions.YtDlp.ViewModels;

public sealed class DownloadHistoryItem(
    string title,
    string filePath,
    TimeSpan? duration,
    string? url = null,
    DateTimeOffset? downloadedAt = null)
{
    public string Title { get; } = title;

    public string FilePath { get; } = filePath;

    public TimeSpan? Duration { get; } = duration;

    public string? Url { get; } = url;

    public DateTimeOffset DownloadedAt { get; } = downloadedAt ?? DateTimeOffset.Now;

    public string FileName => Path.GetFileName(FilePath);

    public string Directory => Path.GetDirectoryName(FilePath) ?? "";

    public string Subtitle => $"{DownloadedAt.LocalDateTime:yyyy/MM/dd HH:mm} · {FileName}";
}
