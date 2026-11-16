using System.Text.Json.Nodes;
using Beutl.Editor.Models;
using Beutl.Editor.Services;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.ProjectSystem;
using Reactive.Bindings;

namespace Beutl.Extensions.YtDlp.Tests;

public class TimelineImportTests
{
    private string _root = null!;
    private string _filePath = null!;
    private DeferredElementAdder _adder = null!;
    private TestEditorClock _clock = null!;
    private YtDlpTabViewModel _viewModel = null!;
    private TestEditorContext _editor = null!;
    private YtDlpExtension _extension = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _filePath = Path.Combine(_root, "download.mp4");
        File.WriteAllBytes(_filePath, []);
        _adder = new DeferredElementAdder();
        _clock = new TestEditorClock();
        _editor = new TestEditorContext(
            new Scene { Uri = new Uri(Path.Combine(_root, "scene.beutl")) },
            _clock, _adder);
        _extension = new YtDlpExtension();
        _viewModel = new YtDlpTabViewModel(new YtDlpTabExtension(), _editor, _extension.Environment);
        _editor.Tab = _viewModel;
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel.Dispose();
        _extension.Unload();
        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task BrowserMenuWaitsForConfirmationAndUsesOptionsForThisDownload()
    {
        UseYtDlpScript("""
            for arg do
                if [ "$previous" = '-P' ]; then directory="$arg"; fi
                if [ "$previous" = '-f' ]; then format="$arg"; fi
                previous="$arg"
            done
            printf '%s\n' "$arg" >> "$(dirname "$0")/urls"
            if [ "$1" = '--dump-single-json' ]; then
                printf '%s\n' '{"title":"Test video","duration":12.5,"webpage_url":"https://example.org/canonical","formats":[{"format_id":"140","vcodec":"none","acodec":"mp4a","ext":"m4a"}]}'
            else
                printf '%s\n' "$format" > "$(dirname "$0")/format"
                printf 'media' > "$directory/clip.mp4"
                printf '[beutl:file]%s/clip.mp4\n' "$directory"
            fi
            """);
        string directory = Path.Combine(_root, "chosen");
        _viewModel.OutputDirectory.Value = Path.Combine(_root, "default");
        var prompted = new TaskCompletionSource<DownloadOptions>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmed = new TaskCompletionSource<DownloadOptions?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.ChoosePageDownloadOptions = (video, defaults, token) =>
        {
            Assert.That(video.Title, Is.EqualTo("Test video"));
            Assert.That(defaults.OutputDirectory, Is.EqualTo(_viewModel.OutputDirectory.Value));
            Assert.That(defaults.AddToTimeline, Is.True);
            prompted.SetResult(defaults with { Format = video.Formats.Value.Single() });
            return confirmed.Task.WaitAsync(token);
        };
        var page = new Uri("https://www.youtube.com/watch?v=test&t=30");

        var pending = _extension.TryDownloadPageAsync(_editor, page, null, CancellationToken.None);
        DownloadOptions options = await prompted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(File.ReadAllLines(Path.Combine(_root, "urls")), Is.EqualTo(new[] { page.AbsoluteUri }));
        Assert.That(_viewModel.History, Is.Empty);
        confirmed.SetResult(options with { OutputDirectory = directory, AddToTimeline = false });

        Assert.That(await pending, Is.True);
        Assert.That(File.ReadAllLines(Path.Combine(_root, "urls")), Is.EqualTo(new[] { page.AbsoluteUri, page.AbsoluteUri }));
        DownloadHistoryItem item = _viewModel.History.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Test video"));
            Assert.That(item.Duration, Is.EqualTo(TimeSpan.FromSeconds(12.5)));
            Assert.That(item.FilePath, Is.EqualTo(Path.Combine(directory, "clip.mp4")));
            Assert.That(File.ReadAllText(item.FilePath), Is.EqualTo("media"));
            Assert.That(item.Url, Is.EqualTo(page.AbsoluteUri));
            Assert.That(File.ReadAllText(Path.Combine(_root, "format")).Trim(), Is.EqualTo("140"));
            Assert.That(_viewModel.OutputDirectory.Value, Is.EqualTo(Path.Combine(_root, "default")));
            Assert.That(_viewModel.AddToTimelineAfterDownload.Value, Is.True);
            Assert.That(_adder.Description, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelingBrowserOptionsFinishesWithoutDownloading(bool closeBrowser)
    {
        UseYtDlpScript("""
            if [ "$1" = '--dump-single-json' ]; then
                printf '%s\n' '{"title":"Test video"}'
            else
                touch "$0.download"
            fi
            """);
        using var browser = new CancellationTokenSource();
        _viewModel.ChoosePageDownloadOptions = (_, _, token) =>
        {
            if (closeBrowser)
            {
                browser.Cancel();
                token.ThrowIfCancellationRequested();
            }
            return Task.FromResult<DownloadOptions?>(null);
        };

        bool handled = await _extension.TryDownloadPageAsync(_editor, new Uri("https://example.org/video"), null, browser.Token);

        Assert.That(handled, Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "yt-dlp.download")), Is.False);
        Assert.That(_viewModel.History, Is.Empty);
        Assert.That(_viewModel.IsBusy.Value, Is.False);
    }

