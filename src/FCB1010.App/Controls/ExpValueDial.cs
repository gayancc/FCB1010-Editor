using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FCB1010.App.Controls;

/// <summary>
/// Instrument-style MIDI value dial (0–127) — circular sweep with centered readout.
/// Replaces immature linear progress bars with a console/plugin meter aesthetic.
/// </summary>
public sealed class ExpValueDial : Control
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<ExpValueDial, int>(nameof(Value));
    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<ExpValueDial, int>(nameof(Minimum), 0);
    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<ExpValueDial, int>(nameof(Maximum), 127);
    public static readonly StyledProperty<Color> AccentProperty =
        AvaloniaProperty.Register<ExpValueDial, Color>(nameof(Accent), Color.FromRgb(0xFF, 0x9F, 0x1C));
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<ExpValueDial, string>(nameof(Caption), "");

    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public Color Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    static ExpValueDial()
    {
        AffectsRender<ExpValueDial>(ValueProperty, MinimumProperty, MaximumProperty, AccentProperty, CaptionProperty);
    }

    public ExpValueDial()
    {
        Width = 72;
        Height = 72;
        MinWidth = 64;
        MinHeight = 64;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = 72.0;
        if (!double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
            side = Math.Min(side, availableSize.Width);
        if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
            side = Math.Min(side, availableSize.Height);
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 8 || b.Height < 8) return;

        var cx = b.Width / 2;
        var cy = b.Height / 2;
        var radius = Math.Min(b.Width, b.Height) / 2 - 3;
        var stroke = Math.Max(3.5, radius * 0.11);

        // Sweep: 135° (SW) → clockwise 270° ending at 45° (SE) — classic gain-knob geometry
        const double startDeg = 135;
        const double sweepDeg = 270;
        var range = Math.Max(1, Maximum - Minimum);
        var t = Math.Clamp((Value - Minimum) / (double)range, 0, 1);
        var valueSweep = sweepDeg * t;

        // Outer ghost ring
        context.DrawEllipse(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1),
            new Point(cx, cy), radius + 1.5, radius + 1.5);

        // Track arc (full range)
        DrawArc(context, cx, cy, radius, startDeg, sweepDeg,
            new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1F, 0x26)), stroke)
            {
                LineCap = PenLineCap.Round,
            });

        // Active value arc
        if (valueSweep > 0.5)
        {
            var accent = Accent;
            DrawArc(context, cx, cy, radius, startDeg, valueSweep,
                new Pen(new SolidColorBrush(accent), stroke)
                {
                    LineCap = PenLineCap.Round,
                });

            // Soft glow under the tip
            var tipAngle = (startDeg + valueSweep) * Math.PI / 180.0;
            var tip = new Point(cx + Math.Cos(tipAngle) * radius, cy + Math.Sin(tipAngle) * radius);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(90, accent.R, accent.G, accent.B)),
                null, tip, stroke * 0.85, stroke * 0.85);
            context.DrawEllipse(
                new SolidColorBrush(accent),
                null, tip, stroke * 0.35, stroke * 0.35);
        }

        // Nested tick marks at 0 / mid / max
        DrawTick(context, cx, cy, radius, stroke, startDeg, 0.55);
        DrawTick(context, cx, cy, radius, stroke, startDeg + sweepDeg * 0.5, 0.4);
        DrawTick(context, cx, cy, radius, stroke, startDeg + sweepDeg, 0.55);

        // Center readout
        var valueText = Value.ToString("000");
        var ft = new FormattedText(
            valueText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(Mono, FontStyle.Normal, FontWeight.Bold),
            Math.Max(14, radius * 0.52),
            new SolidColorBrush(Accent));
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2 - (string.IsNullOrEmpty(Caption) ? 0 : 2)));

        if (!string.IsNullOrEmpty(Caption))
        {
            var cap = new FormattedText(
                Caption,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(Mono, FontStyle.Normal, FontWeight.SemiBold),
                Math.Max(7, radius * 0.18),
                new SolidColorBrush(Color.FromRgb(0x6A, 0x73, 0x7C)));
            context.DrawText(cap, new Point(cx - cap.Width / 2, cy + ft.Height * 0.35));
        }
    }

    private static void DrawTick(DrawingContext context, double cx, double cy, double radius, double stroke, double deg, double lenScale)
    {
        var rad = deg * Math.PI / 180.0;
        var inner = radius - stroke * 0.15;
        var outer = radius + stroke * lenScale;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(100, 200, 200, 210)), Math.Max(1, stroke * 0.18));
        context.DrawLine(pen,
            new Point(cx + Math.Cos(rad) * inner, cy + Math.Sin(rad) * inner),
            new Point(cx + Math.Cos(rad) * outer, cy + Math.Sin(rad) * outer));
    }

    private static void DrawArc(DrawingContext context, double cx, double cy, double radius, double startDeg, double sweepDeg, Pen pen)
    {
        if (sweepDeg <= 0) return;
        // Approximate arc with a polyline of short segments for Avalonia Geometry
        const int steps = 48;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                var deg = startDeg + sweepDeg * t;
                var rad = deg * Math.PI / 180.0;
                var pt = new Point(cx + Math.Cos(rad) * radius, cy + Math.Sin(rad) * radius);
                if (i == 0) ctx.BeginFigure(pt, false);
                else ctx.LineTo(pt);
            }
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geo);
    }
}
