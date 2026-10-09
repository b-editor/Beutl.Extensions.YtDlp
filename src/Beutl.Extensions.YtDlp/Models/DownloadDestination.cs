namespace Beutl.Extensions.YtDlp.Models;

public enum DownloadDestination
{
    // The directory configured in the tab (or the project's resources/yt-dlp folder).
    OutputDirectory,

    // Beutl's shared materials folder, so the file shows up in the file browser's 素材 favorite.
    Materials,
}

public static class DownloadDestinations
{
    public const string MaterialsSubdirectory = "yt-dlp";

    public static string GetMaterialsDirectory()
    {
        return Path.Combine(BeutlEnvironment.GetMaterialsDirectoryPath(), MaterialsSubdirectory);
    }

    public static string GetLabel(DownloadDestination destination) => destination switch
    {
        DownloadDestination.Materials => "素材フォルダー",
        _ => "保存先",
    };
}
