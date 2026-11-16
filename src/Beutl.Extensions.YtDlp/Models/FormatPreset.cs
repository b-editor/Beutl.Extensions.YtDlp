using System.ComponentModel.DataAnnotations;
using Beutl.Extensions.YtDlp.Resources;

namespace Beutl.Extensions.YtDlp.Models;

public enum FormatPreset
{
    [Display(Name = nameof(Strings.BestQualityPreset), ShortName = nameof(Strings.BestQuality), ResourceType = typeof(Strings))]
    Best,

    [Display(Name = nameof(Strings.UpTo1080p), ShortName = nameof(Strings.Resolution1080p), ResourceType = typeof(Strings))]
    UpTo1080p,

    [Display(Name = nameof(Strings.UpTo720p), ShortName = nameof(Strings.Resolution720p), ResourceType = typeof(Strings))]
    UpTo720p,

    [Display(Name = nameof(Strings.AudioOnlyPreset), ShortName = nameof(Strings.AudioOnly), ResourceType = typeof(Strings))]
    AudioOnly,

    [Display(Name = nameof(Strings.CustomArguments), ShortName = nameof(Strings.Custom), ResourceType = typeof(Strings))]
    Custom,
}
