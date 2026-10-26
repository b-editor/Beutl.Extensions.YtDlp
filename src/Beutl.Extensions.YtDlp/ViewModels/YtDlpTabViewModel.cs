using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Beutl.Editor;
using Beutl.Editor.Models;
using Beutl.Editor.Services;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Logging;
using Beutl.ProjectSystem;
using Beutl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace Beutl.Extensions.YtDlp.ViewModels;

public sealed class YtDlpTabViewModel : IToolContext
{
    private const int MaxLogLines = 200;
    private const string SearchHint = "キーワードまたは URL を入力して Enter キーを押します";
    private static readonly string[] s_audioExtensions =
        [".m4a", ".mp3", ".aac", ".opus", ".ogg", ".oga", ".wav", ".flac", ".wma"];

    private readonly ILogger _logger = Log.CreateLogger<YtDlpTabViewModel>();
    private readonly IEditorContext _editorContext;
    private readonly Scene _scene;
    private readonly IEditorClock _clock;
    private readonly YtDlpEnvironment _environment;
    private readonly YtDlpSettings _settings;
    private readonly CompositeDisposable _disposables = [];
    private readonly List<string> _logLines = [];
    private readonly DownloadHistoryStore _materialsHistoryStore;
    private readonly DownloadHistoryStore _sceneHistoryStore;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _thumbnailCts;
    private int _busy;

    public YtDlpTabViewModel(ToolTabExtension extension, IEditorContext editorContext, YtDlpEnvironment environment)
    {
        Extension = extension;
        _editorContext = editorContext;
        _environment = environment;
        _settings = environment.Settings;
        _scene = editorContext.GetRequiredService<Scene>();
        _clock = editorContext.GetRequiredService<IEditorClock>();

        Presets = Enum.GetValues<FormatPreset>().Select(FormatPresetItem.Create).ToArray();
        SelectedPreset = new ReactiveProperty<FormatPresetItem>(FindPreset(_settings.DefaultPreset.Value))
            .AddTo(_disposables);
        IsCustomPreset = SelectedPreset
            .Select(p => p?.Preset == FormatPreset.Custom)
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);

        OutputDirectory = new ReactiveProperty<string>(_settings.OutputDirectory.Value ?? "").AddTo(_disposables);
        DefaultOutputDirectory = GetProjectOutputDirectory();
        SessionHistoryHeader = OutputDirectory
            .Select(_ => $"保存先（{ResolveOutputDirectory()}）")
            .ToReadOnlyReactivePropertySlim("")
            .AddTo(_disposables);

        SearchProviders = Enum.GetValues<SearchProvider>().Select(SearchProviderItem.Create).ToArray();
        SelectedSearchProvider = new ReactiveProperty<SearchProviderItem>(SearchProviders[0]).AddTo(_disposables);

