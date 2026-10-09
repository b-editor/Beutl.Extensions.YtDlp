using System.Globalization;
using System.Text;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Services;

public sealed record YtDlpDownloadRequest(
    string Url,
    string OutputDirectory,
    FormatPreset Preset,
    string? CustomArguments,
    string? FFmpegLocation,
    string? ExtraArguments);

// Builds yt-dlp command lines. Kept free of process/UI concerns so it can be unit tested.
public static class YtDlpArguments
{
    public const string DownloadProgressPrefix = "[beutl:dl]";
    public const string PostProcessPrefix = "[beutl:pp]";
    public const string OutputFilePrefix = "[beutl:file]";

    // `.120B` truncates the title to 120 bytes so long titles stay inside filesystem name limits.
    public const string OutputTemplate = "%(title).120B [%(id)s].%(ext)s";

    public static IReadOnlyList<string> BuildVersionArguments() => ["--version"];

    public static IReadOnlyList<string> BuildInfoArguments(string url, string? ffmpegLocation, string? extraArguments)
    {
        var args = new List<string>
        {
            "--dump-single-json",
            "--no-playlist",
            "--no-warnings",
            // Metadata only: skip the DASH/HLS manifest round-trips, which just duplicate the
            // formats the player API already reports. Downloads use their own argument set.
            "--extractor-args", "youtube:skip=dash,hls",
        };
        AppendCommon(args, ffmpegLocation, extraArguments);
        args.Add("--");
        args.Add(url);
        return args;
    }

    public static IReadOnlyList<string> BuildSearchArguments(
        SearchProvider provider, string query, int count, string? ffmpegLocation, string? extraArguments)
    {
        var args = new List<string>
        {
            "--dump-single-json",
            // Flat entries carry title/duration/thumbnail without extracting every video.
            "--flat-playlist",
            "--no-warnings",
            // Search URLs (YouTube) have no count in them; "<prefix>N:" targets ignore this harmlessly.
            "--playlist-end",
            Math.Clamp(count, SearchProviders.MinResultCount, SearchProviders.MaxResultCount).ToString(CultureInfo.InvariantCulture),
        };
        AppendCommon(args, ffmpegLocation, extraArguments);
        args.Add("--");
        args.Add(SearchProviders.BuildQueryUrl(provider, query, count));
        return args;
    }

    public static IReadOnlyList<string> BuildDownloadArguments(YtDlpDownloadRequest request)
    {
        var args = new List<string>
        {
            "--no-playlist",
            // Warnings stay on: "formats won't be merged" and the like belong in the log.
            "--newline",
            // --print implies --quiet; --progress keeps the progress lines flowing regardless.
            "--progress",
            "--progress-template",
            $"download:{DownloadProgressPrefix}%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
            "--progress-template",
            $"postprocess:{PostProcessPrefix}%(progress.status)s|%(progress.postprocessor)s",
            // after_move runs after every post-processor, so this is the final path of the file.
            "--print",
            $"after_move:{OutputFilePrefix}%(filepath)s",
            "--windows-filenames",
            "-P",
            request.OutputDirectory,
            "-o",
            OutputTemplate,
        };
        args.AddRange(GetFormatArguments(request.Preset, request.CustomArguments, canMerge: request.FFmpegLocation != null));
        AppendCommon(args, request.FFmpegLocation, request.ExtraArguments);
        args.Add("--");
        args.Add(request.Url);
        return args;
    }

    // A format picked for one video overrides the global preset by becoming a custom selector.
    public static (FormatPreset Preset, string? CustomArguments) ResolveFormat(
        FormatPreset preset, string? customArguments, YtDlpFormat? itemFormat)
    {
        if (itemFormat == null) return (preset, customArguments);

        return (FormatPreset.Custom, $"-f \"{itemFormat.ToFormatSelector()}\" --merge-output-format mp4");
    }

    // Without a usable ffmpeg, yt-dlp cannot merge or convert: "bv*+ba" would leave the video and audio
    // as separate .fNNN files while still reporting the merged path, so pick single-file formats instead.
    public static IReadOnlyList<string> GetFormatArguments(FormatPreset preset, string? customArguments, bool canMerge = true)
    {
        if (!canMerge)
        {
            return preset switch
            {
                FormatPreset.Best => ["-f", "b", "-S", "res,codec:h264:aac,ext:mp4:m4a"],
                FormatPreset.UpTo1080p => ["-f", "b", "-S", "res:1080,codec:h264:aac,ext:mp4:m4a"],
                FormatPreset.UpTo720p => ["-f", "b", "-S", "res:720,codec:h264:aac,ext:mp4:m4a"],
                FormatPreset.AudioOnly => ["-f", "ba[ext=m4a]/ba/b"],
                FormatPreset.Custom => SplitArguments(customArguments),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        return preset switch
        {
            FormatPreset.Best =>
            [
                "-f", "bv*+ba/b",
                "-S", "res,codec:h264:aac,ext:mp4:m4a",
                "--merge-output-format", "mp4",
            ],
            FormatPreset.UpTo1080p =>
            [
                "-f", "bv*+ba/b",
                "-S", "res:1080,codec:h264:aac,ext:mp4:m4a",
                "--merge-output-format", "mp4",
            ],
            FormatPreset.UpTo720p =>
            [
                "-f", "bv*+ba/b",
                "-S", "res:720,codec:h264:aac,ext:mp4:m4a",
                "--merge-output-format", "mp4",
            ],
            FormatPreset.AudioOnly =>
            [
                "-f", "ba[ext=m4a]/ba/b",
                "--extract-audio",
                "--audio-format", "m4a",
            ],
            FormatPreset.Custom => SplitArguments(customArguments),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }

    private static void AppendCommon(List<string> args, string? ffmpegLocation, string? extraArguments)
    {
        // Output is read as UTF-8. Without this yt-dlp encodes stdout with the pipe's encoding (the ANSI
        // code page on Windows) and silently drops what it cannot encode, corrupting printed file paths.
        args.Add("--encoding");
        args.Add("utf-8");

        if (!string.IsNullOrWhiteSpace(ffmpegLocation))
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegLocation);
        }

        args.AddRange(SplitArguments(extraArguments));
    }

    // Splits a user-typed argument string the way a POSIX shell would, without invoking one.
    public static IReadOnlyList<string> SplitArguments(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var current = new StringBuilder();
        bool inToken = false;
        char quote = '\0';

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else if (c == '\\' && quote == '"' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
                {
                    current.Append(text[++i]);
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (inToken)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    inToken = false;
                }

                continue;
            }

            inToken = true;
            switch (c)
            {
                case '"':
                case '\'':
                    quote = c;
                    break;
                case '\\' when i + 1 < text.Length:
                    current.Append(text[++i]);
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (quote != '\0')
        {
            throw new FormatException("引用符が閉じられていません。");
        }

        if (inToken)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
