using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.Logging;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp.Services;

// A download history persisted as a JSON file. Two instances are used: the materials history in
// the Beutl home directory, shared by every project, and one per scene inside the scene's
// ".beutl" folder for downloads to the configured output directory. The file is written on every
// change, so closing the tab or Beutl never loses entries.
public sealed class DownloadHistoryStore
{
    public const int MaxEntries = 200;

    public const string FileName = "yt-dlp-history.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ConcurrentDictionary<string, DownloadHistoryStore> s_stores =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private readonly ILogger _logger = Log.CreateLogger<DownloadHistoryStore>();
    private bool _loaded;

    public DownloadHistoryStore(string path)
    {
        Path = path;
    }

    public string Path { get; }

    // ~/.beutl/yt-dlp-history.json: lives outside ~/.beutl/yt-dlp because the installer replaces that directory.
    public static string MaterialsPath => System.IO.Path.Combine(BeutlEnvironment.GetHomeDirectoryPath(), FileName);

    public static DownloadHistoryStore Materials => ForPath(MaterialsPath);

    // One instance per file so every tab bound to the same scene sees the same collection.
    public static DownloadHistoryStore ForPath(string path)
    {
        return s_stores.GetOrAdd(System.IO.Path.GetFullPath(path), p => new DownloadHistoryStore(p));
    }

    // Newest first. Bind to it from the UI thread only.
    public ObservableCollection<DownloadHistoryItem> Items { get; } = [];

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (!File.Exists(Path)) return;

            HistoryEntry[]? entries = JsonSerializer.Deserialize<HistoryEntry[]>(File.ReadAllText(Path), s_jsonOptions);
            if (entries == null) return;

            foreach (HistoryEntry entry in entries.OrderByDescending(e => e.DownloadedAt).Take(MaxEntries))
            {
                // Entries whose file was moved or deleted are dropped rather than shown as dead rows.
                if (string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath)) continue;

                Items.Add(entry.ToItem());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load the download history from {Path}", Path);
        }
    }

    public void Add(DownloadHistoryItem item)
    {
        EnsureLoaded();
        Items.Insert(0, item);
        while (Items.Count > MaxEntries)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        Save();
    }

    public bool Remove(DownloadHistoryItem item)
    {
        if (!Items.Remove(item)) return false;

        Save();
        return true;
    }

    private void Save()
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(Path);
            if (directory != null) Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(Items.Select(HistoryEntry.FromItem).ToArray(), s_jsonOptions);
            string temp = Path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, Path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the download history to {Path}", Path);
        }
    }

    private sealed class HistoryEntry
    {
        public string Title { get; set; } = "";

        public string FilePath { get; set; } = "";

        public long? DurationTicks { get; set; }

        public string? Url { get; set; }

        public DateTimeOffset DownloadedAt { get; set; }

        public static HistoryEntry FromItem(DownloadHistoryItem item) => new()
        {
            Title = item.Title,
            FilePath = item.FilePath,
            DurationTicks = item.Duration?.Ticks,
            Url = item.Url,
            DownloadedAt = item.DownloadedAt,
        };

        public DownloadHistoryItem ToItem() => new(
            Title,
            FilePath,
            DurationTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null,
            Url,
            DownloadedAt);
    }
}
