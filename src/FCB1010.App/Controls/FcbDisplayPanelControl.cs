using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Windows.Input;

namespace FCB1010.App.Controls;

/// <summary>
/// FCB1010 configuration / display strip — calibrated to Assets/ref/display-panel-ref.png (592×77).
/// SWITCH 1 / SWITCH 2 LEDs are clickable and toggle preset relays (synced with bottom SW1/SW2 pads).
/// </summary>
public sealed class FcbDisplayPanelControl : Control
{
    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, string>(nameof(DisplayText), "00");
    public static readonly StyledProperty<bool> Switch1LedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(Switch1Led));
    public static readonly StyledProperty<bool> Switch2LedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(Switch2Led));
    public static readonly StyledProperty<bool> SelectLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(SelectLed));
    public static readonly StyledProperty<bool> NumberLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(NumberLed));
    public static readonly StyledProperty<bool> Value1LedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(Value1Led));
    public static readonly StyledProperty<bool> Value2LedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(Value2Led));
    public static readonly StyledProperty<bool> DirectSelectLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(DirectSelectLed));
    public static readonly StyledProperty<bool> MidiFunctionLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(MidiFunctionLed));
    public static readonly StyledProperty<bool> MidiChannelLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(MidiChannelLed));
    public static readonly StyledProperty<bool> ConfigLedProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, bool>(nameof(ConfigLed));
    public static readonly StyledProperty<ICommand?> ToggleRelayCommandProperty =
        AvaloniaProperty.Register<FcbDisplayPanelControl, ICommand?>(nameof(ToggleRelayCommand));

    public string DisplayText { get => GetValue(DisplayTextProperty); set => SetValue(DisplayTextProperty, value); }
    public bool Switch1Led { get => GetValue(Switch1LedProperty); set => SetValue(Switch1LedProperty, value); }
    public bool Switch2Led { get => GetValue(Switch2LedProperty); set => SetValue(Switch2LedProperty, value); }
    public bool SelectLed { get => GetValue(SelectLedProperty); set => SetValue(SelectLedProperty, value); }
    public bool NumberLed { get => GetValue(NumberLedProperty); set => SetValue(NumberLedProperty, value); }
    public bool Value1Led { get => GetValue(Value1LedProperty); set => SetValue(Value1LedProperty, value); }
    public bool Value2Led { get => GetValue(Value2LedProperty); set => SetValue(Value2LedProperty, value); }
    public bool DirectSelectLed { get => GetValue(DirectSelectLedProperty); set => SetValue(DirectSelectLedProperty, value); }
    public bool MidiFunctionLed { get => GetValue(MidiFunctionLedProperty); set => SetValue(MidiFunctionLedProperty, value); }
    public bool MidiChannelLed { get => GetValue(MidiChannelLedProperty); set => SetValue(MidiChannelLedProperty, value); }
    public bool ConfigLed { get => GetValue(ConfigLedProperty); set => SetValue(ConfigLedProperty, value); }
    public ICommand? ToggleRelayCommand { get => GetValue(ToggleRelayCommandProperty); set => SetValue(ToggleRelayCommandProperty, value); }

    private readonly SevenSegmentDisplay _display = new();
    private const double RefW = 592;
    private const double RefH = 77;

    private static readonly FontFamily PanelFont = new("Arial Narrow, Segoe UI Condensed, Segoe UI, Arial");

    static FcbDisplayPanelControl()
    {
        AffectsRender<FcbDisplayPanelControl>(
            DisplayTextProperty, Switch1LedProperty, Switch2LedProperty,
            SelectLedProperty, NumberLedProperty, Value1LedProperty, Value2LedProperty,
            DirectSelectLedProperty, MidiFunctionLedProperty, MidiChannelLedProperty, ConfigLedProperty);
        FocusableProperty.OverrideDefaultValue<FcbDisplayPanelControl>(true);
    }

    public FcbDisplayPanelControl()
    {
        LogicalChildren.Add(_display);
        VisualChildren.Add(_display);
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayTextProperty)
            _display.Text = DisplayText;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxW = double.IsInfinity(availableSize.Width) ? RefW : availableSize.Width;
        var maxH = double.IsInfinity(availableSize.Height) ? RefH : availableSize.Height;
        if (maxW <= 0 || maxH <= 0) return default;
        var scale = Math.Min(maxW / RefW, maxH / RefH);
        var size = new Size(RefW * scale, RefH * scale);
        _display.Measure(new Size(size.Width * 0.12, size.Height * 0.78));
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var scale = Math.Min(finalSize.Width / RefW, finalSize.Height / RefH);
        var w = RefW * scale;
        var h = RefH * scale;
        var ox = (finalSize.Width - w) / 2;
        var oy = (finalSize.Height - h) / 2;
        var dispW = w * 0.115;
        var dispH = h * 0.78;
        _display.Arrange(new Rect(ox + w * 0.875, oy + (h - dispH) / 2, dispW, dispH));
        _display.Text = DisplayText;
        return finalSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var hit = HitRelay(e.GetPosition(this));
        if (hit is null) return;
        if (ToggleRelayCommand?.CanExecute(hit.Value) == true)
            ToggleRelayCommand.Execute(hit.Value);
        e.Handled = true;
    }

    private int? HitRelay(Point p)
    {
        var scale = Math.Min(Bounds.Width / RefW, Bounds.Height / RefH);
        var w = RefW * scale;
        var h = RefH * scale;
        var ox = (Bounds.Width - w) / 2;
        var oy = (Bounds.Height - h) / 2;
        var sw1 = new Rect(ox + w * 0.02, oy + h * 0.20, w * 0.075, h * 0.70);
        var sw2 = new Rect(ox + w * 0.085, oy + h * 0.20, w * 0.075, h * 0.70);
        if (sw1.Contains(p)) return 1;
        if (sw2.Contains(p)) return 2;
        return null;
    }

    public override void Render(DrawingContext context)
    {
        var scale = Math.Min(Bounds.Width / RefW, Bounds.Height / RefH);
        var w = RefW * scale;
        var h = RefH * scale;
        var ox = (Bounds.Width - w) / 2;
        var oy = (Bounds.Height - h) / 2;
        var panel = new Rect(ox, oy, w, h);

        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14)), panel, 1.5f);

        var labelBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        var micro = Math.Max(4.8, 5.4 * scale);
        var header = Math.Max(5.4, 6.2 * scale);
        var type = new Typeface(PanelFont, FontStyle.Normal, FontWeight.Bold);
        double Lx(double nx) => panel.X + nx * w;
        double Ly(double ny) => panel.Y + ny * h;

        const double LedY = 0.485;
        const double LabelY = 0.64;
        const double HeaderY = 0.10;

        DrawOvalLed(context, Lx(0.065), Ly(LedY), scale, Switch1Led, red: true);
        DrawOvalLed(context, Lx(0.117), Ly(LedY), scale, Switch2Led, red: true);
        DrawCentered(context, "SWITCH 1", Lx(0.065), Ly(LabelY), micro, type, labelBrush);
        DrawCentered(context, "SWITCH 2", Lx(0.117), Ly(LabelY), micro, type, labelBrush);

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(170, 220, 220, 220)), Math.Max(0.7, scale * 0.65));
        context.DrawLine(pen, new Point(Lx(0.145), Ly(0.70)), new Point(Lx(0.231), Ly(0.70)));
        context.DrawLine(pen, new Point(Lx(0.231), Ly(0.70)), new Point(Lx(0.231), Ly(0.55)));

        DrawCentered(context, "PRESET CONFIGURATION", Lx(0.342), Ly(HeaderY), header, type, labelBrush);
        var presetXs = new[] { 0.231, 0.286, 0.342, 0.396, 0.452 };
        var presetOn = new[] { SelectLed, SelectLed, NumberLed, Value1Led, Value2Led };
        var presetLabels = new[] { "", "SELECT", "NUMBER", "VALUE 1", "VALUE 2" };
        for (var i = 0; i < 5; i++)
        {
            DrawOvalLed(context, Lx(presetXs[i]), Ly(LedY), scale, presetOn[i], red: false);
            if (presetLabels[i].Length > 0)
                DrawCentered(context, presetLabels[i], Lx(presetXs[i]), Ly(LabelY), micro, type, labelBrush);
        }

        DrawCentered(context, "GLOBAL CONFIGURATION", Lx(0.645), Ly(HeaderY), header, type, labelBrush);
        var globXs = new[] { 0.562, 0.617, 0.673, 0.728 };
        var globOn = new[] { DirectSelectLed, MidiFunctionLed, MidiChannelLed, ConfigLed };
        for (var i = 0; i < 4; i++)
        {
            DrawOvalLed(context, Lx(globXs[i]), Ly(LedY), scale, globOn[i], red: false);
            if (i == 3)
            {
                var ft = Fmt("CONFIG.", type, micro, Brushes.Black);
                var bx = Lx(globXs[i]) - ft.Width / 2 - 2.5 * scale;
                var by = Ly(0.60);
                context.FillRectangle(Brushes.White, new Rect(bx, by, ft.Width + 5 * scale, ft.Height + 1.8 * scale), (float)(1.2 * scale));
                context.DrawText(ft, new Point(bx + 2.5 * scale, by + 0.6 * scale));
            }
            else
            {
                var lines = i switch
                {
                    0 => ("DIRECT", "SELECT"),
                    1 => ("MIDI", "FUNCTION"),
                    _ => ("MIDI", "CHAN."),
                };
                DrawCentered(context, lines.Item1, Lx(globXs[i]), Ly(0.58), micro * 0.90, type, labelBrush);
                DrawCentered(context, lines.Item2, Lx(globXs[i]), Ly(0.74), micro * 0.90, type, labelBrush);
            }
        }
    }

    private static void DrawOvalLed(DrawingContext context, double cx, double cy, double scale, bool on, bool red)
    {
        var rx = Math.Max(3.2, 4.8 * scale);
        var ry = Math.Max(1.8, 2.6 * scale);
        if (on)
        {
            var core = red ? Color.FromRgb(0xE8, 0x28, 0x2E) : Color.FromRgb(0x3C, 0xD0, 0x4A);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, core.R, core.G, core.B)), null, new Point(cx, cy), rx * 1.55, ry * 1.55);
            context.DrawEllipse(new SolidColorBrush(core), null, new Point(cx, cy), rx, ry);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), null, new Point(cx - rx * 0.25, cy - ry * 0.3), rx * 0.28, ry * 0.22);
        }
        else
        {
            var dark = red ? Color.FromRgb(0x3A, 0x14, 0x14) : Color.FromRgb(0x14, 0x2A, 0x16);
            context.DrawEllipse(new SolidColorBrush(dark), null, new Point(cx, cy), rx * 0.9, ry * 0.9);
        }
    }

    private static void DrawCentered(DrawingContext context, string text, double cx, double y, double size, Typeface type, IBrush brush)
    {
        var ft = Fmt(text, type, size, brush);
        context.DrawText(ft, new Point(cx - ft.Width / 2, y));
    }

    private static FormattedText Fmt(string text, Typeface type, double size, IBrush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, type, size, brush);
}
