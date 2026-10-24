using System.Text.Json.Nodes;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Serialization;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpSettingsTests
{
    [Test]
    public void Defaults_ArePreserved()
    {
        var settings = new YtDlpSettings();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ExecutablePath.Value, Is.Empty);
            Assert.That(settings.FFmpegPath.Value, Is.Empty);
            Assert.That(settings.OutputDirectory.Value, Is.Empty);
            Assert.That(settings.DefaultPreset.Value, Is.EqualTo(FormatPreset.Best));
            Assert.That(settings.ExtraArguments.Value, Is.Empty);
            Assert.That(settings.SearchResultCount.Value, Is.EqualTo(20));
        });
    }

    [Test]
    public void Changes_NotifyTheSettingsStoreAndExecutableDetection()
    {
        var settings = new YtDlpSettings();
        var changedNames = new List<string?>();
        int saves = 0;
        settings.ConfigurationChanged += (_, _) => saves++;
        settings.PropertyChanged += (_, e) => changedNames.Add(e.PropertyName);

        settings.ExecutablePath.Value = "/tools/yt-dlp";
        settings.FFmpegPath.Value = "/tools/ffmpeg";
        settings.OutputDirectory.Value = "/downloads";
        settings.DefaultPreset.Value = FormatPreset.AudioOnly;
        settings.ExtraArguments.Value = "--no-playlist";
        settings.SearchResultCount.Value = 35;

        Assert.Multiple(() =>
        {
            Assert.That(saves, Is.EqualTo(6));
            Assert.That(changedNames, Is.EqualTo(new[]
            {
                nameof(YtDlpSettings.ExecutablePath), nameof(YtDlpSettings.FFmpegPath),
                nameof(YtDlpSettings.OutputDirectory), nameof(YtDlpSettings.DefaultPreset),
                nameof(YtDlpSettings.ExtraArguments), nameof(YtDlpSettings.SearchResultCount),
            }));
        });
    }

    [Test]
    public void Preview8Settings_RestoreAndSaveWithoutChangingTheSchema()
    {
        var saved = JsonNode.Parse("""
            {
                "ExecutablePath": "/tools/yt-dlp",
                "FFmpegPath": "/tools/ffmpeg",
                "OutputDirectory": "/downloads/動画",
                "DefaultPreset": 3,
                "ExtraArguments": "--no-playlist",
                "SearchResultCount": 35,
                "$type": "[Beutl.Extensions.YtDlp].Settings:YtDlpSettings"
            }
            """)!.AsObject();
        var settings = new YtDlpSettings();

        CoreSerializer.PopulateFromJsonObject(settings, saved);
        JsonObject restored = CoreSerializer.SerializeToJsonObject(settings);

        Assert.Multiple(() =>
        {
            Assert.That(settings.ExecutablePath.Value, Is.EqualTo("/tools/yt-dlp"));
            Assert.That(settings.FFmpegPath.Value, Is.EqualTo("/tools/ffmpeg"));
            Assert.That(settings.OutputDirectory.Value, Is.EqualTo("/downloads/動画"));
            Assert.That(settings.DefaultPreset.Value, Is.EqualTo(FormatPreset.AudioOnly));
            Assert.That(settings.ExtraArguments.Value, Is.EqualTo("--no-playlist"));
            Assert.That(settings.SearchResultCount.Value, Is.EqualTo(35));
            Assert.That(JsonNode.DeepEquals(restored, saved), Is.True, restored.ToJsonString());
        });
    }
}
