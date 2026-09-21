using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Windows.Input;

namespace FCB1010.App.Controls;

/// <summary>
/// FCB1010 footswitch — photo housing/rocker with silk calibrated to Assets/ref/footswitch-ref.png (117×224).
/// Pixel anchors (measured): LED (38,26) Ø9; number bbox ~43–66×36–72 center (54,54) h=36;
/// primary label right-edge x=109 y≈8–23; reverse pill y≈26–33.
/// </summary>
public sealed class FcbFootswitchControl : Control
{
    public static readonly StyledProperty<string> IdentityProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, string>(nameof(Identity), "1");
    public static readonly StyledProperty<string> PrimaryLabelProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, string>(nameof(PrimaryLabel), "");
    public static readonly StyledProperty<string?> SecondaryLabelProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, string?>(nameof(SecondaryLabel));
    public static readonly StyledProperty<bool> SecondaryBoxedProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, bool>(nameof(SecondaryBoxed));
    public static readonly StyledProperty<bool> IdentityBoxedProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, bool>(nameof(IdentityBoxed));
    public static readonly StyledProperty<FootswitchLedState> LedStateProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, FootswitchLedState>(nameof(LedState));
    public static readonly StyledProperty<bool> IsPressedProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, bool>(nameof(IsPressed));
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, bool>(nameof(IsSelected));
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, ICommand?>(nameof(Command));
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<FcbFootswitchControl, object?>(nameof(CommandParameter));

