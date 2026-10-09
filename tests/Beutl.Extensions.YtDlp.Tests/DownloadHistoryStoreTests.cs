using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.ViewModels;

namespace Beutl.Extensions.YtDlp.Tests;

public class DownloadHistoryStoreTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string CreateFile(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Test]
    public void Add_PersistsAndReloadsNewestFirst()
    {
        string historyPath = Path.Combine(_root, ".beutl", "yt-dlp-history.json");
        string first = CreateFile("first.mp4");
        string second = CreateFile("second.mp4");
        var store = new DownloadHistoryStore(historyPath);

        store.Add(new DownloadHistoryItem("First", first, TimeSpan.FromSeconds(10), "https://a", new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero)));
        store.Add(new DownloadHistoryItem("Second", second, null, "https://b", new DateTimeOffset(2026, 9, 8, 1, 0, 0, TimeSpan.Zero)));

        Assert.That(File.Exists(historyPath), Is.True, "the .beutl folder is created on demand");

        var reloaded = new DownloadHistoryStore(historyPath);
        reloaded.EnsureLoaded();

        Assert.That(reloaded.Items.Select(i => i.Title), Is.EqualTo(new[] { "Second", "First" }));
        Assert.That(reloaded.Items[1].Duration, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(reloaded.Items[1].Url, Is.EqualTo("https://a"));
    }

    [Test]
    public void EnsureLoaded_DropsEntriesWhoseFileIsGone()
    {
        string historyPath = Path.Combine(_root, "history.json");
        string kept = CreateFile("kept.mp4");
        string gone = CreateFile("gone.mp4");
        var store = new DownloadHistoryStore(historyPath);
        store.Add(new DownloadHistoryItem("Kept", kept, null));
        store.Add(new DownloadHistoryItem("Gone", gone, null));
        File.Delete(gone);

        var reloaded = new DownloadHistoryStore(historyPath);
        reloaded.EnsureLoaded();

        Assert.That(reloaded.Items.Select(i => i.Title), Is.EqualTo(new[] { "Kept" }));
    }

    [Test]
    public void Remove_UpdatesTheFileAndReportsWhetherItWasFound()
    {
        string historyPath = Path.Combine(_root, "history.json");
        string file = CreateFile("a.mp4");
        var store = new DownloadHistoryStore(historyPath);
        var item = new DownloadHistoryItem("A", file, null);
        store.Add(item);

        Assert.That(store.Remove(item), Is.True);
        Assert.That(store.Remove(item), Is.False);

        var reloaded = new DownloadHistoryStore(historyPath);
        reloaded.EnsureLoaded();
        Assert.That(reloaded.Items, Is.Empty);
        Assert.That(File.Exists(file), Is.True, "removing a history entry must not delete the download");
    }

    [Test]
    public void Add_CapsTheNumberOfEntries()
    {
        string historyPath = Path.Combine(_root, "history.json");
        string file = CreateFile("a.mp4");
        var store = new DownloadHistoryStore(historyPath);

        for (int i = 0; i < DownloadHistoryStore.MaxEntries + 5; i++)
        {
            store.Add(new DownloadHistoryItem($"Item {i}", file, null));
        }

        Assert.That(store.Items, Has.Count.EqualTo(DownloadHistoryStore.MaxEntries));
        Assert.That(store.Items[0].Title, Is.EqualTo($"Item {DownloadHistoryStore.MaxEntries + 4}"));
    }

    [Test]
    public void EnsureLoaded_ToleratesCorruptFile()
    {
        string historyPath = Path.Combine(_root, "history.json");
        File.WriteAllText(historyPath, "{ not json");
        var store = new DownloadHistoryStore(historyPath);

        Assert.DoesNotThrow(store.EnsureLoaded);
        Assert.That(store.Items, Is.Empty);
    }

    [Test]
    public void ForPath_ReturnsOneInstancePerFile()
    {
        string historyPath = Path.Combine(_root, "history.json");

        DownloadHistoryStore a = DownloadHistoryStore.ForPath(historyPath);
        DownloadHistoryStore b = DownloadHistoryStore.ForPath(Path.Combine(_root, ".", "history.json"));
        DownloadHistoryStore other = DownloadHistoryStore.ForPath(Path.Combine(_root, "other.json"));

        Assert.That(b, Is.SameAs(a));
        Assert.That(other, Is.Not.SameAs(a));
    }
}
