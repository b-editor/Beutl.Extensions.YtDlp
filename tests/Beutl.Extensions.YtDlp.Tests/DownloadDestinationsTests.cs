using Beutl.Extensions.YtDlp.Models;

namespace Beutl.Extensions.YtDlp.Tests;

public class DownloadDestinationsTests
{
    [Test]
    public void GetMaterialsDirectory_IsSubfolderOfBeutlMaterials()
    {
        string directory = DownloadDestinations.GetMaterialsDirectory();

        Assert.That(directory, Is.EqualTo(Path.Combine(BeutlEnvironment.GetMaterialsDirectoryPath(), "yt-dlp")));
        Assert.That(directory, Does.EndWith(Path.Combine("materials", "yt-dlp")));
    }

    [TestCase(DownloadDestination.OutputDirectory, "保存先")]
    [TestCase(DownloadDestination.Materials, "素材フォルダー")]
    public void GetLabel_NamesTheDestination(DownloadDestination destination, string expected)
    {
        Assert.That(DownloadDestinations.GetLabel(destination), Is.EqualTo(expected));
    }
}
