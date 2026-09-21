using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FCB1010.App.Controls;

/// <summary>Single 7-segment digit with independent A–G segments (FCB1010 amber style).</summary>
public sealed class SevenSegmentDigit : Control
{
    public static readonly StyledProperty<int?> DigitProperty =
        AvaloniaProperty.Register<SevenSegmentDigit, int?>(nameof(Digit));
    public static readonly StyledProperty<bool> ShowOffSegmentsProperty =
        AvaloniaProperty.Register<SevenSegmentDigit, bool>(nameof(ShowOffSegments), true);

    public int? Digit { get => GetValue(DigitProperty); set => SetValue(DigitProperty, value); }
    public bool ShowOffSegments { get => GetValue(ShowOffSegmentsProperty); set => SetValue(ShowOffSegmentsProperty, value); }

    private static readonly byte[] Patterns =
    [
        0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011,
        0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011,
    ];

    static SevenSegmentDigit()
    {
        AffectsRender<SevenSegmentDigit>(DigitProperty, ShowOffSegmentsProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var h = double.IsInfinity(availableSize.Height) ? 36 : Math.Max(8, availableSize.Height);
        var w = h * 0.62;
        if (!double.IsInfinity(availableSize.Width) && availableSize.Width > 0 && availableSize.Width < w)
        {
            w = availableSize.Width;
            h = w / 0.62;
        }
        return new Size(w, h);
    }

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 2) return;

        // Hardware readout is warm amber/orange (not pure red)
        var on = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x18));
        var glow = new SolidColorBrush(Color.FromArgb(75, 0xFF, 0x70, 0x10));
        var off = new SolidColorBrush(Color.FromRgb(0x4A, 0x18, 0x10));

        byte pattern = 0;
        if (Digit is >= 0 and <= 9) pattern = Patterns[Digit.Value];

        DrawSeg(context, b, true, (pattern & 0b1000000) != 0, on, glow, off);
        DrawSeg(context, b, false, (pattern & 0b0100000) != 0, on, glow, off, right: true, top: true);
        DrawSeg(context, b, false, (pattern & 0b0010000) != 0, on, glow, off, right: true, top: false);
        DrawSeg(context, b, true, (pattern & 0b0001000) != 0, on, glow, off, bottom: true);
        DrawSeg(context, b, false, (pattern & 0b0000100) != 0, on, glow, off, right: false, top: false);
        DrawSeg(context, b, false, (pattern & 0b0000010) != 0, on, glow, off, right: false, top: true);
        DrawSeg(context, b, true, (pattern & 0b0000001) != 0, on, glow, off, mid: true);
    }

    private void DrawSeg(
        DrawingContext ctx, Rect b, bool horizontal, bool lit,
        IBrush on, IBrush glow, IBrush offBrush,
        bool right = false, bool top = true, bool bottom = false, bool mid = false)
    {
        if (!lit && !ShowOffSegments) return;
        var brush = lit ? on : offBrush;
        var t = Math.Max(1.4, b.Width * 0.13);
        var pad = b.Width * 0.08;
        var midY = b.Height * 0.5;

        StreamGeometry geo;
        if (horizontal)
        {
            double y = bottom ? b.Bottom - pad - t : mid ? midY - t * 0.5 : b.Y + pad;
            geo = HSeg(b.X + pad + t * 0.25, y, b.Width - pad * 2 - t * 0.5, t);
        }
        else
        {
            var x = right ? b.Right - pad - t : b.X + pad;
            geo = top
                ? VSeg(x, b.Y + pad + t * 0.35, t, midY - b.Y - pad - t * 0.6)
                : VSeg(x, midY + t * 0.15, t, b.Bottom - midY - pad - t * 0.6);
        }

        if (lit) ctx.DrawGeometry(glow, null, geo);
        ctx.DrawGeometry(brush, null, geo);
    }

    private static StreamGeometry HSeg(double x, double y, double w, double t)
    {
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(x + t * 0.35, y), true);
        ctx.LineTo(new Point(x + w - t * 0.35, y));
        ctx.LineTo(new Point(x + w, y + t * 0.5));
        ctx.LineTo(new Point(x + w - t * 0.35, y + t));
        ctx.LineTo(new Point(x + t * 0.35, y + t));
        ctx.LineTo(new Point(x, y + t * 0.5));
        ctx.EndFigure(true);
        return g;
    }

    private static StreamGeometry VSeg(double x, double y, double t, double h)
    {
        h = Math.Max(t, h);
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(new Point(x + t * 0.5, y), true);
        ctx.LineTo(new Point(x + t, y + t * 0.35));
        ctx.LineTo(new Point(x + t, y + h - t * 0.35));
        ctx.LineTo(new Point(x + t * 0.5, y + h));
        ctx.LineTo(new Point(x, y + h - t * 0.35));
        ctx.LineTo(new Point(x, y + t * 0.35));
        ctx.EndFigure(true);
        return g;
    }
}

/// <summary>Two-digit FCB1010 seven-segment readout.</summary>
public sealed class SevenSegmentDisplay : Control
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SevenSegmentDisplay, string>(nameof(Text), "00");

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private readonly SevenSegmentDigit _tens = new();
    private readonly SevenSegmentDigit _ones = new();

    static SevenSegmentDisplay()
    {
        AffectsRender<SevenSegmentDisplay>(TextProperty);
        AffectsArrange<SevenSegmentDisplay>(TextProperty);
    }

    public SevenSegmentDisplay()
    {
        LogicalChildren.Add(_tens);
        LogicalChildren.Add(_ones);
        VisualChildren.Add(_tens);
        VisualChildren.Add(_ones);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) ApplyDigits();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyDigits();
    }

    private void ApplyDigits()
    {
        var text = (Text ?? "00").PadLeft(2)[^2..];
        _tens.Digit = text[0] is >= '0' and <= '9' ? text[0] - '0' : null;
        _ones.Digit = text[1] is >= '0' and <= '9' ? text[1] - '0' : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var h = double.IsInfinity(availableSize.Height) ? 40 : Math.Max(16, availableSize.Height);
        var w = double.IsInfinity(availableSize.Width) ? h * 1.4 : Math.Max(20, availableSize.Width);
        var digitSize = new Size((w - 10) / 2, h - 8);
        _tens.Measure(digitSize);
        _ones.Measure(digitSize);
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var digitW = (finalSize.Width - 10) / 2;
        var digitH = finalSize.Height - 8;
        _tens.Arrange(new Rect(4, 4, digitW, digitH));
        _ones.Arrange(new Rect(6 + digitW, 4, digitW, digitH));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x28, 0x08, 0x08)), b, 2);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x04, 0x04)),
            new Rect(2, 2, b.Width - 4, b.Height - 4), 1);
    }
}
