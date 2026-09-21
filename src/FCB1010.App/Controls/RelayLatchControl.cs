using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace FCB1010.App.Controls;

/// <summary>
/// Electromechanical relay latch — contact studs + bridging armature + jewel LED.
/// Closed state mirrors FCB1010 SWITCH 1 / SWITCH 2 (not a generic toggle circle).
/// </summary>
public sealed class RelayLatchControl : Control
{
    public static readonly StyledProperty<bool> IsClosedProperty =
        AvaloniaProperty.Register<RelayLatchControl, bool>(nameof(IsClosed));
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RelayLatchControl, string>(nameof(Label), "SW1");
    public static readonly StyledProperty<string> SubLabelProperty =
        AvaloniaProperty.Register<RelayLatchControl, string>(nameof(SubLabel), "RELAY");

    public bool IsClosed { get => GetValue(IsClosedProperty); set => SetValue(IsClosedProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string SubLabel { get => GetValue(SubLabelProperty); set => SetValue(SubLabelProperty, value); }

    public event EventHandler? Toggled;

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");
    private static readonly FontFamily Ui = new("Bahnschrift, Segoe UI Variable Display, Segoe UI");

    static RelayLatchControl()
    {
        AffectsRender<RelayLatchControl>(IsClosedProperty, LabelProperty, SubLabelProperty);
        FocusableProperty.OverrideDefaultValue<RelayLatchControl>(true);
    }

    public RelayLatchControl()
    {
        Width = 88;
        Height = 92;
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = false;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(availableSize.Width, 88), Math.Min(availableSize.Height, 92));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        IsClosed = !IsClosed;
        Toggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter)
        {
            IsClosed = !IsClosed;
            Toggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        if (b.Width < 8) return;

        var closed = IsClosed;

        // Chassis plate
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x0E, 0x11, 0x14)), b, 8);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(closed ? Color.FromRgb(0x5A, 0x28, 0x20) : Color.FromRgb(0x22, 0x28, 0x30)), 1),
            new Rect(0.5, 0.5, b.Width - 1, b.Height - 1), 8);

        // Inner well
        var well = new Rect(8, 22, b.Width - 16, 48);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x0A, 0x0C)), well, 5);
        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.8),
            well, 5);

        // Contact studs (left / right)
        var studY = well.Y + well.Height * 0.62;
        var leftX = well.X + well.Width * 0.28;
        var rightX = well.X + well.Width * 0.72;
        DrawStud(context, leftX, studY);
        DrawStud(context, rightX, studY);

        // Bridging armature
        var barY = closed ? studY - 7 : studY - 18;
        var barTilt = closed ? 0.0 : -0.22;
        var barW = rightX - leftX + 10;
        var barH = 5.0;
        var midX = (leftX + rightX) / 2;

        using (context.PushTransform(
                   Matrix.CreateTranslation(-midX, -barY) *
                   Matrix.CreateRotation(barTilt) *
                   Matrix.CreateTranslation(midX, barY)))
        {
            var bar = new Rect(midX - barW / 2, barY - barH / 2, barW, barH);
            context.FillRectangle(
                new SolidColorBrush(closed ? Color.FromRgb(0xC8, 0xB0, 0x70) : Color.FromRgb(0x4A, 0x52, 0x5A)),
                bar, 1.5f);
            // Highlight edge
            context.DrawLine(
                new Pen(new SolidColorBrush(Color.FromArgb(closed ? (byte)160 : (byte)40, 255, 230, 160)), 1),
                new Point(bar.X + 2, bar.Y + 1),
                new Point(bar.Right - 2, bar.Y + 1));
        }

        // Pivot pin on left stud
        context.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x36)),
            new Pen(new SolidColorBrush(Color.FromRgb(0x6A, 0x72, 0x7A)), 0.8),
            new Point(leftX, studY - (closed ? 7 : 14)), 2.2, 2.2);

        // Jewel LED (hardware red SWITCH indicator)
        var ledCx = b.Width * 0.5;
        var ledCy = 12.0;
        var ledR = 4.2;
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x10, 0x0C, 0x0C)), null, new Point(ledCx, ledCy), ledR + 1.4, ledR + 1.4);
        if (closed)
        {
            var core = Color.FromRgb(0xE0, 0x28, 0x2E);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, core.R, core.G, core.B)), null, new Point(ledCx, ledCy), ledR * 1.7, ledR * 1.7);
            context.DrawEllipse(new SolidColorBrush(core), null, new Point(ledCx, ledCy), ledR * 0.85, ledR * 0.85);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 210, 210)), null,
                new Point(ledCx - ledR * 0.25, ledCy - ledR * 0.25), ledR * 0.25, ledR * 0.25);
        }
        else
        {
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x3A, 0x14, 0x14)), null, new Point(ledCx, ledCy), ledR * 0.85, ledR * 0.85);
        }

        // Labels
        var state = closed ? "CLOSED" : "OPEN";
        var stateBrush = closed
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x3A))
            : new SolidColorBrush(Color.FromRgb(0x5C, 0x65, 0x6E));
        var stateFt = Fmt(state, Mono, 8, stateBrush, FontWeight.Bold);
        context.DrawText(stateFt, new Point((b.Width - stateFt.Width) / 2, well.Bottom + 4));

        var lab = Fmt(Label, Ui, 11, new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xED)), FontWeight.Bold);
        context.DrawText(lab, new Point(10, 4));

        var sub = Fmt(SubLabel, Mono, 7, new SolidColorBrush(Color.FromRgb(0x5C, 0x65, 0x6E)), FontWeight.SemiBold);
        context.DrawText(sub, new Point(b.Width - sub.Width - 8, 5));
    }

    private static void DrawStud(DrawingContext context, double x, double y)
    {
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x22)), null, new Point(x, y), 6, 6);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x6A, 0x72, 0x7A)), null, new Point(x, y), 3.5, 3.5);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xC0, 0xC6, 0xCC)), null, new Point(x - 0.8, y - 0.8), 1.2, 1.2);
    }

    private static FormattedText Fmt(string text, FontFamily family, double size, IBrush brush, FontWeight weight) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyle.Normal, weight), size, brush);
}