        IsYtDlpMissing = _environment.HasChecked
            .CombineLatest(_environment.IsAvailable, (checkedOnce, available) => checkedOnce && !available)
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);
        IsCheckingYtDlp = _environment.HasChecked.Select(c => !c).ToReadOnlyReactivePropertySlim().AddTo(_disposables);

        HasSearchResults = SearchResults.Select(r => r.Length > 0).ToReadOnlyReactivePropertySlim().AddTo(_disposables);
        IsUrlInput = SearchQuery.Select(UrlDetector.IsUrl).ToReadOnlyReactivePropertySlim().AddTo(_disposables);
        BusyMessage = IsUrlInput
            .Select(u => u ? "動画情報を取得中..." : "検索中...")
            .ToReadOnlyReactivePropertySlim("検索中...")
            .AddTo(_disposables);
        IsSearchListVisible = HasSearchResults
            .CombineLatest(IsSearching, (has, searching) => has && !searching)
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);
        IsSearchEmptyStateVisible = HasSearchResults
            .CombineLatest(IsSearching, (has, searching) => !has && !searching)
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);

        HasLog = LogText.Select(t => !string.IsNullOrEmpty(t)).ToReadOnlyReactivePropertySlim().AddTo(_disposables);

        _materialsHistoryStore = DownloadHistoryStore.Materials;
        _materialsHistoryStore.EnsureLoaded();
        MaterialsHistory = _materialsHistoryStore.Items;
        _sceneHistoryStore = DownloadHistoryStore.ForPath(GetSceneHistoryPath());
        _sceneHistoryStore.EnsureLoaded();
        History = _sceneHistoryStore.Items;
        HasSessionHistory = History.CollectionChangedAsObservable()
            .Select(_ => History.Count > 0)
            .ToReadOnlyReactivePropertySlim(History.Count > 0)
            .AddTo(_disposables);
        HasMaterialsHistory = MaterialsHistory.CollectionChangedAsObservable()
            .Select(_ => MaterialsHistory.Count > 0)
            .ToReadOnlyReactivePropertySlim(MaterialsHistory.Count > 0)
            .AddTo(_disposables);
        HasHistory = HasSessionHistory
            .CombineLatest(HasMaterialsHistory, (session, materials) => session || materials)
            .ToReadOnlyReactivePropertySlim()
            .AddTo(_disposables);
    }

    public ToolTabExtension Extension { get; }

    public IReactiveProperty<bool> IsSelected { get; } = new ReactiveProperty<bool>();

    public IReadOnlyReactiveProperty<string> Header { get; } = new ReactivePropertySlim<string>("yt-dlp");

    public IReadOnlyReactiveProperty<bool> IsYtDlpAvailable => _environment.IsAvailable;

    public IReadOnlyReactiveProperty<string?> YtDlpVersion => _environment.Version;

    // True only after detection finished without finding yt-dlp (drives the warning banner).
    public ReadOnlyReactivePropertySlim<bool> IsYtDlpMissing { get; }

    public ReadOnlyReactivePropertySlim<bool> IsCheckingYtDlp { get; }

    public IReadOnlyReactiveProperty<bool> IsSlowBuild => _environment.IsSlowBuild;

    // 0 = 検索, 1 = 履歴
    public ReactiveProperty<int> SelectedTabIndex { get; } = new(0);

    public ReactiveProperty<string> SearchQuery { get; } = new("");

    public SearchProviderItem[] SearchProviders { get; }

    public ReactiveProperty<SearchProviderItem> SelectedSearchProvider { get; }

    public ReadOnlyReactivePropertySlim<bool> IsUrlInput { get; }

    public ReactiveProperty<bool> IsSearching { get; } = new();

    public ReadOnlyReactivePropertySlim<string> BusyMessage { get; }

    public ReactiveProperty<string> SearchMessage { get; } = new(SearchHint);

    public ReactiveProperty<VideoItem[]> SearchResults { get; } = new([]);

    public ReadOnlyReactivePropertySlim<bool> HasSearchResults { get; }

    public ReadOnlyReactivePropertySlim<bool> IsSearchListVisible { get; }

    public ReadOnlyReactivePropertySlim<bool> IsSearchEmptyStateVisible { get; }

    public FormatPresetItem[] Presets { get; }

    public ReactiveProperty<FormatPresetItem> SelectedPreset { get; }

    public ReadOnlyReactivePropertySlim<bool> IsCustomPreset { get; }

    public ReactiveProperty<string> CustomFormatArguments { get; } = new("");

    public ReactiveProperty<string> OutputDirectory { get; }

    public string DefaultOutputDirectory { get; }

    public ReactiveProperty<bool> AddToTimelineAfterDownload { get; } = new(true);

    public ReactiveProperty<bool> IsBusy { get; } = new();

    public ReactiveProperty<VideoItem?> ActiveItem { get; } = new();

    public ReactiveProperty<string> LogText { get; } = new("");

    public ReadOnlyReactivePropertySlim<bool> HasLog { get; }

    // Downloads to the configured output directory: persisted in the scene's ".beutl" folder.
    public ObservableCollection<DownloadHistoryItem> History { get; }

    // Downloads to ~/.beutl/materials: shared by every project and persisted in the Beutl home.
    public ObservableCollection<DownloadHistoryItem> MaterialsHistory { get; }

    public ReadOnlyReactivePropertySlim<bool> HasSessionHistory { get; }

    // "保存先（<current output directory>）" for the history page.
    public ReadOnlyReactivePropertySlim<string> SessionHistoryHeader { get; }

    public ReadOnlyReactivePropertySlim<bool> HasMaterialsHistory { get; }

    public ReadOnlyReactivePropertySlim<bool> HasHistory { get; }

    // Keyword → provider search; URL → fetch that one video. Both end up as rows in SearchResults.
    public async Task Search()
    {
        if (!TryBegin()) return;

        bool isUrl = false;
        try
        {
            string query = SearchQuery.Value?.Trim() ?? "";
            if (query.Length == 0)
            {
                ShowWarning("キーワードまたは URL を入力してください。");
                return;
            }

            if (!TryGetRunner(out YtDlpRunner? runner)) return;

            isUrl = UrlDetector.IsUrl(query);
            IsSearching.Value = true;
            SearchMessage.Value = "";
            _cts = new CancellationTokenSource();

            VideoItem[] items;
            if (isUrl)
            {
                string url = UrlDetector.Normalize(query);
                YtDlpVideoInfo info = await runner.FetchInfoAsync(
                    url, _environment.FFmpegLocation.Value, _settings.ExtraArguments.Value, _cts.Token);

                if (info.IsPlaylist)
                {
                    ReplaceSearchResults([]);
                    SearchMessage.Value = "プレイリスト URL は未対応です。動画の URL を指定してください。";
                    ShowWarning($"プレイリスト URL は未対応です（{info.PlaylistCount ?? 0} 件）。動画の URL を指定してください。");
                    return;
                }

                var item = new VideoItem(YtDlpSearchResult.FromVideoInfo(info, url), this);
                item.SetFormats(info.Formats);
                items = [item];
            }
            else
            {
                int count = Math.Clamp(
                    _settings.SearchResultCount.Value, Models.SearchProviders.MinResultCount, Models.SearchProviders.MaxResultCount);
                IReadOnlyList<YtDlpSearchResult> results = await runner.SearchAsync(
                    SelectedSearchProvider.Value.Provider, query, count,
                    _environment.FFmpegLocation.Value, _settings.ExtraArguments.Value, _cts.Token);
                items = results.Select(r => new VideoItem(r, this)).ToArray();
            }

            ReplaceSearchResults(items);
            SearchMessage.Value = items.Length == 0 ? "見つかりませんでした" : "";
        }
        catch (OperationCanceledException)
        {
            SearchMessage.Value = SearchHint;
        }
        catch (Exception ex)
        {
            string title = isUrl ? "動画情報の取得に失敗しました。" : "検索に失敗しました。";
            _logger.LogError(ex, "{Operation} failed", isUrl ? "Fetch" : "Search");
            SearchMessage.Value = title;
            ShowError(title, ex.Message);
        }
        finally
        {
            IsSearching.Value = false;
            End();
        }
    }

    public async Task DownloadItem(VideoItem item, DownloadDestination destination)
    {
        if (!TryBegin()) return;

        try
        {
            if (!TryGetRunner(out YtDlpRunner? runner)) return;

            (FormatPreset preset, string? customArguments) = YtDlpArguments.ResolveFormat(
                SelectedPreset.Value.Preset, CustomFormatArguments.Value, item.SelectedFormat.Value);
            if (preset == FormatPreset.Custom && string.IsNullOrWhiteSpace(customArguments))
            {
                ShowWarning("カスタム引数を入力するか、別の形式を選択してください。");
                return;
            }

            var request = new YtDlpDownloadRequest(
                Url: item.Url,
                OutputDirectory: ResolveOutputDirectory(destination),
                Preset: preset,
                CustomArguments: customArguments,
                FFmpegLocation: _environment.FFmpegLocation.Value,
                ExtraArguments: _settings.ExtraArguments.Value);

            ClearLog();
            if (request.FFmpegLocation == null && preset != FormatPreset.Custom)
            {
                // YtDlpArguments then picks single-file formats, which can be lower quality.
                AppendLog(preset == FormatPreset.AudioOnly
                    ? "警告: FFmpeg が見つからないため、音声を m4a に変換できません。元の形式のまま保存します。"
                    : "警告: FFmpeg が見つからないため、映像と音声を結合できません。結合済みの形式でダウンロードするため、画質が下がることがあります。");
            }

            ActiveItem.Value = item;
            item.BeginDownload();
            _cts = new CancellationTokenSource();

            IReadOnlyList<string> files = await runner.DownloadAsync(
                request, ev => OnOutputEvent(item, ev), AppendLog, _cts.Token);

            item.CompleteDownload(files[^1], destination);
            foreach (string file in files)
            {
                var historyItem = new DownloadHistoryItem(item.Title, file, item.Result.Duration, item.Url, DateTimeOffset.Now);
                (destination == DownloadDestination.Materials ? _materialsHistoryStore : _sceneHistoryStore).Add(historyItem);

                if (AddToTimelineAfterDownload.Value)
                {
                    await AddToTimelineCoreAsync(historyItem, _cts.Token);
                }
            }

            NotificationService.ShowSuccess(
                "yt-dlp", $"{DownloadDestinations.GetLabel(destination)}に保存しました: {Path.GetFileName(files[^1])}");
        }
        catch (OperationCanceledException)
        {
            item.FailDownload("", canceled: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {Url}", item.Url);
            item.FailDownload(ex.Message, canceled: false);
            if (ex is YtDlpException { StandardError.Length: > 0 } ytDlpException)
            {
                AppendLog(ytDlpException.StandardError.TrimEnd());
            }

            ShowError("ダウンロードに失敗しました。", ex.Message);
        }
        finally
        {
            ActiveItem.Value = null;
            End();
        }
    }

    // Formats are fetched lazily when the row's format flyout opens (search rows only carry flat data).
    public async Task LoadFormatsAsync(VideoItem item)
    {
        if (item.FormatsLoaded) return;
        if (!TryBegin()) return;

        try
        {
            if (!TryGetRunner(out YtDlpRunner? runner)) return;

            item.IsLoadingFormats.Value = true;
            item.FormatsMessage.Value = "";
            _cts = new CancellationTokenSource();

            YtDlpVideoInfo info = await runner.FetchInfoAsync(
                item.Url, _environment.FFmpegLocation.Value, _settings.ExtraArguments.Value, _cts.Token);
            item.SetFormats(info.Formats);
        }
        catch (OperationCanceledException)
        {
            item.FormatsMessage.Value = "キャンセルしました";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load formats for {Url}", item.Url);
            item.FormatsMessage.Value = $"取得に失敗しました: {ex.Message}";
        }
        finally
        {
            item.IsLoadingFormats.Value = false;
            End();
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void ClearSearchResults()
    {
        ReplaceSearchResults([]);
        SearchMessage.Value = SearchHint;
    }

    public Task AddToTimeline(DownloadHistoryItem item)
    {
        return AddToTimelineCoreAsync(item, CancellationToken.None);
    }

    // Removes an entry from whichever history file holds it; the downloaded file itself is kept.
    public void RemoveHistoryItem(DownloadHistoryItem item)
    {
        if (!_materialsHistoryStore.Remove(item))
        {
            _sceneHistoryStore.Remove(item);
        }
    }

    public string ResolveOutputDirectory(DownloadDestination destination)
    {
        if (destination == DownloadDestination.Materials)
        {
            return DownloadDestinations.GetMaterialsDirectory();
        }

        return ResolveOutputDirectory();
    }

    public string ResolveOutputDirectory()
    {
        string? configured = OutputDirectory.Value?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
        }

        return DefaultOutputDirectory;
    }

    private void ReplaceSearchResults(VideoItem[] items)
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;

        VideoItem[] previous = SearchResults.Value;
        SearchResults.Value = items;

        // The list may still be rendering the old bitmaps; dispose them after the UI has moved on.
        if (previous.Length > 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (VideoItem item in previous)
                {
                    item.Dispose();
                }
            }, DispatcherPriority.Background);
        }

        if (items.Length == 0) return;

        var cts = new CancellationTokenSource();
        _thumbnailCts = cts;
        foreach (VideoItem item in items)
        {
            _ = LoadThumbnailAsync(item, cts.Token);
        }
    }

    private async Task LoadThumbnailAsync(VideoItem item, CancellationToken ct)
    {
        try
        {
            await item.LoadThumbnailAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load a thumbnail");
        }
    }

    private async Task AddToTimelineCoreAsync(DownloadHistoryItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(item.FilePath))
            {
                ShowWarning($"ファイルが見つかりません: {item.FilePath}");
                return;
            }

            var adder = _editorContext.GetRequiredService<IElementAdder>();
            MediaTime start = _clock.CurrentTime.Value;
            MediaTime length = item.Duration is { } d && d > TimeSpan.Zero
                ? MediaTime.FromTimeSpan(d)
                : MediaTime.FromSeconds(5);

            // A video import also creates an audio companion on the next layer, so reserve two layers.
            bool isAudioOnly = s_audioExtensions.Contains(
                Path.GetExtension(item.FilePath), StringComparer.OrdinalIgnoreCase);
            int layer = LayerFinder.FindFreeLayer(_scene, start, length, isAudioOnly ? 1 : 2);

            ElementAddResult result = await adder.AddAsync(
                [new ElementDescription(start, length, layer, new ElementSource.File(item.FilePath))],
                cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Failure is { } failure)
                {
                    _logger.LogError(failure.Exception, "Failed to add the downloaded file to the timeline: {Message}", failure.Message);
                    ShowError("タイムラインへの追加に失敗しました。", failure.Message);
                }

                return;
            }

            _logger.LogInformation("Added {File} to the timeline at {Start} on layer {Layer}", item.FilePath, start.ToTimeSpan(), layer);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add the downloaded file to the timeline");
            ShowError("タイムラインへの追加に失敗しました。", ex.Message);
        }
    }

    private bool TryBegin()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return false;

        IsBusy.Value = true;
        return true;
    }

    private void End()
    {
        _cts?.Dispose();
        _cts = null;
        IsBusy.Value = false;
        Interlocked.Exchange(ref _busy, 0);
    }

    private bool TryGetRunner([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out YtDlpRunner? runner)
    {
        runner = _environment.CreateRunner();
        if (runner != null) return true;

        ShowWarning("yt-dlp が見つかりません。インストールするか、設定で実行ファイルのパスを指定してください。");
        return false;
    }

    private FormatPresetItem FindPreset(FormatPreset preset)
    {
        return Presets.FirstOrDefault(p => p.Preset == preset) ?? Presets[0];
    }

    // <scene folder>/.beutl/yt-dlp-history.json, next to Beutl's own per-scene view state.
    private string GetSceneHistoryPath()
    {
        string sceneDirectory = _scene.Uri is { } sceneUri
            ? Path.GetDirectoryName(sceneUri.LocalPath) ?? Directory.GetCurrentDirectory()
            : Directory.GetCurrentDirectory();
        return Path.Combine(sceneDirectory, EditorConstants.BeutlFolder, DownloadHistoryStore.FileName);
    }

    private string GetProjectOutputDirectory()
    {
        string? projectDirectory = _scene.FindHierarchicalParent<Project>() is { Uri: { } projectUri }
            ? Path.GetDirectoryName(projectUri.LocalPath)
            : _scene.Uri is { } sceneUri ? Path.GetDirectoryName(sceneUri.LocalPath) : null;

        return Path.Combine(projectDirectory ?? Directory.GetCurrentDirectory(), "resources", "yt-dlp");
    }

    // Called from the process reader thread.
    private void OnOutputEvent(VideoItem item, YtDlpOutputEvent ev)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (ev)
            {
                case DownloadProgressEvent progress:
                    if (progress.Fraction is { } fraction)
                    {
                        item.IsIndeterminate.Value = false;
                        item.Progress.Value = fraction * 100;
                    }
                    else
                    {
                        item.IsIndeterminate.Value = true;
                    }

                    item.StatusText.Value = FormatDownloadStatus(progress);
                    break;

                case PostProcessEvent { IsStarted: true } post:
                    item.IsIndeterminate.Value = true;
                    item.StatusText.Value = $"後処理中: {post.Postprocessor}";
                    break;

                case PostProcessEvent { IsFinished: true } post:
                    item.IsIndeterminate.Value = false;
                    AppendLogCore($"後処理完了: {post.Postprocessor}");
                    break;

                case OutputFileEvent file:
                    AppendLogCore($"出力: {file.FilePath}");
                    break;
            }
        });
    }

    private static string FormatDownloadStatus(DownloadProgressEvent progress)
    {
        if (progress.IsFinished) return "ダウンロード完了。後処理を待っています...";

        var text = new StringBuilder("ダウンロード中");
        if (progress.Fraction is { } fraction)
        {
            text.Append(CultureInfo.InvariantCulture, $" {fraction * 100:0.0}%");
        }

        if (progress.DownloadedBytes is { } downloaded)
        {
            text.Append("  ").Append(YtDlpFormat.FormatBytes(downloaded));
            if (progress.TotalBytes is { } total)
            {
                text.Append(" / ").Append(YtDlpFormat.FormatBytes(total));
            }
        }

        if (progress.Speed is { } speed)
        {
            text.Append("  ").Append(YtDlpFormat.FormatBytes(speed)).Append("/s");
        }

        if (progress.Eta is { } eta)
        {
            text.Append("  残り ").Append(YtDlpVideoInfo.FormatDuration(TimeSpan.FromSeconds(eta)));
        }

        return text.ToString();
    }

    private void AppendLog(string line)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AppendLogCore(line);
        }
        else
        {
            Dispatcher.UIThread.Post(() => AppendLogCore(line));
        }
    }

    private void AppendLogCore(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        _logLines.Add(line.TrimEnd());
        if (_logLines.Count > MaxLogLines)
        {
            _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
        }

        LogText.Value = string.Join(Environment.NewLine, _logLines);
    }

    private void ClearLog()
    {
        _logLines.Clear();
        LogText.Value = "";
    }

    private static void ShowWarning(string message)
    {
        NotificationService.ShowWarning("yt-dlp", message);
    }

    private static void ShowError(string title, string message)
    {
        NotificationService.ShowError(title, message);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _thumbnailCts?.Cancel();
        _disposables.Dispose();
        foreach (VideoItem item in SearchResults.Value)
        {
            item.Dispose();
        }

        SearchResults.Value = [];
    }

    public void WriteToJson(JsonObject json)
    {
        json[nameof(SelectedTabIndex)] = SelectedTabIndex.Value;
        json[nameof(SearchQuery)] = SearchQuery.Value;
        json[nameof(SelectedSearchProvider)] = SelectedSearchProvider.Value.Provider.ToString();
        json[nameof(SelectedPreset)] = SelectedPreset.Value.Preset.ToString();
        json[nameof(CustomFormatArguments)] = CustomFormatArguments.Value;
        json[nameof(OutputDirectory)] = OutputDirectory.Value;
        json[nameof(AddToTimelineAfterDownload)] = AddToTimelineAfterDownload.Value;
    }

    public void ReadFromJson(JsonObject json)
    {
        SelectedTabIndex.Value = Math.Clamp((int?)json[nameof(SelectedTabIndex)] ?? 0, 0, 1);
        SearchQuery.Value = (string?)json[nameof(SearchQuery)] ?? "";
        if (Enum.TryParse((string?)json[nameof(SelectedSearchProvider)], out SearchProvider provider)
            && SearchProviders.FirstOrDefault(p => p.Provider == provider) is { } providerItem)
        {
            SelectedSearchProvider.Value = providerItem;
        }

        CustomFormatArguments.Value = (string?)json[nameof(CustomFormatArguments)] ?? "";
        OutputDirectory.Value = (string?)json[nameof(OutputDirectory)] ?? "";
        AddToTimelineAfterDownload.Value = (bool?)json[nameof(AddToTimelineAfterDownload)] ?? true;
        if (Enum.TryParse((string?)json[nameof(SelectedPreset)], out FormatPreset preset))
        {
            SelectedPreset.Value = FindPreset(preset);
        }
    }

    public object? GetService(Type serviceType)
    {
        return null;
    }
}
