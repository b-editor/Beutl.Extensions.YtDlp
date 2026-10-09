using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Beutl.Extensions.YtDlp.ViewModels;

// One row of the video list: the search result (or URL-fetched video) plus its own format choice
// and download state, so the list itself is the download UI.
public sealed class VideoItem : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    public VideoItem(YtDlpSearchResult result, YtDlpTabViewModel owner)
    {
        Result = result;
        Owner = owner;

        HasFormats = Formats.Select(f => f.Length > 0).ToReadOnlyReactivePropertySlim().AddTo(_disposables);
        HasSelectedFormat = SelectedFormat.Select(f => f != null).ToReadOnlyReactivePropertySlim().AddTo(_disposables);
        // The chip always states what this row will download with: the shared preset or an override.
        FormatChipText = SelectedFormat
            .CombineLatest(Owner.SelectedPreset, (format, preset) =>
                format != null ? $"この動画: {format.ShortName}" : $"形式: {preset?.ShortName ?? ""}")
            .ToReadOnlyReactivePropertySlim("")
            .AddTo(_disposables);
        FormatChipToolTip = SelectedFormat
            .CombineLatest(Owner.SelectedPreset, (format, preset) =>
                format != null
                    ? $"この動画だけ「{format.DisplayName}」でダウンロードします。クリックで変更"
                    : $"共通の形式「{preset?.Name ?? ""}」でダウンロードします。クリックでこの動画だけ変更")
            .ToReadOnlyReactivePropertySlim("")
            .AddTo(_disposables);
        HasFormatsMessage = FormatsMessage.Select(m => !string.IsNullOrEmpty(m)).ToReadOnlyReactivePropertySlim().AddTo(_disposables);
        HasStatus = IsActive
            .CombineLatest(StatusText, (active, status) => active || !string.IsNullOrEmpty(status))
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);
    }

    public YtDlpSearchResult Result { get; }

    public YtDlpTabViewModel Owner { get; }

    public string Title => Result.DisplayTitle;

    public string Subtitle => Result.Subtitle;

    public string Url => Result.Url;

    public ReactiveProperty<Bitmap?> Thumbnail { get; } = new();

    public ReactiveProperty<YtDlpFormat[]> Formats { get; } = new([]);

    public ReactiveProperty<YtDlpFormat?> SelectedFormat { get; } = new();

    public ReactiveProperty<bool> IsLoadingFormats { get; } = new();

    public ReactiveProperty<string> FormatsMessage { get; } = new("");

    public ReadOnlyReactivePropertySlim<bool> HasFormats { get; }

    public ReadOnlyReactivePropertySlim<bool> HasSelectedFormat { get; }

    public ReadOnlyReactivePropertySlim<bool> HasFormatsMessage { get; }

    public ReadOnlyReactivePropertySlim<string> FormatChipText { get; }

    public ReadOnlyReactivePropertySlim<string> FormatChipToolTip { get; }

    public bool FormatsLoaded { get; private set; }

    public ReactiveProperty<bool> IsActive { get; } = new();

    public ReactiveProperty<bool> IsCompleted { get; } = new();

    public ReactiveProperty<bool> IsFailed { get; } = new();

    public ReactiveProperty<bool> IsIndeterminate { get; } = new();

    public ReactiveProperty<double> Progress { get; } = new();

    public ReactiveProperty<string> StatusText { get; } = new("");

    public ReadOnlyReactivePropertySlim<bool> HasStatus { get; }

    public Task Download() => Owner.DownloadItem(this, DownloadDestination.OutputDirectory);

    public Task DownloadToMaterials() => Owner.DownloadItem(this, DownloadDestination.Materials);

    public void Cancel() => Owner.Cancel();

    public Task LoadFormats() => Owner.LoadFormatsAsync(this);

    public void ClearFormat() => SelectedFormat.Value = null;

    public async Task LoadThumbnailAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Result.ThumbnailUrl)) return;

        Bitmap? bitmap = await ThumbnailLoader.LoadAsync(Result.ThumbnailUrl, ct);
        if (bitmap == null) return;

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            return;
        }

        Thumbnail.Value = bitmap;
    }

    internal void SetFormats(IReadOnlyList<YtDlpFormat> formats)
    {
        Formats.Value = FormatCatalog.Summarize(formats).ToArray();
        FormatsLoaded = true;
        FormatsMessage.Value = Formats.Value.Length == 0 ? "利用可能なフォーマットがありません。" : "";
    }

    internal void BeginDownload()
    {
        IsCompleted.Value = false;
        IsFailed.Value = false;
        IsIndeterminate.Value = true;
        Progress.Value = 0;
        StatusText.Value = "ダウンロードを開始しています...";
        IsActive.Value = true;
    }

    internal void CompleteDownload(string filePath, DownloadDestination destination)
    {
        IsActive.Value = false;
        IsIndeterminate.Value = false;
        Progress.Value = 100;
        IsCompleted.Value = true;
        StatusText.Value = $"{DownloadDestinations.GetLabel(destination)}に保存: {Path.GetFileName(filePath)}";
    }

    internal void FailDownload(string message, bool canceled)
    {
        IsActive.Value = false;
        IsIndeterminate.Value = false;
        IsFailed.Value = !canceled;
        StatusText.Value = canceled ? "キャンセルしました" : $"失敗: {message}";
    }

    public void Dispose()
    {
        _disposables.Dispose();
        Bitmap? bitmap = Thumbnail.Value;
        Thumbnail.Value = null;
        bitmap?.Dispose();
        Thumbnail.Dispose();
    }
}
