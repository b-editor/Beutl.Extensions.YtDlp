using System.Text.RegularExpressions;

namespace Beutl.Extensions.YtDlp.Services;

// Decides whether the text typed into the search box is a URL (fetch it) or a keyword (search it).
public static partial class UrlDetector
{
    // Scheme-less forms such as "www.youtube.com/watch?v=x" or "youtu.be/x". The last label must be
    // alphabetic so a file-like keyword such as "song.mp3" is still treated as a search.
    [GeneratedRegex(@"^(www\.)?([a-z0-9-]+\.)+[a-z]{2,}(:[0-9]+)?(/\S*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SchemelessUrlRegex();

    public static bool IsUrl(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace)) return false;

        if (HasHttpScheme(trimmed))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out _);
        }

        return SchemelessUrlRegex().IsMatch(trimmed);
    }

    public static string Normalize(string text)
    {
        string trimmed = text.Trim();
        return HasHttpScheme(trimmed) ? trimmed : "https://" + trimmed;
    }

    private static bool HasHttpScheme(string text)
    {
        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
