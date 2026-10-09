using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.Extensions.YtDlp.Views;
using Beutl.Logging;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp;

[Export]
public sealed class YtDlpTabExtension : ToolTabExtension
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpTabExtension>();

    public override string Name => "yt-dlp Downloader Tab";

    public override string DisplayName => "yt-dlp ダウンローダー";

    public override string Header => "yt-dlp";

    public override bool CanMultiple => false;

    public override DockAnchor DefaultAnchor => DockAnchor.Right;

    public override bool TryCreateContent(IEditorContext editorContext, [NotNullWhen(true)] out Control? control)
    {
        control = new YtDlpTabView();
        return true;
    }

    public override bool TryCreateContext(IEditorContext editorContext, [NotNullWhen(true)] out IToolContext? context)
    {
        YtDlpEnvironment? environment = YtDlpExtension.Current?.Environment;
        if (environment == null)
        {
            // Extensions in one package are instantiated together, so this only happens if YtDlpExtension
            // failed to load. Fall back to defaults so the tab still works with a PATH-installed yt-dlp.
            _logger.LogWarning("YtDlpExtension is not loaded; using default settings for the tool tab");
            environment = new YtDlpEnvironment(new YtDlpSettings());
            _ = environment.RefreshAsync();
        }

        context = new YtDlpTabViewModel(this, editorContext, environment);
        return true;
    }
}