    [Test]
    public async Task DownloadFromToolTabStartsDirectly()
    {
        UseYtDlpScript("""
            for arg do
                if [ "$previous" = '-P' ]; then directory="$arg"; fi
                previous="$arg"
            done
            touch "$directory/tab.mp4"
            printf '[beutl:file]%s/tab.mp4\n' "$directory"
            """);
        _viewModel.OutputDirectory.Value = _root;
        _viewModel.AddToTimelineAfterDownload.Value = false;
        _viewModel.ChoosePageDownloadOptions = (_, _, _) => throw new AssertionException("Unexpected browser dialog");
        using var item = new VideoItem(new YtDlpSearchResult { Url = "https://example.org/video" }, _viewModel);

        await item.Download();

        Assert.That(_viewModel.History.Single().FilePath, Is.EqualTo(Path.Combine(_root, "tab.mp4")));
    }

    [Test]
    public async Task UnsupportedBrowserPageLetsBeutlHandleTheDownload()
    {
        UseYtDlpScript("""
            printf '%s\n' 'ERROR: Unsupported URL: https://example.org/page' >&2
            exit 1
            """);

        bool handled = await _extension.TryDownloadPageAsync(_editor, new Uri("https://example.org/page"), null, CancellationToken.None);

        Assert.That(handled, Is.False);
        Assert.That(_viewModel.SearchResults.Value, Is.Empty);
    }

