using Avalonia.Media.Imaging;
using Beutl.Logging;
using Microsoft.Extensions.Logging;

namespace Beutl.Extensions.YtDlp.Services;

// Fetches thumbnails with a small concurrency cap so a page of search results does not open dozens
// of connections at once. Returns null (after logging) when an image cannot be fetched or decoded.
public static class ThumbnailLoader
{
    private static readonly ILogger s_logger = Log.CreateLogger(typeof(ThumbnailLoader));
    private static readonly HttpClient s_client = CreateClient();
    private static readonly SemaphoreSlim s_gate = new(4, 4);

    public static async Task<Bitmap?> LoadAsync(string url, CancellationToken ct)
    {
        await s_gate.WaitAsync(ct);
        try
        {
            byte[] bytes = await s_client.GetByteArrayAsync(url, ct);
            using var memory = new MemoryStream(bytes);
            return new Bitmap(memory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logger.LogWarning(ex, "Failed to load thumbnail {Url}", url);
            return null;
        }
        finally
        {
            s_gate.Release();
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Beutl.Extensions.YtDlp");
        return client;
    }
}
