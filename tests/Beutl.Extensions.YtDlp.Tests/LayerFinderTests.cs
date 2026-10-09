using Beutl.Extensions.YtDlp.Services;

namespace Beutl.Extensions.YtDlp.Tests;

public class LayerFinderTests
{
    private static readonly TimeSpan s_zero = TimeSpan.Zero;

    private static TimeSpan Seconds(double s) => TimeSpan.FromSeconds(s);

    [Test]
    public void EmptyScene_UsesLayerZero()
    {
        int layer = LayerFinder.FindFreeLayer([], _ => false, s_zero, Seconds(10));

        Assert.That(layer, Is.EqualTo(0));
    }

    [Test]
    public void SkipsLayersOccupiedInTheTargetRange()
    {
        OccupiedRange[] elements =
        [
            new(0, Seconds(0), Seconds(10)),
            new(1, Seconds(5), Seconds(10)),
        ];

        int layer = LayerFinder.FindFreeLayer(elements, _ => false, Seconds(8), Seconds(5));

        Assert.That(layer, Is.EqualTo(2));
    }

    [Test]
    public void ElementsOutsideTheRangeDoNotBlock()
    {
        OccupiedRange[] elements =
        [
            new(0, Seconds(0), Seconds(10)),
        ];

        // Starts exactly when the existing element ends: no overlap.
        int layer = LayerFinder.FindFreeLayer(elements, _ => false, Seconds(10), Seconds(5));

        Assert.That(layer, Is.EqualTo(0));
    }

    [Test]
    public void RequiresConsecutiveFreeLayersForVideoWithAudio()
    {
        OccupiedRange[] elements =
        [
            new(1, Seconds(0), Seconds(10)),
        ];

        int single = LayerFinder.FindFreeLayer(elements, _ => false, s_zero, Seconds(5), requiredLayers: 1);
        int pair = LayerFinder.FindFreeLayer(elements, _ => false, s_zero, Seconds(5), requiredLayers: 2);

        Assert.That(single, Is.EqualTo(0));
        Assert.That(pair, Is.EqualTo(2));
    }

    [Test]
    public void LockedLayersAreSkipped()
    {
        int layer = LayerFinder.FindFreeLayer([], z => z is 0 or 1, s_zero, Seconds(5), requiredLayers: 2);

        Assert.That(layer, Is.EqualTo(2));
    }

    [Test]
    public void ZeroLengthIsTreatedAsAnInstant()
    {
        OccupiedRange[] elements = [new(0, Seconds(0), Seconds(10))];

        int layer = LayerFinder.FindFreeLayer(elements, _ => false, Seconds(5), s_zero);

        Assert.That(layer, Is.EqualTo(1));
    }
}
