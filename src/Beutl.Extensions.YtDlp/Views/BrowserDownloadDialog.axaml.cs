using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.ViewModels;
using FluentAvalonia.UI.Controls;

namespace Beutl.Extensions.YtDlp.Views;

internal partial class BrowserDownloadDialog : FAContentDialog
{
    private string _outputDirectory = "";

    public BrowserDownloadDialog()
    {
        InitializeComponent();
    }

    private BrowserDownloadDialog(VideoItem item, DownloadOptions defaults) : this()
    {
        VideoTitle.Text = item.Title;
        ToolTip.SetTip(VideoTitle, item.Url);
        FormatBox.ItemsSource = item.Owner.Presets.Cast<object>().Concat(item.Formats.Value).ToArray();
        FormatBox.SelectedItem = (object?)defaults.Format ?? item.Owner.Presets.First(p => p.Preset == defaults.Preset);
        CustomArgumentsBox.Text = defaults.CustomArguments;
        _outputDirectory = defaults.OutputDirectory;
        DestinationBox.SelectedIndex = 0;
        AddToTimelineBox.IsChecked = defaults.AddToTimeline;

        FormatBox.SelectionChanged += (_, _) => UpdateFormat();
        CustomArgumentsBox.TextChanged += (_, _) => UpdateFormat();
        DestinationBox.SelectionChanged += (_, _) => UpdateFolder();
        UpdateFormat();
        UpdateFolder();
    }

    protected override Type StyleKeyOverride => typeof(FAContentDialog);

    internal static async Task<DownloadOptions?> ChooseAsync(VideoItem item, DownloadOptions defaults, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new BrowserDownloadDialog(item, defaults);
        Task<FAContentDialogResult> result = dialog.ShowAsync();
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() =>
        {
            if (!result.IsCompleted) dialog.Hide();
        }));
        if (await result != FAContentDialogResult.Primary) return null;
        cancellationToken.ThrowIfCancellationRequested();
        return defaults with
        {
            Preset = (dialog.FormatBox.SelectedItem as FormatPresetItem)?.Preset ?? defaults.Preset,
            CustomArguments = dialog.CustomArgumentsBox.Text,
            Format = dialog.FormatBox.SelectedItem as YtDlpFormat,
            Destination = dialog.Destination,
            OutputDirectory = dialog.SelectedDirectory,
            AddToTimeline = dialog.AddToTimelineBox.IsChecked == true,
        };
    }

    private DownloadDestination Destination => DestinationBox.SelectedIndex == 1
        ? DownloadDestination.Materials : DownloadDestination.OutputDirectory;

    private string SelectedDirectory => Destination == DownloadDestination.Materials
        ? DownloadDestinations.GetMaterialsDirectory() : _outputDirectory;

    private void UpdateFormat()
    {
        bool custom = FormatBox.SelectedItem is FormatPresetItem { Preset: FormatPreset.Custom };
        CustomArgumentsBox.IsVisible = custom;
        IsPrimaryButtonEnabled = !custom || !string.IsNullOrWhiteSpace(CustomArgumentsBox.Text);
    }

    private void UpdateFolder()
    {
        FolderPath.Text = SelectedDirectory;
        ToolTip.SetTip(FolderPath, FolderPath.Text);
    }

    private async void BrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (await OutputFolderPicker.ChooseAsync(this, SelectedDirectory) is not { } path) return;
        _outputDirectory = path;
        OutputFolderOption.Content = new DirectoryInfo(path).Name;
        DestinationBox.SelectedIndex = 0;
        UpdateFolder();
    }
}
