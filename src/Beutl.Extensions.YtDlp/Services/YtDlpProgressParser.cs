using System.Globalization;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Services;

public static class YtDlpProgressParser
{
    // Returns null for lines that are not one of our templated lines (ordinary yt-dlp log output).
    public static YtDlpOutputEvent? Parse(string line)
    {
        string trimmed = line.Trim();

        if (trimmed.StartsWith(YtDlpArguments.OutputFilePrefix, StringComparison.Ordinal))
        {
            string path = trimmed[YtDlpArguments.OutputFilePrefix.Length..].Trim();
            return path.Length == 0 ? null : new OutputFileEvent(path);
        }

        if (trimmed.StartsWith(YtDlpArguments.DownloadProgressPrefix, StringComparison.Ordinal))
        {
            string[] fields = trimmed[YtDlpArguments.DownloadProgressPrefix.Length..].Split('|');
            if (fields.Length < 6) return null;

            long? total = ParseInt64(fields[2]) ?? ParseInt64(fields[3]);
            return new DownloadProgressEvent(
                Status: fields[0].Trim(),
                DownloadedBytes: ParseInt64(fields[1]),
                TotalBytes: total,
                Speed: ParseDouble(fields[4]),
                Eta: ParseInt64(fields[5]) is { } eta ? (int)Math.Min(eta, int.MaxValue) : null);
        }

        if (trimmed.StartsWith(YtDlpArguments.PostProcessPrefix, StringComparison.Ordinal))
        {
            string[] fields = trimmed[YtDlpArguments.PostProcessPrefix.Length..].Split('|');
            if (fields.Length < 2) return null;

            return new PostProcessEvent(fields[0].Trim(), fields[1].Trim());
        }

        return null;
    }

    // yt-dlp renders missing values as "NA" and floats such as "1234.0".
    private static double? ParseDouble(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text == "NA" || text == "None") return null;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
               && double.IsFinite(value)
            ? value
            : null;
    }

    private static long? ParseInt64(string text)
    {
        double? value = ParseDouble(text);
        return value is { } v ? (long)v : null;
    }
}
