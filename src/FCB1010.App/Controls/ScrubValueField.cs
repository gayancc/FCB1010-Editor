using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace FCB1010.App.Controls;

/// <summary>
/// Data-visible MIDI value label: drag vertically to scrub, double-click to type.
/// Looks like a readout — edit chrome only appears while typing.
/// </summary>
public sealed class ScrubValueField : Border
{
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<ScrubValueField, int>(
            nameof(Value), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<ScrubValueField, int>(nameof(Minimum), 0);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<ScrubValueField, int>(nameof(Maximum), 127);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<ScrubValueField, string>(nameof(FormatString), "000");

    public static readonly StyledProperty<double> DigitsFontSizeProperty =
        AvaloniaProperty.Register<ScrubValueField, double>(nameof(DigitsFontSize), 13);

    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, Clamp(value));
    }

    public int Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public double DigitsFontSize
    {
        get => GetValue(DigitsFontSizeProperty);
        set => SetValue(DigitsFontSizeProperty, value);
    }

    private readonly Grid _root;
    private readonly TextBlock _display;
    private readonly TextBox _editor;
    private bool _editing;
    private bool _scrubbing;
    private bool _pressed;
    private bool _suppressLostFocus;
    private Point _pressOrigin;
    private double _lastY;
    private double _accum;
    private const double ScrubThreshold = 2;
    private const double PixelsPerStep = 3;

    static ScrubValueField()
    {
        FocusableProperty.OverrideDefaultValue<ScrubValueField>(true);
        ValueProperty.Changed.AddClassHandler<ScrubValueField>((c, e) =>
        {
            if (e.NewValue is int v)
            {
                var clamped = Math.Clamp(v, c.Minimum, c.Maximum);
                if (clamped != v)
                {
                    c.SetValue(ValueProperty, clamped);
                    return;
                }
            }
            c.RefreshDisplay();
        });
        FormatStringProperty.Changed.AddClassHandler<ScrubValueField>((c, _) => c.RefreshDisplay());
        DigitsFontSizeProperty.Changed.AddClassHandler<ScrubValueField>((c, e) =>
        {
            var size = e.NewValue is double d ? d : 13;
            c._display.FontSize = size;
            c._editor.FontSize = size;
        });
        MinimumProperty.Changed.AddClassHandler<ScrubValueField>((c, _) => c.OnRangeChanged());
        MaximumProperty.Changed.AddClassHandler<ScrubValueField>((c, _) => c.OnRangeChanged());
        IsEnabledProperty.Changed.AddClassHandler<ScrubValueField>((c, _) => c.UpdateChrome());
    }

    public ScrubValueField()
    {
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // nearly invisible, still hit-testable
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0, 0, 0, 1);
        CornerRadius = new CornerRadius(2);
        MinWidth = 36;
        MinHeight = 22;
        Height = 26;
        Padding = new Thickness(4, 0);
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        ClipToBounds = true;
        Focusable = true;
        ToolTip.SetTip(this, "Drag up/down to change · double-click to type");

        _display = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xED)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false,
        };

        _editor = new TextBox
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            CaretBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6)),
            IsVisible = false,
            IsHitTestVisible = false,
            Focusable = true,
        };
        _editor.KeyDown += OnEditorKeyDown;
        _editor.LostFocus += OnEditorLostFocus;

        // Opaque-to-hit-test surface that owns all pointer interaction.
        _root = new Grid { Background = Brushes.Transparent };
        _root.Children.Add(_display);
        _root.Children.Add(_editor);
        Child = _root;

        _root.PointerPressed += OnRootPointerPressed;
        _root.PointerMoved += OnRootPointerMoved;
        _root.PointerReleased += OnRootPointerReleased;
        _root.PointerCaptureLost += OnRootPointerCaptureLost;
        _root.PointerWheelChanged += OnRootPointerWheel;
        _root.DoubleTapped += OnRootDoubleTapped;

        RefreshDisplay();
        UpdateChrome();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScrubValueFieldAutomationPeer(this);

    private sealed class ScrubValueFieldAutomationPeer(ScrubValueField owner)
        : ControlAutomationPeer(owner), IRangeValueProvider
    {
        private ScrubValueField Field => (ScrubValueField)Owner;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;
        protected override string GetClassNameCore() => nameof(ScrubValueField);
        public bool IsReadOnly => !Field.IsEnabled;
        public double Minimum => Field.Minimum;
        public double Maximum => Field.Maximum;
        public double Value => Field.Value;
        public double LargeChange => 10;
        public double SmallChange => 1;
        public void SetValue(double value)
        {
            if (!IsReadOnly) Field.Value = (int)Math.Round(value);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsEnabled && !_editing)
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_scrubbing && !_editing)
            BorderBrush = Brushes.Transparent;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || _editing) return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.F2:
                BeginEdit();
                e.Handled = true;
                break;
            case Key.Up:
                Nudge(e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 5 : 1);
                e.Handled = true;
                break;
            case Key.Down:
                Nudge(e.KeyModifiers.HasFlag(KeyModifiers.Control) ? -5 : -1);
                e.Handled = true;
                break;
            case Key.PageUp:
                Nudge(10);
                e.Handled = true;
                break;
            case Key.PageDown:
                Nudge(-10);
                e.Handled = true;
                break;
        }
    }

    private void OnRootDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled || _editing) return;
        BeginEdit();
        e.Handled = true;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _editing) return;
        if (!e.GetCurrentPoint(_root).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount >= 2)
        {
            _pressed = false;
            _scrubbing = false;
            BeginEdit();
            e.Handled = true;
            return;
        }

        _pressed = true;
        _scrubbing = false;
        _accum = 0;
        _pressOrigin = e.GetPosition(_root);
        _lastY = _pressOrigin.Y;
        Focus();
        e.Handled = true;
    }

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed || _editing || !IsEnabled) return;

        var pos = e.GetPosition(_root);
        if (!_scrubbing)
        {
            if (Math.Abs(pos.Y - _pressOrigin.Y) < ScrubThreshold &&
                Math.Abs(pos.X - _pressOrigin.X) < ScrubThreshold)
                return;

            _scrubbing = true;
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x1C));
            _lastY = pos.Y;
            e.Pointer.Capture(_root);
        }

        var dy = _lastY - pos.Y; // drag up → increase
        _lastY = pos.Y;
        _accum += dy;

        var stepPx = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1.5 : PixelsPerStep;
        var coarse = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 5 : 1;

        while (_accum >= stepPx)
        {
            _accum -= stepPx;
            Nudge(coarse);
        }
        while (_accum <= -stepPx)
        {
            _accum += stepPx;
            Nudge(-coarse);
        }

        e.Handled = true;
    }

    private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed && !_scrubbing) return;
        _pressed = false;
        if (_scrubbing)
        {
            _scrubbing = false;
            BorderBrush = IsPointerOver
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6))
                : Brushes.Transparent;
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void OnRootPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        _scrubbing = false;
        if (!_editing)
            BorderBrush = Brushes.Transparent;
    }

    private void OnRootPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabled || _editing) return;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 5 : 1;
        if (e.Delta.Y > 0) Nudge(step);
        else if (e.Delta.Y < 0) Nudge(-step);
        e.Handled = true;
    }

    private void Nudge(int delta)
    {
        var next = Clamp(Value + delta);
        if (next == Value) return;
        // SetValue (not SetCurrentValue) so TwoWay bindings push to the view-model.
        SetValue(ValueProperty, next);
    }

    private void BeginEdit()
    {
        if (_editing) return;
        _pressed = false;
        _scrubbing = false;

        _editing = true;
        _suppressLostFocus = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6));
        BorderThickness = new Thickness(0, 0, 0, 2);

        _display.IsVisible = false;
        _editor.IsVisible = true;
        _editor.IsHitTestVisible = true;
        _editor.Text = Value.ToString(CultureInfo.InvariantCulture);

        Dispatcher.UIThread.Post(() =>
        {
            if (!_editing) return;
            _editor.Focus();
            _editor.SelectAll();
            _suppressLostFocus = false;
        }, DispatcherPriority.Input);
    }

    private void EndEdit(bool commit)
    {
        if (!_editing) return;
        _editing = false;
        _suppressLostFocus = true;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush = IsPointerOver
            ? new SolidColorBrush(Color.FromRgb(0x2E, 0xC4, 0xB6))
            : Brushes.Transparent;

        _editor.IsVisible = false;
        _editor.IsHitTestVisible = false;
        _display.IsVisible = true;

        if (commit && int.TryParse(_editor.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            SetValue(ValueProperty, Clamp(parsed));

        RefreshDisplay();
        _suppressLostFocus = false;
        Focus();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EndEdit(commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndEdit(commit: false);
            e.Handled = true;
        }
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_suppressLostFocus) return;
        EndEdit(commit: true);
    }

    private void OnRangeChanged()
    {
        var clamped = Clamp(Value);
        if (clamped != Value) SetValue(ValueProperty, clamped);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_editing) return;
        var fmt = string.IsNullOrWhiteSpace(FormatString) ? "0" : FormatString;
        try { _display.Text = Value.ToString(fmt, CultureInfo.InvariantCulture); }
        catch (FormatException) { _display.Text = Value.ToString(CultureInfo.InvariantCulture); }
    }

    private void UpdateChrome()
    {
        Opacity = IsEnabled ? 1.0 : 0.38;
        Cursor = IsEnabled && !_editing
            ? new Cursor(StandardCursorType.SizeNorthSouth)
            : new Cursor(StandardCursorType.Arrow);
        _root.IsHitTestVisible = IsEnabled;
    }

    private int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);
}
