using Beutl.Editor.Models;
using Beutl.Editor.Services;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.ProjectSystem;
using Reactive.Bindings;

namespace Beutl.Extensions.YtDlp.Tests;

public class TimelineImportTests
{
    private string _root = null!;
    private string _filePath = null!;
    private DeferredElementAdder _adder = null!;
    private YtDlpTabViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Beutl.Extensions.YtDlp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _filePath = Path.Combine(_root, "download.mp4");
        File.WriteAllBytes(_filePath, []);
        _adder = new DeferredElementAdder();
        var context = new TestEditorContext(
            new Scene { Uri = new Uri(Path.Combine(_root, "scene.beutl")) },
            new TestEditorClock(), _adder);
        _viewModel = new YtDlpTabViewModel(new YtDlpTabExtension(), context, new YtDlpEnvironment(new YtDlpSettings()));
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [TestCase(12, 12)]
    [TestCase(0, 5)]
    public async Task AddToTimeline_WaitsForImportAndPreservesPlacement(int durationSeconds, int expectedLengthSeconds)
    {
        var item = new DownloadHistoryItem("Video", _filePath, TimeSpan.FromSeconds(durationSeconds), "https://example.com/video", DateTimeOffset.Now);

        Task import = _viewModel.AddToTimeline(item);

        Assert.That(import.IsCompleted, Is.False);
        ElementDescription description = _adder.Description!;
        Assert.Multiple(() =>
        {
            Assert.That(description.Source, Is.TypeOf<ElementSource.File>());
            Assert.That(((ElementSource.File)description.Source).FileName, Is.EqualTo(_filePath));
            Assert.That(description.Start, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(description.Length, Is.EqualTo(TimeSpan.FromSeconds(expectedLengthSeconds)));
            Assert.That(description.Layer, Is.EqualTo(0));
        });

        _adder.Completion.SetResult(ElementAddResult.Succeeded([new ElementAddItemResult(description, new Element(), [])]));
        await import.WaitAsync(TimeSpan.FromSeconds(5));
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
        public IReactiveProperty<TimeSpan> CurrentTime { get; } = new ReactiveProperty<TimeSpan>(TimeSpan.FromSeconds(3));

        public IReadOnlyReactiveProperty<TimeSpan> MaximumTime { get; } = new ReactiveProperty<TimeSpan>(TimeSpan.FromMinutes(1));
    }

    private sealed class TestEditorContext(Scene scene, IEditorClock clock, IElementAdder adder) : IEditorContext
    {
        public CoreObject Object => scene;

        public EditorExtension Extension => throw new NotSupportedException();

        public IReactiveProperty<bool> IsEnabled { get; } = new ReactiveProperty<bool>(true);

        public object? GetService(Type serviceType) => serviceType == typeof(Scene) ? scene
            : serviceType == typeof(IEditorClock) ? clock
            : serviceType == typeof(IElementAdder) ? adder
            : null;

        public T? FindToolTab<T>(Func<T, bool> condition) where T : IToolContext => default;

        public T? FindToolTab<T>() where T : IToolContext => default;

        public bool OpenToolTab(IToolContext item) => false;

        public void CloseToolTab(IToolContext item) { }
    }
}