    public string Identity { get => GetValue(IdentityProperty); set => SetValue(IdentityProperty, value); }
    public string PrimaryLabel { get => GetValue(PrimaryLabelProperty); set => SetValue(PrimaryLabelProperty, value); }
    public string? SecondaryLabel { get => GetValue(SecondaryLabelProperty); set => SetValue(SecondaryLabelProperty, value); }
    public bool SecondaryBoxed { get => GetValue(SecondaryBoxedProperty); set => SetValue(SecondaryBoxedProperty, value); }
    public bool IdentityBoxed { get => GetValue(IdentityBoxedProperty); set => SetValue(IdentityBoxedProperty, value); }
    public FootswitchLedState LedState { get => GetValue(LedStateProperty); set => SetValue(LedStateProperty, value); }
    public bool IsPressed { get => GetValue(IsPressedProperty); set => SetValue(IsPressedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    private static readonly Bitmap? Housing = TryLoad();

    private const double RefW = 117;
    private const double RefH = 224;

    // Heavy upright sans — hardware digits are Helvetica-Bold style, not italic Black
    private static readonly FontFamily LabelFont = new("Arial Narrow, Segoe UI Condensed, Segoe UI, Arial");
    private static readonly FontFamily IdentityFont = new("Arial, Helvetica, Segoe UI");

    static FcbFootswitchControl()
    {
        AffectsRender<FcbFootswitchControl>(
            IdentityProperty, PrimaryLabelProperty, SecondaryLabelProperty, SecondaryBoxedProperty,
            IdentityBoxedProperty, LedStateProperty, IsPressedProperty, IsSelectedProperty);
        FocusableProperty.OverrideDefaultValue<FcbFootswitchControl>(true);
    }

    public FcbFootswitchControl()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        MinWidth = 44;
        MinHeight = 84;
        ClipToBounds = true;
    }

    private static Bitmap? TryLoad()
    {
        try { return new Bitmap(AssetLoader.Open(new Uri("avares://FCB1010Studio/Assets/ref/footswitch-blank.png"))); }
        catch
        {
            try { return new Bitmap(AssetLoader.Open(new Uri("avares://FCB1010Studio/Assets/ref/footswitch-ref.png"))); }
            catch { return null; }
        }
    }

    public void ApplyFactoryFace(int pedalNumber)
    {
        var face = FcbHardwareLabels.ForPedal(pedalNumber);
        Identity = face.Identity;
        PrimaryLabel = face.Primary;
        SecondaryLabel = face.Secondary;
        SecondaryBoxed = face.SecondaryBoxed;
        IdentityBoxed = face.IdentityBoxed;
    }

    public void ApplyUpFace()
    {
        var face = FcbHardwareLabels.Up;
        Identity = face.Identity; PrimaryLabel = face.Primary;
        SecondaryLabel = face.Secondary; SecondaryBoxed = face.SecondaryBoxed;
        IdentityBoxed = face.IdentityBoxed;
    }

    public void ApplyDownFace()
    {
        var face = FcbHardwareLabels.Down;
        Identity = face.Identity; PrimaryLabel = face.Primary;
        SecondaryLabel = face.Secondary; SecondaryBoxed = face.SecondaryBoxed;
        IdentityBoxed = face.IdentityBoxed;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxW = double.IsInfinity(availableSize.Width) ? RefW : availableSize.Width;
        var maxH = double.IsInfinity(availableSize.Height) ? RefH : availableSize.Height;
        if (maxW <= 0 || maxH <= 0) return default;
        var scale = Math.Min(maxW / RefW, maxH / RefH);
        return new Size(RefW * scale, RefH * scale);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 2) return;
        var sx = bounds.Width / RefW;
        var sy = bounds.Height / RefH;
        var press = IsPressed ? 2.0 * sy : 0;

        if (Housing is not null)
        {
            // Blank asset already has factory silk removed — draw housing as-is (no gray cover).
            context.DrawImage(
                Housing,
                new Rect(0, 0, Housing.PixelSize.Width, Housing.PixelSize.Height),
                new Rect(0, press, bounds.Width, bounds.Height - press));
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x2A)), bounds, (float)(4 * sx));
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1C)),
                new Rect(8 * sx, 96 * sy + press, bounds.Width - 16 * sx, bounds.Height - 104 * sy - press), (float)(5 * sx));
        }

        DrawLed(context, sx, sy, press);
        if (IdentityBoxed)
            DrawNavFace(context, sx, sy, press);
        else
        {
            DrawLabels(context, sx, sy, press);
            DrawIdentity(context, sx, sy, press);
        }

        if (IsPressed)
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
                new Rect(8 * sx, 96 * sy, bounds.Width - 16 * sx, bounds.Height - 108 * sy));
    }

    private void DrawLed(DrawingContext context, double sx, double sy, double press)
    {
        // Measured LED center (38, 26), diameter ≈ 9px on 117×224
        var cx = 38 * sx;
        var cy = 26 * sy + press;
        var r = Math.Max(2.6, 4.5 * Math.Min(sx, sy));

        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x10, 0x0C, 0x0C)), null, new Point(cx, cy), r + 1.2 * sx, r + 1.2 * sy);

        var on = LedState is FootswitchLedState.On or FootswitchLedState.ActivePreset
            or FootswitchLedState.ActiveStomp or FootswitchLedState.Pressed
            || IsSelected;

        if (on)
        {
            var core = LedState == FootswitchLedState.ActiveStomp
                ? Color.FromRgb(0xE8, 0xA0, 0x3A)
                : Color.FromRgb(0xE0, 0x24, 0x28);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, core.R, core.G, core.B)), null, new Point(cx, cy), r * 1.55, r * 1.55);
            context.DrawEllipse(new SolidColorBrush(core), null, new Point(cx, cy), r * 0.82, r * 0.82);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(190, 255, 210, 210)), null, new Point(cx - r * 0.2, cy - r * 0.2), r * 0.22, r * 0.22);
        }
        else
        {
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x3A, 0x12, 0x14)), null, new Point(cx, cy), r * 0.85, r * 0.85);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x24, 0x0A, 0x0C)), null, new Point(cx, cy), r * 0.48, r * 0.48);
        }
    }

    private void DrawLabels(DrawingContext context, double sx, double sy, double press)
    {
        if (string.IsNullOrEmpty(PrimaryLabel) && string.IsNullOrEmpty(SecondaryLabel)) return;

        // Primary glyph ≈ 11 px on ref; secondary pill ≈ 7–8 px tall with padding
        var primarySize = Math.Max(6.5, 11.0 * sy);
        var secondarySize = Math.Max(5.5, 7.2 * sy);
        var type = new Typeface(LabelFont, FontStyle.Normal, FontWeight.Bold);
        var right = 107 * sx;
        var yTop = 10 * sy + press;

        if (!string.IsNullOrEmpty(PrimaryLabel))
        {
            var ft = Fmt(PrimaryLabel, type, primarySize, Brushes.White);
            context.DrawText(ft, new Point(right - ft.Width, yTop));
            yTop = 28 * sy + press;
        }

        if (!string.IsNullOrEmpty(SecondaryLabel))
        {
            if (SecondaryBoxed)
            {
                var ft = Fmt(SecondaryLabel, type, secondarySize, Brushes.Black);
                var padX = 2.4 * sx;
                var padY = 1.0 * sy;
                var boxH = Math.Max(ft.Height + padY * 2, 8.0 * sy);
                var box = new Rect(right - ft.Width - padX * 2, yTop, ft.Width + padX * 2, boxH);
                context.FillRectangle(Brushes.White, box, (float)(0.9 * sx));
                context.DrawText(ft, new Point(box.X + padX, box.Y + (boxH - ft.Height) / 2));
            }
            else
            {
                var ft = Fmt(SecondaryLabel, type, secondarySize, Brushes.White);
                context.DrawText(ft, new Point(right - ft.Width, yTop));
            }
        }
    }

    private void DrawIdentity(DrawingContext context, double sx, double sy, double press)
    {
        // Measured number height 36 px; sit slightly lower so labels clear (center y≈58)
        var size = Math.Max(13, 34 * sy);
        var type = new Typeface(IdentityFont, FontStyle.Normal, FontWeight.Black);
        var ft = Fmt(Identity, type, size, Brushes.White);

        var cx = 54 * sx;
        var cy = 58 * sy + press;
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    /// <summary>UP / DOWN face: optional primary above, boxed identity, secondary (ENTER/ESCAPE) below — all centered.</summary>
    private void DrawNavFace(DrawingContext context, double sx, double sy, double press)
    {
        var labelType = new Typeface(LabelFont, FontStyle.Normal, FontWeight.Bold);
        var idType = new Typeface(IdentityFont, FontStyle.Normal, FontWeight.Black);
        var cx = 58 * sx;

        var idSize = Math.Max(11, 20 * sy);
        var micro = Math.Max(5.5, 8.0 * sy);
        var idFt = Fmt(Identity, idType, idSize, Brushes.White);

        var idCy = 42 * sy + press;
        var idX = cx - idFt.Width / 2;
        var idY = idCy - idFt.Height / 2;
        var padX = 5 * sx;
        var padY = 2.2 * sy;
        var box = new Rect(idX - padX, idY - padY, idFt.Width + padX * 2, idFt.Height + padY * 2);
        context.DrawRectangle(null, new Pen(Brushes.White, Math.Max(1.0, 1.35 * sx)), box, (float)(1.2 * sx));
        context.DrawText(idFt, new Point(idX, idY));

        if (!string.IsNullOrEmpty(PrimaryLabel))
        {
            var ft = Fmt(PrimaryLabel, labelType, micro, Brushes.White);
            context.DrawText(ft, new Point(cx - ft.Width / 2, box.Y - ft.Height - 1.5 * sy));
        }

        if (!string.IsNullOrEmpty(SecondaryLabel))
        {
            var ft = Fmt(SecondaryLabel, labelType, micro, Brushes.White);
            context.DrawText(ft, new Point(cx - ft.Width / 2, box.Bottom + 2.0 * sy));
        }
    }

    private static FormattedText Fmt(string text, Typeface type, double size, IBrush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, type, size, brush);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        IsPressed = true;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsPressed) return;
        IsPressed = false;
        e.Pointer.Capture(null);
        if (Bounds.Contains(e.GetPosition(this)) && Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (IsPressed) { IsPressed = false; InvalidateVisual(); }
    }
}
