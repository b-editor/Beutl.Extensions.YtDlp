using System.ComponentModel.DataAnnotations;

namespace Beutl.Extensions.YtDlp.Models;

public enum FormatPreset
{
    [Display(Name = "最高画質（H.264 / MP4 優先）", ShortName = "最高画質")]
    Best,

    [Display(Name = "1080p まで", ShortName = "1080p")]
    UpTo1080p,

    [Display(Name = "720p まで", ShortName = "720p")]
    UpTo720p,

    [Display(Name = "音声のみ（M4A）", ShortName = "音声のみ")]
    AudioOnly,

    [Display(Name = "カスタム引数", ShortName = "カスタム")]
    Custom,
}
