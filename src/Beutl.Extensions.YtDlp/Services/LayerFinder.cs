using Beutl.ProjectSystem;

namespace Beutl.Extensions.YtDlp.Services;

public readonly record struct OccupiedRange(int ZIndex, TimeSpan Start, TimeSpan Length)
{
    public TimeSpan End => Start + Length;
}

// Picks the lowest layer where a new element (and, for video, its audio companion on the next
// layer) fits without overlapping existing elements or locked layers.
public static class LayerFinder
{
    public const int MaxLayer = 10_000;

    public static int FindFreeLayer(Scene scene, TimeSpan start, TimeSpan length, int requiredLayers = 1)
    {
        return FindFreeLayer(
            scene.Children.Select(e => new OccupiedRange(e.ZIndex, e.Start, e.Length)),
            scene.IsLayerLocked,
            start,
            length,
            requiredLayers);
    }

    public static int FindFreeLayer(
        IEnumerable<OccupiedRange> elements,
        Func<int, bool> isLayerLocked,
        TimeSpan start,
        TimeSpan length,
        int requiredLayers = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredLayers, 1);
        if (length <= TimeSpan.Zero) length = TimeSpan.FromTicks(1);

        TimeSpan end = start + length;
        var occupied = new HashSet<int>(
            elements
                .Where(e => e.Start < end && start < e.End)
                .Select(e => e.ZIndex));

        for (int z = 0; z <= MaxLayer; z++)
        {
            bool free = true;
            for (int i = 0; i < requiredLayers; i++)
            {
                if (occupied.Contains(z + i) || isLayerLocked(z + i))
                {
                    free = false;
                    break;
                }
            }

            if (free) return z;
        }

        return MaxLayer + 1;
    }
}
