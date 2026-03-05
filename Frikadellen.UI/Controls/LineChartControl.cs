using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

namespace Frikadellen.UI.Controls;

public class LineChartControl : Control
{
    public static readonly StyledProperty<IList<double>?> DataPointsProperty =
        AvaloniaProperty.Register<LineChartControl, IList<double>?>(nameof(DataPoints));

    public IList<double>? DataPoints
    {
        get => GetValue(DataPointsProperty);
        set => SetValue(DataPointsProperty, value);
    }

    static LineChartControl()
    {
        DataPointsProperty.Changed.AddClassHandler<LineChartControl>((c, _) => c.InvalidateVisual());
        AffectsRender<LineChartControl>(DataPointsProperty);
    }

    public override void Render(DrawingContext context)
    {
        var data = DataPoints;
        if (data == null || data.Count < 2) return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double min = double.MaxValue, max = double.MinValue;
        foreach (var v in data) { if (v < min) min = v; if (v > max) max = v; }
        if (max == min) { max = min + 1; }

        double padding = 8;
        double chartH = h - padding * 2;
        double chartW = w - padding * 2;

        var points = new List<Point>();
        for (int i = 0; i < data.Count; i++)
        {
            double x = padding + i * chartW / (data.Count - 1);
            double y = padding + chartH - (data[i] - min) / (max - min) * chartH;
            points.Add(new Point(x, y));
        }

        // Draw filled area
        var fillGeo = new PathGeometry();
        var fillFigure = new PathFigure { StartPoint = new Point(points[0].X, h), IsClosed = true, Segments = new PathSegments() };
        foreach (var p in points) fillFigure.Segments.Add(new LineSegment { Point = p });
        fillFigure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, h) });
        fillGeo.Figures ??= new PathFigures();
        fillGeo.Figures.Add(fillFigure);

        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#50818CF8"), 0), new GradientStop(Color.Parse("#00818CF8"), 1) }
        };
        context.DrawGeometry(fillBrush, null, fillGeo);

        // Draw line
        var linePen = new Pen(new SolidColorBrush(Color.Parse("#818CF8")), 2);
        for (int i = 1; i < points.Count; i++)
            context.DrawLine(linePen, points[i - 1], points[i]);
    }
}
