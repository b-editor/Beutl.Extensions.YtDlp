using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class YtDlpSearchOutputTests
{
    // Real yt-dlp output shape when the middle video fails extraction and the search has fewer
    // entries than requested: resolved videos inherit playlist_count and the failed entry is null.
    private const string PartialResults = """
        {
          "_type": "playlist", "playlist_count": 3,
          "entries": [
            { "extractor_key": "BiliBili", "id": "BV1", "title": "First", "webpage_url": "https://www.bilibili.com/video/BV1", "playlist_count": 3 },
            null,
            { "extractor_key": "BiliBili", "id": "BV3", "title": "Third", "webpage_url": "https://www.bilibili.com/video/BV3", "playlist_count": 3 }
          ]
        }
        """;

    [TestCase(0)]
    [TestCase(1)]
    public void ParseSearchOutput_KeepsUsableHitsOnBothSidesOfFailedEntry(int exitCode)
    {
        var results = YtDlpRunner.ParseSearchOutput(exitCode, PartialResults, "ERROR: This video requires login");

        Assert.That(results.Select(r => r.Id), Is.EqualTo(new[] { "BV1", "BV3" }));
        Assert.That(results.Select(r => r.Title), Is.EqualTo(new[] { "First", "Third" }));
    }

    [TestCase("")]
    [TestCase("null")]
    [TestCase("{invalid")]
    [TestCase("""{ "_type": "playlist", "entries": [null] }""")]
    public void ParseSearchOutput_FailedSearchPreservesExitCodeAndDiagnostic(string json)
    {
        const string error = "ERROR: Search request failed: HTTP Error 412";

        var exception = Assert.Throws<YtDlpException>(() => YtDlpRunner.ParseSearchOutput(1, json, error))!;

        Assert.That(exception.ExitCode, Is.EqualTo(1));
        Assert.That(exception.StandardError, Is.EqualTo(error));
        Assert.That(exception.Message, Does.Contain("HTTP Error 412"));
    }

    [Test]
    public void ParseSearchOutput_AllVideosUnavailableIsAnError()
    {
        const string json = """{ "_type": "playlist", "entries": [null, null] }""";

        Assert.Throws<YtDlpException>(() => YtDlpRunner.ParseSearchOutput(1, json, "ERROR: Videos require login"));
    }

    [Test]
    public void ParseSearchOutput_SuccessfulEmptySearchIsEmpty()
    {
        const string json = """{ "_type": "playlist", "entries": [] }""";

        Assert.That(YtDlpRunner.ParseSearchOutput(0, json, ""), Is.Empty);
    }

    [TestCase("")]
    [TestCase("null")]
    public void ParseSearchOutput_MissingOutputIsNotAnEmptySearch(string json)
    {
        Assert.Throws<YtDlpException>(() => YtDlpRunner.ParseSearchOutput(0, json, ""));
    }
}
