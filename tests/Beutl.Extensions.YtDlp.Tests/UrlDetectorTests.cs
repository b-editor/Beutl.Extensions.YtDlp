using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class UrlDetectorTests
{
    [TestCase("https://youtu.be/jNQXAC9IVRw")]
    [TestCase("HTTP://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [TestCase("  https://soundcloud.com/a/b  ")]
    [TestCase("www.youtube.com/watch?v=jNQXAC9IVRw")]
    [TestCase("youtu.be/jNQXAC9IVRw")]
    [TestCase("example.com")]
    [TestCase("localhost.example:8080/video")]
    public void IsUrl_AcceptsHttpAndSchemelessUrls(string text)
    {
        Assert.That(UrlDetector.IsUrl(text), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("me at the zoo")]
    [TestCase("youtube")]
    [TestCase("song.mp3")]
    [TestCase("https://not a url")]
    [TestCase("ftp://example.com/file")]
    public void IsUrl_RejectsKeywords(string? text)
    {
        Assert.That(UrlDetector.IsUrl(text), Is.False);
    }

    [Test]
    public void Normalize_AddsHttpsWhenSchemeIsMissing()
    {
        Assert.That(UrlDetector.Normalize(" www.youtube.com/watch?v=x "), Is.EqualTo("https://www.youtube.com/watch?v=x"));
        Assert.That(UrlDetector.Normalize("https://youtu.be/x"), Is.EqualTo("https://youtu.be/x"));
        Assert.That(UrlDetector.Normalize("HTTP://example.com"), Is.EqualTo("HTTP://example.com"));
    }
}
