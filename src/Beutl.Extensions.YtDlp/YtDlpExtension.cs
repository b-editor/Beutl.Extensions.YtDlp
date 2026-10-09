using System.ComponentModel;
using Avalonia.Threading;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.Extensions.YtDlp.Views;
using Beutl.Logging;
using Beutl.Services;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp;

// Owns the settings and the shared yt-dlp environment; the tool tab is registered separately.
[Export]
public sealed class YtDlpExtension : Extension
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpExtension>();

    public YtDlpExtension()
    {
        Environment = new YtDlpEnvironment(Settings);
        Current = this;
    }

    public static YtDlpExtension? Current { get; private set; }

    public override string Name => "yt-dlp";

    public override string DisplayName => "yt-dlp ダウンローダー";

    public override YtDlpSettings Settings { get; } = new();

    public YtDlpEnvironment Environment { get; }

    public override void Load()
    {
        base.Load();
        Settings.PropertyChanged += OnSettingsPropertyChanged;

        _ = Environment.RefreshAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Failed to locate yt-dlp");
                return;
            }

            if (!Environment.IsAvailable.Value)
            {
                Dispatcher.UIThread.Post(() =>
                    NotificationService.ShowWarning(
                        title: "yt-dlp",
                        message: "yt-dlp が見つかりません。インストールするか、設定で実行ファイルのパスを指定してください。",
                        actions: [new NotificationAction("インストール", () => _ = ShowInstallDialogAsync())]));
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    public override void Unload()
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        if (Current == this)
        {
            Current = null;
        }

        base.Unload();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(YtDlpSettings.ExecutablePath) or nameof(YtDlpSettings.FFmpegPath))
        {
            _ = Environment.RefreshAsync();
        }
    }

    public static async Task ShowInstallDialogAsync()
    {
        YtDlpEnvironment environment = Current?.Environment ?? new YtDlpEnvironment(new YtDlpSettings());
        var dialog = new YtDlpInstallDialog
        {
            DataContext = new YtDlpInstallDialogViewModel(environment)
        };
        await dialog.ShowAsync();
    }
}
