namespace Beutl.Extensions.YtDlp.Models;

// A structured line emitted by yt-dlp through the templates in YtDlpArguments.
public abstract record YtDlpOutputEvent;

public sealed record DownloadProgressEvent(
    string Status,
    long? DownloadedBytes,
    long? TotalBytes,
    double? Speed,
    int? Eta) : YtDlpOutputEvent
{
    public bool IsFinished => Status == "finished";

    // 0..1, or null while the total size is unknown.
    public double? Fraction
    {
        get
        {
            if (IsFinished) return 1;
            if (DownloadedBytes is not { } downloaded || TotalBytes is not { } total || total <= 0) return null;
            return Math.Clamp((double)downloaded / total, 0, 1);
        }
    }
}

public sealed record PostProcessEvent(string Status, string Postprocessor) : YtDlpOutputEvent
{
    public bool IsStarted => Status == "started";

    public bool IsFinished => Status == "finished";
}

public sealed record OutputFileEvent(string FilePath) : YtDlpOutputEvent;
