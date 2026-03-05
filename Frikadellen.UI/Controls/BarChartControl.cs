using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

namespace Frikadellen.UI.Controls;

public class BarChartControl : Control
{
    public static readonly StyledProperty<IList<double>?> ValuesProperty =
        AvaloniaProperty.Register<BarChartControl, IList<double>?>(nameof(Values));
    public static readonly StyledProperty<int> HighlightIndexProperty =
        AvaloniaProperty.Register<BarChartControl, int>(nameof(HighlightIndex), -1);

    public IList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public int HighlightIndex   { get => GetValue(HighlightIndexProperty); set => SetValue(HighlightIndexProperty, value); }

    static BarChartControl()
    {
        AffectsRender<BarChartControl>(ValuesProperty, HighlightIndexProperty);
    }

    public override void Render(DrawingContext context)
    {
        var data = Values;
        if (data == null || data.Count == 0) return;

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double max = 0;
        foreach (var v in data) if (v > max) max = v;
        if (max == 0) max = 1;

        double padding = 4;
        double chartH = h - padding * 2;
        double barW = (w - padding * 2) / data.Count;
        double gap = barW * 0.2;

        for (int i = 0; i < data.Count; i++)
        {
            double barHeight = chartH * (data[i] / max);
            double x = padding + i * barW + gap / 2;
            double y = padding + chartH - barHeight;
            double bw = barW - gap;

            bool highlight = i == HighlightIndex;
            var color = highlight ? Color.Parse("#E879F9") : Color.Parse("#818CF8");
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = {
                    new GradientStop(Color.FromArgb(255, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(100, color.R, color.G, color.B), 1)
                }
            };

            var rect = new Rect(x, y, bw, barHeight);
            context.DrawRectangle(brush, null, rect, 3, 3);
        }
    }
}