    [Test]
    public void PlaylistPageDoesNotStartABulkDownload()
    {
        UseYtDlpScript("""
            printf '%s\n' '{"_type":"playlist","entries":[]}'
            """);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _extension.TryDownloadPageAsync(_editor, new Uri("https://example.org/playlist"), null, CancellationToken.None));
        Assert.That(_viewModel.SearchResults.Value, Is.Empty);
    }

    private void UseYtDlpScript(string commands)
    {
        string executable = Path.Combine(_root, "yt-dlp");
        File.WriteAllText(executable, "#!/bin/sh\n" + commands + "\n");
        if (OperatingSystem.IsWindows())
            Assert.Ignore("The fixture uses a POSIX executable.");
        else
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _extension.Environment.ExecutablePath.Value = executable;
        _extension.Environment.HasChecked.Value = true;
    }

    [TestCase(12, 12)]
    [TestCase(12.3456789, 12.3456789)]
    [TestCase(0, 5)]
    [TestCase(-1, 5)]
    public async Task AddToTimeline_WaitsForImportAndPreservesPlacement(double durationSeconds, double expectedLengthSeconds)
    {
        var item = new DownloadHistoryItem("Video", _filePath, TimeSpan.FromSeconds(durationSeconds), "https://example.com/video", DateTimeOffset.Now);

        Task import = _viewModel.AddToTimeline(item);

        Assert.That(import.IsCompleted, Is.False);
        ElementDescription description = _adder.Description!;
        Assert.Multiple(() =>
        {
            Assert.That(description.Source, Is.TypeOf<ElementSource.File>());
            Assert.That(((ElementSource.File)description.Source).FileName, Is.EqualTo(_filePath));
            Assert.That(description.Start, Is.EqualTo(MediaTime.FromSeconds(3)));
            Assert.That(description.Length, Is.EqualTo(MediaTime.FromSeconds(expectedLengthSeconds)));
            Assert.That(description.Layer, Is.EqualTo(0));
        });

        _adder.Completion.SetResult(ElementAddResult.Succeeded([new ElementAddItemResult(description, new Element(), [])]));
        await import.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task AddToTimeline_PreservesThePlayheadAtFlickPrecision()
    {
        var start = new MediaTime(1);
        _clock.Seek(start);
        var item = new DownloadHistoryItem("Video", _filePath, null);

        Task import = _viewModel.AddToTimeline(item);

        ElementDescription description = _adder.Description!;
        Assert.Multiple(() =>
        {
            Assert.That(description.Start, Is.EqualTo(start));
            Assert.That(description.Length, Is.EqualTo(MediaTime.FromSeconds(5)));
        });
        _adder.Completion.SetResult(ElementAddResult.Succeeded([new ElementAddItemResult(description, new Element(), [])]));
        await import.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Preview8TabState_RoundTripsWithoutMigration()
    {
        var saved = JsonNode.Parse("""
            {
                "SelectedTabIndex": 1,
                "SearchQuery": "音楽",
                "SelectedSearchProvider": "SoundCloud",
                "SelectedPreset": "AudioOnly",
                "CustomFormatArguments": "-f bestaudio",
                "OutputDirectory": "/downloads/音楽",
                "AddToTimelineAfterDownload": false
            }
            """)!.AsObject();

        _viewModel.ReadFromJson(saved);
        var restored = new JsonObject();
        _viewModel.WriteToJson(restored);

        Assert.That(JsonNode.DeepEquals(restored, saved), Is.True);
    }

    [TestCase("Niconico", "ニコニコ動画")]
    [TestCase("Bilibili", "Bilibili")]
    public void SearchProvider_NewSitesCanBeSelectedAndRestored(string provider, string displayName)
    {
        var saved = new JsonObject { ["SelectedSearchProvider"] = provider };
        _viewModel.ReadFromJson(saved);
        var restored = new JsonObject();
        _viewModel.WriteToJson(restored);

        Assert.That(_viewModel.SelectedSearchProvider.Value.Name, Is.EqualTo(displayName));
        Assert.That((string?)restored["SelectedSearchProvider"], Is.EqualTo(provider));
    }

    [Test]
    public async Task AddToTimeline_CanceledImportCompletesWithoutAnError()
    {
        var item = new DownloadHistoryItem("Video", _filePath, TimeSpan.FromSeconds(12), "https://example.com/video", DateTimeOffset.Now);
        Task import = _viewModel.AddToTimeline(item);

        _adder.Completion.SetCanceled();

        await import.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(import.IsCompletedSuccessfully, Is.True);
    }

    private sealed class DeferredElementAdder : IElementAdder
    {
        public IElementSourceHandlerRegistry SourceHandlers => throw new NotSupportedException();

        public TaskCompletionSource<ElementAddResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ElementDescription? Description { get; private set; }

        public ValueTask<ElementAddResult> AddAsync(IReadOnlyList<ElementDescription> descriptions, CancellationToken cancellationToken)
        {
            Description = descriptions.Single();
            return new ValueTask<ElementAddResult>(Completion.Task);
        }
    }

    private sealed class TestEditorClock : IEditorClock
    {
        private readonly ReactiveProperty<MediaTime> _currentTime = new(MediaTime.FromSeconds(3));

        public IReadOnlyReactiveProperty<MediaTime> CurrentTime => _currentTime;

        public IReadOnlyReactiveProperty<MediaTime> MaximumTime { get; } = new ReactiveProperty<MediaTime>(MediaTime.FromSeconds(60));

        public void Seek(MediaTime time) => _currentTime.Value = time;
    }

    private sealed class TestEditorContext(Scene scene, IEditorClock clock, IElementAdder adder) : IEditorContext
    {
        public IToolContext? Tab { get; set; }

        public CoreObject Object => scene;

        public EditorExtension Extension => throw new NotSupportedException();

        public IReactiveProperty<bool> IsEnabled { get; } = new ReactiveProperty<bool>(true);

        public object? GetService(Type serviceType) => serviceType == typeof(Scene) ? scene
            : serviceType == typeof(IEditorClock) ? clock
            : serviceType == typeof(IElementAdder) ? adder
            : null;

        public T? FindToolTab<T>(Func<T, bool> condition) where T : IToolContext => default;

        public T? FindToolTab<T>() where T : IToolContext => Tab is T tab ? tab : default;

        public bool OpenToolTab(IToolContext item) => ReferenceEquals(Tab, item);

        public void CloseToolTab(IToolContext item) { }
    }
}
