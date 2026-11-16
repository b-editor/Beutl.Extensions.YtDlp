namespace Beutl.Extensions.YtDlp.Models;

internal sealed record DownloadOptions(
    FormatPreset Preset,
    string? CustomArguments,
    YtDlpFormat? Format,
    DownloadDestination Destination,
    string OutputDirectory,
    bool AddToTimeline);
