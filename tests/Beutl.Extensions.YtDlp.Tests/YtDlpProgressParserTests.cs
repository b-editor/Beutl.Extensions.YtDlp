using Beutl.Extensions.YtDlp.Models;
using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpProgressParserTests
{
    [Test]
    public void Parse_DownloadLine_ReturnsProgressWithFraction()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:dl]downloading|512000|1024000|NA|20480.5|25") as DownloadProgressEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.Status, Is.EqualTo("downloading"));
        Assert.That(ev.DownloadedBytes, Is.EqualTo(512000));
        Assert.That(ev.TotalBytes, Is.EqualTo(1024000));
        Assert.That(ev.Speed, Is.EqualTo(20480.5));
        Assert.That(ev.Eta, Is.EqualTo(25));
        Assert.That(ev.Fraction, Is.EqualTo(0.5));
        Assert.That(ev.IsFinished, Is.False);
    }

    [Test]
    public void Parse_DownloadLine_FallsBackToEstimateAndTreatsNaAsUnknown()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:dl]downloading|1000.0|NA|4000.0|NA|NA") as DownloadProgressEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.TotalBytes, Is.EqualTo(4000));
        Assert.That(ev.Fraction, Is.EqualTo(0.25));
        Assert.That(ev.Speed, Is.Null);
        Assert.That(ev.Eta, Is.Null);
    }

    [Test]
    public void Parse_DownloadLine_WithoutTotalHasNoFraction()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:dl]downloading|1000|NA|NA|NA|NA") as DownloadProgressEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.Fraction, Is.Null);
    }

    [Test]
    public void Parse_FinishedLine_ReportsFullFraction()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:dl]finished|1024000|1024000|NA|NA|NA") as DownloadProgressEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.IsFinished, Is.True);
        Assert.That(ev.Fraction, Is.EqualTo(1));
    }

    [Test]
    public void Parse_PostProcessLine()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:pp]started|Merger") as PostProcessEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.IsStarted, Is.True);
        Assert.That(ev.Postprocessor, Is.EqualTo("Merger"));
    }

    [Test]
    public void Parse_OutputFileLine_KeepsPathIncludingColonsAndSpaces()
    {
        var ev = YtDlpProgressParser.Parse("[beutl:file]C:\\Videos\\My Title [abc123].mp4") as OutputFileEvent;

        Assert.That(ev, Is.Not.Null);
        Assert.That(ev!.FilePath, Is.EqualTo("C:\\Videos\\My Title [abc123].mp4"));
    }

    [Test]
    public void Parse_UnrelatedLine_ReturnsNull()
    {
        Assert.That(YtDlpProgressParser.Parse("[youtube] abc123: Downloading webpage"), Is.Null);
        Assert.That(YtDlpProgressParser.Parse(""), Is.Null);
        Assert.That(YtDlpProgressParser.Parse("[beutl:dl]broken"), Is.Null);
    }
}
