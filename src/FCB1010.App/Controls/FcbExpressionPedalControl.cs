using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FCB1010.App.Controls;

/// <summary>
/// FCB1010 expression pedal — photo-accurate tread surface with bottom→top travel fill (0–127).
/// </summary>
public sealed class FcbExpressionPedalControl : Control
{
    public static readonly StyledProperty<string> PedalIdProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, string>(nameof(PedalId), "A");
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, int>(nameof(Value), 0);
    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, int>(nameof(Minimum), 0);
    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, int>(nameof(Maximum), 127);
    public static readonly StyledProperty<bool> IsEnabledPedalProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, bool>(nameof(IsEnabledPedal), true);
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsDraggingProperty =
        AvaloniaProperty.Register<FcbExpressionPedalControl, bool>(nameof(IsDragging));

    public string PedalId { get => GetValue(PedalIdProperty); set => SetValue(PedalIdProperty, value); }
    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public bool IsEnabledPedal { get => GetValue(IsEnabledPedalProperty); set => SetValue(IsEnabledPedalProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsDragging { get => GetValue(IsDraggingProperty); set => SetValue(IsDraggingProperty, value); }

    public event EventHandler<int>? ValueCommitted;

    private static readonly Bitmap? PedalBitmap = TryLoad();

    private const double RefW = 211;
    private const double RefH = 514;

    static FcbExpressionPedalControl()
    {
        AffectsRender<FcbExpressionPedalControl>(
            ValueProperty, MinimumProperty, MaximumProperty, IsSelectedProperty, IsDraggingProperty, IsEnabledPedalProperty);
        FocusableProperty.OverrideDefaultValue<FcbExpressionPedalControl>(true);
    }

    public FcbExpressionPedalControl()
    {
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        MinWidth = 56;
        MinHeight = 140;
        ClipToBounds = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpressionPedalAutomationPeer(this);

    private sealed class ExpressionPedalAutomationPeer(FcbExpressionPedalControl owner)
        : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private FcbExpressionPedalControl Pedal => (FcbExpressionPedalControl)Owner;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
        protected override string GetClassNameCore() => nameof(FcbExpressionPedalControl);
        public bool IsReadOnly => !Pedal.IsEnabled || !Pedal.IsEnabledPedal;
        public double Minimum => Pedal.Minimum;
        public double Maximum => Pedal.Maximum;
        public double Value => Pedal.Value;
        public double LargeChange => 10;
        public double SmallChange => 1;
        public void SetValue(double value)
        {
            if (IsReadOnly) return;
            Pedal.Value = Math.Clamp((int)Math.Round(value), Pedal.Minimum, Pedal.Maximum);
            Pedal.ValueCommitted?.Invoke(Pedal, Pedal.Value);
        }
    }

    private static Bitmap? TryLoad()
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri("avares://FCB1010Studio/Assets/ref/exp-pedal-ref.png")));
        }
        catch { return null; }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxW = double.IsInfinity(availableSize.Width) ? RefW * 0.45 : availableSize.Width;
        var maxH = double.IsInfinity(availableSize.Height) ? RefH * 0.45 : availableSize.Height;
        if (maxW <= 0 || maxH <= 0) return default;
        var scale = Math.Min(maxW / RefW, maxH / RefH);
        return new Size(RefW * scale, RefH * scale);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 2) return;

        // Subtle tilt: heel (0) flat, toe (127) foreshortens from bottom pivot.
        var t = Math.Clamp(Value / 127.0, 0, 1);
        var skew = t * 0.012;
        using (context.PushTransform(Matrix.CreateTranslation(0, t * 2)))
        {
            if (PedalBitmap is not null)
                context.DrawImage(PedalBitmap, new Rect(0, 0, PedalBitmap.PixelSize.Width, PedalBitmap.PixelSize.Height), bounds);
            else
                DrawFallbackPedal(context, bounds);

            // Bottom→top travel illumination — very restrained so tread photo stays dominant
            if (t > 0.04)
            {
                var fillH = bounds.Height * t;
                var fill = new Rect(bounds.X + 4, bounds.Bottom - fillH, bounds.Width - 8, fillH);
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)(16 + t * 22), 0xA0, 0x20, 0x28), 0),
                        new GradientStop(Color.FromArgb((byte)(6 + t * 10), 0x50, 0x12, 0x16), 0.7),
                        new GradientStop(Color.FromArgb(0, 0, 0, 0), 1),
                    }
                };
                context.FillRectangle(brush, fill);
            }
        }

        if (IsSelected)
        {
            context.DrawRectangle(null,
                new Pen(new SolidColorBrush(Color.FromArgb(100, 224, 36, 48)), Math.Max(1, bounds.Width / 80)),
                new Rect(0.5, 0.5, bounds.Width - 1, bounds.Height - 1), 3);
        }

        if (IsDragging)
        {
            var label = $"EXP {PedalId}  {Value:000}";
            var ft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Cascadia Mono", FontStyle.Normal, FontWeight.Bold),
                Math.Max(10, bounds.Width * 0.14), Brushes.White);
            var bg = new Rect((bounds.Width - ft.Width) / 2 - 4, 6, ft.Width + 8, ft.Height + 4);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 10, 10, 12)), bg, 2);
            context.DrawText(ft, new Point(bg.X + 4, bg.Y + 2));
        }

        _ = skew; // reserved for stronger perspective if needed
    }

    private static void DrawFallbackPedal(DrawingContext context, Rect bounds)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), bounds, 3);
        var tread = new Rect(bounds.X + 4, bounds.Y + bounds.Height * 0.22, bounds.Width - 8, bounds.Height * 0.74);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)), tread, 2);
        var cols = 7;
        for (var i = 0; i < cols; i++)
        {
            var x = tread.X + (tread.Width / cols) * i + 1;
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C)),
                new Rect(x, tread.Y, tread.Width / cols - 2, tread.Height), 1);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        IsDragging = true;
        e.Pointer.Capture(this);
        SetFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsDragging) return;
        SetFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsDragging) return;
        IsDragging = false;
        e.Pointer.Capture(null);
        ValueCommitted?.Invoke(this, Value);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var delta = e.Delta.Y > 0 ? 1 : -1;
        Value = Math.Clamp(Value + delta * (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 8 : 1), 0, 127);
        ValueCommitted?.Invoke(this, Value);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 8 : 1;
        switch (e.Key)
        {
            case Key.Up: Value = Math.Clamp(Value + step, 0, 127); e.Handled = true; break;
            case Key.Down: Value = Math.Clamp(Value - step, 0, 127); e.Handled = true; break;
            case Key.Home: Value = 0; e.Handled = true; break;
            case Key.End: Value = 127; e.Handled = true; break;
            case Key.PageUp: Value = Math.Clamp(Value + 16, 0, 127); e.Handled = true; break;
            case Key.PageDown: Value = Math.Clamp(Value - 16, 0, 127); e.Handled = true; break;
        }
        if (e.Handled) ValueCommitted?.Invoke(this, Value);
    }

    private void SetFromPoint(Point p)
    {
        // BOTTOM = 0, TOP = 127
        var t = 1.0 - Math.Clamp(p.Y / Math.Max(1, Bounds.Height), 0, 1);
        Value = (int)Math.Round(t * 127);
    }
}
