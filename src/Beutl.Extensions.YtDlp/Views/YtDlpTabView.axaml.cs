using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Beutl.Extensions.YtDlp.ViewModels;

namespace Beutl.Extensions.YtDlp.Views;

public partial class YtDlpTabView : UserControl
{
    public YtDlpTabView()
    {
        InitializeComponent();
        DisableEnterFocusMove(searchBox);
        // Tunnel so Enter / Ctrl+Enter / Cmd+Enter run the search before the TextBox (or any
        // ancestor key binding, e.g. the Cmd+Enter "commit and unfocus" default) sees the key.
        searchBox.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
    }

    // Beutl's App.axaml sets TextBoxAttachment.EnterDownBehavior="Auto" on every TextBox, which moves
    // focus to the next control (or the dock container) on Enter, e.g. while a search is running.
    // Beutl.Controls is not referenceable from extensions, so set it to None by reflection; the
    // local value outranks the app style. Does nothing if Beutl ever drops the attachment.
    private static void DisableEnterFocusMove(TextBox textBox)
    {
        if (Type.GetType("Beutl.Controls.TextBoxAttachment, Beutl.Controls")
                ?.GetField("EnterDownBehaviorProperty", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) is AvaloniaProperty property
            && Enum.TryParse(property.PropertyType, "None", out object? none))
        {
            textBox.SetValue(property, none);
        }
    }

    private YtDlpTabViewModel? ViewModel => DataContext as YtDlpTabViewModel;

    private async void InstallYtDlp(object? sender, RoutedEventArgs e)
    {
        await YtDlpExtension.ShowInstallDialogAsync();
    }

    private async void UpdateYtDlp(object? sender, RoutedEventArgs e)
    {
        await YtDlpExtension.ShowInstallDialogAsync();
    }

    private void ShowLog(object? sender, RoutedEventArgs e)
    {
        // Let the menu flyout finish closing before the log opens on the same button.
        Dispatcher.UIThread.Post(() => FlyoutBase.ShowAttachedFlyout(moreButton));
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter, Ctrl+Enter and Cmd+Enter all run the search.
        bool isEnter = e.Key == Key.Enter
                       && (e.KeyModifiers & ~(KeyModifiers.Control | KeyModifiers.Meta)) == KeyModifiers.None;
        if (isEnter && ViewModel is { } viewModel && !viewModel.IsBusy.Value)
        {
            e.Handled = true;
            _ = viewModel.Search();
        }
    }

    private void OnVideoItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Double-clicking one of the row's own buttons must not also start a download.
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) != null) return;

        if (sender is Control { DataContext: VideoItem item } && !item.Owner.IsBusy.Value)
        {
            e.Handled = true;
            _ = item.Download();
        }
    }

    private async void OpenVideoPage(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: VideoItem item }) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;

        if (Uri.TryCreate(item.Url, UriKind.Absolute, out Uri? uri))
        {
            await topLevel.Launcher.LaunchUriAsync(uri);
        }
    }

    private void OnFormatFlyoutOpening(object? sender, EventArgs e)
    {
        if (sender is FlyoutBase { Target.DataContext: VideoItem item })
        {
            _ = item.LoadFormats();
        }
    }

    private async void BrowseOutputDirectory(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "保存先を選択",
            AllowMultiple = false,
        };

        string current = viewModel.ResolveOutputDirectory();
        if (Directory.Exists(current))
        {
            options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(current);
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            viewModel.OutputDirectory.Value = path;
        }
    }

    private async void AddHistoryItemToTimeline(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DownloadHistoryItem item } && ViewModel is { } viewModel)
        {
            await viewModel.AddToTimeline(item);
        }
    }

    private void RemoveHistoryItem(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DownloadHistoryItem item } && ViewModel is { } viewModel)
        {
            viewModel.RemoveHistoryItem(item);
        }
    }

    private async void OpenHistoryItemFolder(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: DownloadHistoryItem item }) return;
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;

        if (Directory.Exists(item.Directory))
        {
            await topLevel.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(item.Directory));
        }
    }
}
