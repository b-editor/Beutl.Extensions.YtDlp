using System.ComponentModel;
using Avalonia.Threading;
using Beutl.Extensibility;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Resources;
using Beutl.Extensions.YtDlp.Services;
using Beutl.Extensions.YtDlp.Settings;
using Beutl.Extensions.YtDlp.ViewModels;
using Beutl.Extensions.YtDlp.Views;
using Beutl.Logging;
using Beutl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp;

[Export]
public sealed class YtDlpExtension : BrowserDownloadExtension
{
    private readonly ILogger _logger = Log.CreateLogger<YtDlpExtension>();

    public YtDlpExtension()
    {
        Environment = new YtDlpEnvironment(Settings);
        Current = this;
    }

    public static YtDlpExtension? Current { get; private set; }

    public override string Name => "yt-dlp";

    public override string DisplayName => Strings.DownloaderName;

    public override YtDlpSettings Settings { get; } = new();

    public YtDlpEnvironment Environment { get; }

    public override async ValueTask<bool> TryDownloadPageAsync(IEditorContext editorContext, Uri page,
        string? pageTitle, CancellationToken cancellationToken)
    {
        if (!page.IsAbsoluteUri || page.Scheme is not ("http" or "https")) return false;
        cancellationToken.ThrowIfCancellationRequested();

        if (!Environment.HasChecked.Value) await Environment.RefreshAsync(cancellationToken);
        if (Environment.CreateRunner() is not { } runner)
        {
            ShowInstallNotification(editorContext.GetRequiredService<INotificationService>());
            return true;
        }

        YtDlpVideoInfo info;
        try
        {
            // Only single videos are imported. One playlist entry suffices to identify a playlist.
            info = await runner.FetchInfoAsync(page.AbsoluteUri, Environment.FFmpegLocation.Value,
                $"{Settings.ExtraArguments.Value} --playlist-end 1", cancellationToken);
        }
        catch (YtDlpException ex) when (ex.StandardError.Contains("Unsupported URL:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (info.IsPlaylist)
            throw new InvalidOperationException(Strings.PlaylistUnsupported);

        if (OpenDownloaderTab(editorContext) is not { } tab) return false;
        await tab.DownloadPageAsync(page, pageTitle, info, cancellationToken);
        return true;
    }

    private static YtDlpTabViewModel? OpenDownloaderTab(IEditorContext editor)
    {
        if (editor.FindToolTab<YtDlpTabViewModel>() is { } existing)
            return editor.OpenToolTab(existing) ? existing : null;

        var extensions = editor.GetService<IExtensionProvider>();
        if (extensions?.GetDescriptors<YtDlpTabExtension>().FirstOrDefault() is not { } descriptor
            || !extensions.TryAcquire(descriptor.Id, out IExtensionLease<YtDlpTabExtension>? lease))
            return null;

        using (lease)
        {
            if (!lease.Extension.TryCreateContext(editor, out IToolContext? context)) return null;
            if (context is YtDlpTabViewModel tab && editor.OpenToolTab(tab)) return tab;
            context.Dispose();
            return null;
        }
    }

    public override void Load(IExtensionContext context)
    {
        base.Load(context);
        INotificationService notifications = context.Services.GetRequiredService<INotificationService>();
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
                Dispatcher.UIThread.Post(() => ShowInstallNotification(notifications));
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private static void ShowInstallNotification(INotificationService notifications)
    {
        notifications.ShowWarning(
            title: "yt-dlp",
            message: Strings.YtDlpMissingMessage,
            actions: [new NotificationAction(Language.ExtensionsStrings.Install, () => _ = ShowInstallDialogAsync())]);
    }

    public override void Unload(IExtensionContext context)
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        if (Current == this)
        {
            Current = null;
        }

        base.Unload(context);
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
