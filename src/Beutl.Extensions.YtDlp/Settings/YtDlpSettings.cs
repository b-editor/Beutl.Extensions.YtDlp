using System.ComponentModel.DataAnnotations;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Settings;

public sealed partial class YtDlpSettings : ExtensionSettings
{
    public YtDlpSettings()
    {
        ScanProperties<YtDlpSettings>();
        ExecutablePath.ValueChanged += (_, _) => OnChanged();
        FFmpegPath.ValueChanged += (_, _) => OnChanged();
        OutputDirectory.ValueChanged += (_, _) => OnChanged();
        DefaultPreset.ValueChanged += (_, _) => OnChanged();
        ExtraArguments.ValueChanged += (_, _) => OnChanged();
        SearchResultCount.ValueChanged += (_, _) => OnChanged();
    }

    [Display(Name = "yt-dlp の実行ファイル", Description = "空の場合は ~/.beutl/yt-dlp と PATH から検索します")]
    public IProperty<string> ExecutablePath { get; } = Property.CreateValue("");

    [Display(Name = "FFmpeg の場所", Description = "映像と音声の結合に使います。空の場合は ~/.beutl/ffmpeg、Homebrew、PATH から検索します")]
    public IProperty<string> FFmpegPath { get; } = Property.CreateValue("");

    [Display(Name = "既定の保存先", Description = "空の場合はプロジェクトの resources/yt-dlp に保存します")]
    public IProperty<string> OutputDirectory { get; } = Property.CreateValue("");

    [Display(Name = "既定の形式")]
    public IProperty<FormatPreset> DefaultPreset { get; } = Property.CreateValue(FormatPreset.Best);

    [Display(Name = "追加の引数", Description = "すべての yt-dlp 呼び出しに付加されます（例: --cookies-from-browser chrome）")]
    public IProperty<string> ExtraArguments { get; } = Property.CreateValue("");

    [Display(Name = "検索結果の件数", Description = "動画検索で取得する件数（1〜100）")]
    [Range(SearchProviders.MinResultCount, SearchProviders.MaxResultCount)]
    public IProperty<int> SearchResultCount { get; } = Property.CreateValue(20);
}
