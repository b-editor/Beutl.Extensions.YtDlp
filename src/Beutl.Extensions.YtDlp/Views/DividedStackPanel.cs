using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Beutl.Extensions.YtDlp.Views;

/// <summary>
/// Stacks rows and draws an inset divider between visible ones, mirroring Beutl's OptionsGroupPanel
/// (Beutl.Controls is not part of the extension package graph, so it cannot be referenced).
/// </summary>
public sealed class DividedStackPanel : StackPanel
{
    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<DividedStackPanel, IBrush?>(nameof(DividerBrush));

    private double[] _dividerPositions = [];
    private readonly DividerLayer _dividerLayer;

    public DividedStackPanel()
    {
        Spacing = 1;
        // Panel.Render is sealed, so the dividers live in their own visual. Keeping it out of
        // Children keeps it away from item generation, indexing and measurement.
        _dividerLayer = new DividerLayer(this) { IsHitTestVisible = false };
        VisualChildren.Add(_dividerLayer);
    }

    public IBrush? DividerBrush
    {
        get => GetValue(DividerBrushProperty);
        set => SetValue(DividerBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DividerBrushProperty)
            _dividerLayer?.InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size result = base.ArrangeOverride(finalSize);

        List<double> positions = [];
        Control? previous = null;
        foreach (Control child in Children)
        {
            if (!child.IsVisible || child.Bounds.Height <= 0) continue;

            if (previous != null)
                positions.Add((previous.Bounds.Bottom + child.Bounds.Top) / 2);
            previous = child;
        }

        if (!_dividerPositions.SequenceEqual(positions))
        {
            _dividerPositions = [.. positions];
            _dividerLayer.InvalidateVisual();
        }

        _dividerLayer.Measure(finalSize);
        _dividerLayer.Arrange(new Rect(finalSize));
        return result;
    }

    private sealed class DividerLayer(DividedStackPanel owner) : Control
    {
        // Same inset as the row padding, like Beutl's grouped lists.
        private const double Inset = 12;

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (owner.DividerBrush is null || Bounds.Width <= Inset * 2) return;

            var pen = new Pen(owner.DividerBrush);
            foreach (double y in owner._dividerPositions)
                context.DrawLine(pen, new Point(Inset, y), new Point(Bounds.Width - Inset, y));
        }
    }
}
