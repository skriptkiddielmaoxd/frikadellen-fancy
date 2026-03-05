using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Frikadellen.UI.Controls;

public class DonutChartControl : Control
{
    public static readonly StyledProperty<double> SuccessRateProperty =
        AvaloniaProperty.Register<DonutChartControl, double>(nameof(SuccessRate), 0.87);
    public static readonly StyledProperty<double> FailRateProperty =
        AvaloniaProperty.Register<DonutChartControl, double>(nameof(FailRate), 0.10);
    public static readonly StyledProperty<double> PendingRateProperty =
        AvaloniaProperty.Register<DonutChartControl, double>(nameof(PendingRate), 0.03);

    public double SuccessRate { get => GetValue(SuccessRateProperty); set => SetValue(SuccessRateProperty, value); }
    public double FailRate    { get => GetValue(FailRateProperty);    set => SetValue(FailRateProperty,    value); }
    public double PendingRate { get => GetValue(PendingRateProperty); set => SetValue(PendingRateProperty, value); }

    static DonutChartControl()
    {
        AffectsRender<DonutChartControl>(SuccessRateProperty, FailRateProperty, PendingRateProperty);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double size = Math.Min(w, h);
        double cx = w / 2, cy = h / 2;
        double outerR = size / 2 - 4;
        double innerR = outerR * 0.62;

        DrawSlice(context, cx, cy, outerR, innerR, 0, SuccessRate, Color.Parse("#34D399"));
        DrawSlice(context, cx, cy, outerR, innerR, SuccessRate, SuccessRate + FailRate, Color.Parse("#F87171"));
        DrawSlice(context, cx, cy, outerR, innerR, SuccessRate + FailRate, 1.0, Color.Parse("#FBBF24"));

        // Center text
        var pct = (int)(SuccessRate * 100);
        var ft = new FormattedText(
            $"{pct}%",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            size * 0.18,
            new SolidColorBrush(Color.Parse("#E0E7FF")));
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    private static void DrawSlice(DrawingContext ctx, double cx, double cy, double outerR, double innerR,
        double startFrac, double endFrac, Color color)
    {
        if (endFrac <= startFrac) return;
        double startAngle = startFrac * 360 - 90;
        double endAngle   = endFrac   * 360 - 90;
        double sweep = endAngle - startAngle;
        if (sweep <= 0) return;
        bool isLarge = sweep > 180;

        double sa = startAngle * Math.PI / 180;
        double ea = endAngle   * Math.PI / 180;

        var p1 = new Point(cx + outerR * Math.Cos(sa), cy + outerR * Math.Sin(sa));
        var p2 = new Point(cx + outerR * Math.Cos(ea), cy + outerR * Math.Sin(ea));
        var p3 = new Point(cx + innerR * Math.Cos(ea), cy + innerR * Math.Sin(ea));
        var p4 = new Point(cx + innerR * Math.Cos(sa), cy + innerR * Math.Sin(sa));

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = p1, IsClosed = true, Segments = new PathSegments() };
        fig.Segments.Add(new ArcSegment { Point = p2, Size = new Size(outerR, outerR), SweepDirection = SweepDirection.Clockwise, IsLargeArc = isLarge });
        fig.Segments.Add(new LineSegment { Point = p3 });
        fig.Segments.Add(new ArcSegment { Point = p4, Size = new Size(innerR, innerR), SweepDirection = SweepDirection.CounterClockwise, IsLargeArc = isLarge });
        geo.Figures ??= new PathFigures();
        geo.Figures.Add(fig);

        ctx.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}
