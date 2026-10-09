using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.ViewModels;

public sealed record FormatPresetItem(FormatPreset Preset, string Name, string ShortName)
{
    public static FormatPresetItem Create(FormatPreset preset)
    {
        DisplayAttribute? display = typeof(FormatPreset)
            .GetField(preset.ToString())
            ?.GetCustomAttribute<DisplayAttribute>();
        string name = display?.GetName() ?? preset.ToString();
        return new FormatPresetItem(preset, name, display?.GetShortName() ?? name);
    }
}
