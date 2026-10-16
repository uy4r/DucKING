using System.Windows;
using System.Windows.Media;

namespace DucKING.Views;

/// <summary>Son N ölçümü çizgi olarak çizer; null (ölçülemeyen) noktalarda çizgi kesilir.</summary>
public static class Sparkline
{
    public static Geometry Build(IReadOnlyList<double?> values, int capacity, double width, double height, double max)
    {
        var geometry = new PathGeometry();
        if (width <= 0 || height <= 0 || max <= 0) return geometry;

        PathFigure? figure = null;
        var step = width / Math.Max(1, capacity - 1);
        var offset = capacity - values.Count; // yeni ölçümler sağdan girer
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } value)
            {
                figure = null;
                continue;
            }
            var point = new Point((offset + i) * step, height - 2 - Math.Clamp(value, 0, max) / max * (height - 4));
            if (figure is null)
            {
                figure = new PathFigure { StartPoint = point };
                geometry.Figures.Add(figure);
            }
            else
            {
                figure.Segments.Add(new LineSegment(point, true));
            }
        }
        return geometry;
    }

    public static void Push(List<double?> history, double? value, int capacity)
    {
        history.Add(value);
        if (history.Count > capacity) history.RemoveAt(0);
    }
}
