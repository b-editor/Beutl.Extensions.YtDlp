using System.Globalization;

namespace Beutl.Extensions.YtDlp.Models;

public sealed class YtDlpFormat
{
    public required string FormatId { get; init; }

    public string? Ext { get; init; }

    public string? Resolution { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? Fps { get; init; }

    public string? VCodec { get; init; }

    public string? ACodec { get; init; }

    public long? FileSize { get; init; }

    public double? Tbr { get; init; }

    public string? FormatNote { get; init; }

    public string? Protocol { get; init; }

    public bool HasVideo => VCodec is not (null or "none");

    public bool HasAudio => ACodec is not (null or "none");

    public string DisplayName
    {
        get
        {
            var parts = new List<string> { FormatId };
            if (!string.IsNullOrEmpty(Ext)) parts.Add(Ext);

            if (HasVideo)
            {
                parts.Add(Width is { } w && Height is { } h ? $"{w}x{h}" : Resolution ?? "video");
                if (Fps is { } fps) parts.Add($"{fps.ToString("0.##", CultureInfo.InvariantCulture)}fps");
                parts.Add(VCodec!);
                parts.Add(HasAudio ? ACodec! : "音声を自動結合");
            }
            else if (HasAudio)
            {
                parts.Add("音声のみ");
                parts.Add(ACodec!);
                if (Tbr is { } tbr) parts.Add($"{tbr.ToString("0", CultureInfo.InvariantCulture)}kbps");
            }

            if (!string.IsNullOrEmpty(FormatNote)) parts.Add(FormatNote);
            if (FileSize is { } size) parts.Add(FormatBytes(size));

            return string.Join("  ", parts);
        }
    }

    // Compact label for chips and list rows, e.g. "1080p60", "720p" or "音声のみ m4a".
    public string ShortName
    {
        get
        {
            if (HasVideo)
            {
                string resolution = Height is { } h ? $"{h}p" : Resolution ?? "映像";
                return FormatCatalog.IsHighFrameRate(Fps) && Fps is { } fps
                    ? $"{resolution}{Math.Round(fps).ToString("0", CultureInfo.InvariantCulture)}"
                    : resolution;
            }

            if (HasAudio)
            {
                return string.IsNullOrEmpty(Ext) ? "音声のみ" : $"音声のみ {Ext}";
            }

            return FormatId;
        }
    }

    // Second line of a picker entry: codec family, container, size. Nothing that repeats ShortName.
    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (HasVideo)
            {
                string codec = FormatCatalog.CodecFamily(VCodec);
                if (codec.Length > 0) parts.Add(codec);
                if (!string.IsNullOrEmpty(Ext)) parts.Add(Ext);
            }
            else if (HasAudio)
            {
                string codec = FormatCatalog.CodecFamily(ACodec);
                if (codec.Length > 0) parts.Add(codec);
                if (Tbr is { } tbr) parts.Add($"{Math.Round(tbr).ToString("0", CultureInfo.InvariantCulture)}kbps");
            }

            if (FileSize is { } size) parts.Add(FormatBytes(size));
            return string.Join(" · ", parts);
        }
    }

    // A video-only stream needs the best audio merged in; combined or audio-only streams are used as-is.
    public string ToFormatSelector()
    {
        return HasVideo && !HasAudio ? $"{FormatId}+ba/{FormatId}" : FormatId;
    }

    public static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return $"{bytes.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
