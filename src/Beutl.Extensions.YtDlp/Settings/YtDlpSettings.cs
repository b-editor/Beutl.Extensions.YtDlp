using System.ComponentModel.DataAnnotations;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Resources;

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

    [Display(Name = nameof(Strings.ExecutablePath), Description = nameof(Strings.ExecutablePathDescription), ResourceType = typeof(Strings))]
    public IProperty<string> ExecutablePath { get; } = Property.CreateValue("");

    [Display(Name = nameof(Strings.FFmpegLocation), Description = nameof(Strings.FFmpegLocationDescription), ResourceType = typeof(Strings))]
    public IProperty<string> FFmpegPath { get; } = Property.CreateValue("");

    [Display(Name = nameof(Strings.DefaultOutputFolder), Description = nameof(Strings.DefaultOutputFolderDescription), ResourceType = typeof(Strings))]
    public IProperty<string> OutputDirectory { get; } = Property.CreateValue("");

    [Display(Name = nameof(Strings.DefaultFormat), ResourceType = typeof(Strings))]
    public IProperty<FormatPreset> DefaultPreset { get; } = Property.CreateValue(FormatPreset.Best);

    [Display(Name = nameof(Strings.ExtraArguments), Description = nameof(Strings.ExtraArgumentsDescription), ResourceType = typeof(Strings))]
    public IProperty<string> ExtraArguments { get; } = Property.CreateValue("");

    [Display(Name = nameof(Strings.SearchResultCount), Description = nameof(Strings.SearchResultCountDescription), ResourceType = typeof(Strings))]
    [Range(SearchProviders.MinResultCount, SearchProviders.MaxResultCount)]
    public IProperty<int> SearchResultCount { get; } = Property.CreateValue(20);
}
