using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Resources;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Extensions.YtDlp.ViewModels;

// The existing display tests verify the Japanese UI regardless of the test machine's language.
[assembly: SetUICulture("ja-JP")]

namespace Beutl.Extensions.YtDlp.Tests;

public class LocalizationTests
{
    [TestCase("ja")]
    [TestCase("zh")]
    [TestCase("ko")]
    [TestCase("es")]
    public void TranslationHasEveryMessageAndTheSameFormatArgumentCount(string language)
    {
        ResourceManager manager = Strings.ResourceManager;
        ResourceSet original = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        ResourceSet? translation = manager.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false);
        Assert.That(translation, Is.Not.Null);

        foreach (DictionaryEntry entry in original)
        {
            string key = (string)entry.Key;
            string? value = translation!.GetString(key);
            Assert.That(value, Is.Not.Null.And.Not.Empty, key);
            Assert.That(CompositeFormat.Parse(value!).MinimumArgumentCount,
                Is.EqualTo(CompositeFormat.Parse((string)entry.Value!).MinimumArgumentCount), key);
        }
    }

    [SetUICulture("en-US")]
    [TestCase(0, "0 views")]
    [TestCase(1, "1 view")]
    [TestCase(1_000, "1K views")]
    [TestCase(1_000_000, "1M views")]
    [TestCase(1_000_000_000, "1B views")]
    public void EnglishViewCountUsesTheAppropriateUnit(long views, string expected)
    {
        Assert.That(YtDlpSearchResult.FormatViewCount(views), Is.EqualTo(expected));
    }

    [TestCase("en-US", "yt-dlp executable", "Audio only", "12.3K views")]
    [TestCase("ja-JP", "yt-dlp の実行ファイル", "音声のみ", "1.2万回")]
    [TestCase("zh-CN", "yt-dlp 可执行文件", "仅音频", "1.2 万次观看")]
    [TestCase("ko-KR", "yt-dlp 실행 파일", "오디오만", "조회수 1.2만 회")]
    [TestCase("es-ES", "Ejecutable de yt-dlp", "Solo audio", "12,3 mil visualizaciones")]
    [TestCase("fr-FR", "yt-dlp executable", "Audio only", "12,3K views")]
    public void DisplayUsesTheHostUiCulture(string culture, string settingName, string preset, string views)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var display = typeof(YtDlpSettings).GetProperty(nameof(YtDlpSettings.ExecutablePath))!
            .GetCustomAttribute<DisplayAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(display.GetName(), Is.EqualTo(settingName));
            Assert.That(display.GetDescription(), Does.Contain("~/.beutl/yt-dlp"));
            Assert.That(FormatPresetItem.Create(FormatPreset.AudioOnly).ShortName, Is.EqualTo(preset));
            Assert.That(YtDlpSearchResult.FormatViewCount(12_345), Is.EqualTo(views));
        });
    }
}
