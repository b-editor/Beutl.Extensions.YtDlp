using System.ComponentModel.DataAnnotations;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Settings;

public sealed class YtDlpSettings : ExtensionSettings
{
    public static readonly CoreProperty<string> ExecutablePathProperty;
    public static readonly CoreProperty<string> FFmpegPathProperty;
    public static readonly CoreProperty<string> OutputDirectoryProperty;
    public static readonly CoreProperty<FormatPreset> DefaultPresetProperty;
    public static readonly CoreProperty<string> ExtraArgumentsProperty;
    public static readonly CoreProperty<int> SearchResultCountProperty;

    static YtDlpSettings()
    {
        ExecutablePathProperty = ConfigureProperty<string, YtDlpSettings>(nameof(ExecutablePath))
            .DefaultValue("")
            .Register();

        FFmpegPathProperty = ConfigureProperty<string, YtDlpSettings>(nameof(FFmpegPath))
            .DefaultValue("")
            .Register();

        OutputDirectoryProperty = ConfigureProperty<string, YtDlpSettings>(nameof(OutputDirectory))
            .DefaultValue("")
            .Register();

        DefaultPresetProperty = ConfigureProperty<FormatPreset, YtDlpSettings>(nameof(DefaultPreset))
            .DefaultValue(FormatPreset.Best)
            .Register();

        ExtraArgumentsProperty = ConfigureProperty<string, YtDlpSettings>(nameof(ExtraArguments))
            .DefaultValue("")
            .Register();

        SearchResultCountProperty = ConfigureProperty<int, YtDlpSettings>(nameof(SearchResultCount))
            .DefaultValue(20)
            .Register();

        AffectsConfig<YtDlpSettings>(
            ExecutablePathProperty,
            FFmpegPathProperty,
            OutputDirectoryProperty,
            DefaultPresetProperty,
            ExtraArgumentsProperty,
            SearchResultCountProperty);
    }

    [Display(Name = "yt-dlp の実行ファイル", Description = "空の場合は ~/.beutl/yt-dlp と PATH から検索します")]
    public string ExecutablePath
    {
        get => GetValue(ExecutablePathProperty);
        set => SetValue(ExecutablePathProperty, value);
    }

    [Display(Name = "FFmpeg の場所", Description = "映像と音声の結合に使います。空の場合は ~/.beutl/ffmpeg、Homebrew、PATH から検索します")]
    public string FFmpegPath
    {
        get => GetValue(FFmpegPathProperty);
        set => SetValue(FFmpegPathProperty, value);
    }

    [Display(Name = "既定の保存先", Description = "空の場合はプロジェクトの resources/yt-dlp に保存します")]
    public string OutputDirectory
    {
        get => GetValue(OutputDirectoryProperty);
        set => SetValue(OutputDirectoryProperty, value);
    }

    [Display(Name = "既定の形式")]
    public FormatPreset DefaultPreset
    {
        get => GetValue(DefaultPresetProperty);
        set => SetValue(DefaultPresetProperty, value);
    }

    [Display(Name = "追加の引数", Description = "すべての yt-dlp 呼び出しに付加されます（例: --cookies-from-browser chrome）")]
    public string ExtraArguments
    {
        get => GetValue(ExtraArgumentsProperty);
        set => SetValue(ExtraArgumentsProperty, value);
    }

    [Display(Name = "検索結果の件数", Description = "動画検索で取得する件数（1〜100）")]
    [Range(SearchProviders.MinResultCount, SearchProviders.MaxResultCount)]
    public int SearchResultCount
    {
        get => GetValue(SearchResultCountProperty);
        set => SetValue(SearchResultCountProperty, value);
    }
}
