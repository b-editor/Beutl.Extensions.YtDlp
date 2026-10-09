using System.Text.Json;

namespace Beutl.Extensions.YtDlp.Models;

// Lenient readers for yt-dlp JSON, where any field may be absent or null.
internal static class JsonElementHelpers
{
    public static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static double? GetDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }

    public static int? GetInt32(JsonElement element, string name)
    {
        double? d = GetDouble(element, name);
        return d is { } v && v >= int.MinValue && v <= int.MaxValue ? (int)v : null;
    }

    public static long? GetInt64(JsonElement element, string name)
    {
        double? d = GetDouble(element, name);
        return d is { } v ? (long)v : null;
    }

    public static bool? GetBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
