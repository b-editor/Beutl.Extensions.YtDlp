using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.ViewModels;

namespace Beutl.Extensions.YtDlp.Tests;

public class FormatPresetItemTests
{
    [TestCase(FormatPreset.Best, "最高画質（H.264 / MP4 優先）", "最高画質")]
    [TestCase(FormatPreset.UpTo1080p, "1080p まで", "1080p")]
    [TestCase(FormatPreset.AudioOnly, "音声のみ（M4A）", "音声のみ")]
    [TestCase(FormatPreset.Custom, "カスタム引数", "カスタム")]
    public void Create_ReadsDisplayNameAndShortName(FormatPreset preset, string name, string shortName)
    {
        FormatPresetItem item = FormatPresetItem.Create(preset);

        Assert.That(item.Preset, Is.EqualTo(preset));
        Assert.That(item.Name, Is.EqualTo(name));
        Assert.That(item.ShortName, Is.EqualTo(shortName));
    }
}
