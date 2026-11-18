using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Beutl.Extensions.YtDlp.Resources;

namespace Beutl.Extensions.YtDlp.Views;

internal static class OutputFolderPicker
{
    public static async Task<string?> ChooseAsync(Control owner, string current)
    {
        if (TopLevel.GetTopLevel(owner) is not { } topLevel) return null;
        var options = new FolderPickerOpenOptions { Title = Strings.ChooseOutputFolder, AllowMultiple = false };
        if (Directory.Exists(current))
        {
            options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(current);
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
