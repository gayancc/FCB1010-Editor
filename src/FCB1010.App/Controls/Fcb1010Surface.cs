using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;
using FCB1010.App.ViewModels;

namespace FCB1010.App.Controls;

/// <summary>
/// Assembled FCB1010 from real footswitch, expression, and display components.
/// Chassis topology calibrated to Assets/ref/fcb1010-full-ref.png (1024×366):
/// switches end ≈70.5%; Exp A starts ≈0.705; pitch ≈120 px / 6 columns; mid groove ≈ y 188–196.
/// </summary>
public sealed class Fcb1010Surface : UserControl
{
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<Fcb1010Surface, MainViewModel?>(nameof(ViewModel));

    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly FcbDisplayPanelControl _display = new();
    private readonly FcbFootswitchControl[] _pedals = new FcbFootswitchControl[10];
    private readonly FcbFootswitchControl _up = new();
    private readonly FcbFootswitchControl _down = new();
    private readonly FcbExpressionPedalControl _expA = new() { PedalId = "A" };
    private readonly FcbExpressionPedalControl _expB = new() { PedalId = "B" };
    private MainViewModel? _subscribed;

    public Fcb1010Surface()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xBA));

        for (var i = 0; i < 10; i++)
        {
            var n = i + 1;
            var sw = new FcbFootswitchControl();
            sw.ApplyFactoryFace(n);
            _pedals[i] = sw;
        }
        _up.ApplyUpFace();
        _down.ApplyDownFace();

        _expA.AddHandler(PointerPressedEvent, (_, _) => ViewModel?.SelectExpressionCommand.Execute(true), handledEventsToo: true);
        _expB.AddHandler(PointerPressedEvent, (_, _) => ViewModel?.SelectExpressionCommand.Execute(false), handledEventsToo: true);
        _expA.ValueCommitted += (_, v) => { if (ViewModel is not null) ViewModel.ExpressionAValue = v; };
        _expB.ValueCommitted += (_, v) => { if (ViewModel is not null) ViewModel.ExpressionBValue = v; };

        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = RowDefinitions.Parse("Auto,*"),
            Margin = new Thickness(6, 4, 6, 4),
        };

        // Top strip: brand | display | expression silk — column weights match chassis (22 / 48.5 / 29.5 of full width feel)
        var top = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("0.20*,0.505*,0.295*"),
            Margin = new Thickness(2, 0, 2, 4),
            MinHeight = 52,
        };
        top.Children.Add(BuildBrand());

        Grid.SetColumn(_display, 1);
        _display.HorizontalAlignment = HorizontalAlignment.Stretch;
        _display.VerticalAlignment = VerticalAlignment.Center;
        _display.Height = 56;
        top.Children.Add(_display);

        var expHeaders = BuildExpHeaderStrip();
        Grid.SetColumn(expHeaders, 2);
        top.Children.Add(expHeaders);

        root.Children.Add(top);

        // Deck: switches 70.5% / expression bay 29.5% (ExpA starts ~70.5% on photo)
        // Mid groove ≈ 8–10 px visual band between rows
        var deck = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("705*,295*"),
            RowDefinitions = RowDefinitions.Parse("*,10,*"),
        };
        Grid.SetRow(deck, 1);

        // Visible chassis groove between switch rows
        var groove = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x9A, 0x9A, 0x9C), 0),
                    new GradientStop(Color.FromRgb(0xE8, 0xE8, 0xEA), 0.45),
                    new GradientStop(Color.FromRgb(0x88, 0x88, 0x8A), 1),
                }
            },
            Margin = new Thickness(0, 0, 4, 0),
            CornerRadius = new CornerRadius(1),
        };
        Grid.SetRow(groove, 1);
        Grid.SetColumnSpan(groove, 1);
        deck.Children.Add(groove);

        var topRow = BuildSwitchRow(6, 7, 8, 9, 10, _up);
        var botRow = BuildSwitchRow(1, 2, 3, 4, 5, _down);
        Grid.SetRow(topRow, 0);
        Grid.SetRow(botRow, 2);
        deck.Children.Add(topRow);
        deck.Children.Add(botRow);

        var exps = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,8,*"),
            Margin = new Thickness(6, 0, 2, 0),
        };
        Grid.SetColumn(exps, 1);
        Grid.SetRowSpan(exps, 3);
        _expA.HorizontalAlignment = HorizontalAlignment.Center;
        _expB.HorizontalAlignment = HorizontalAlignment.Center;
        _expA.VerticalAlignment = VerticalAlignment.Stretch;
        _expB.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumn(_expB, 2);
        exps.Children.Add(_expA);
        exps.Children.Add(_expB);
        deck.Children.Add(exps);

        root.Children.Add(deck);

        return new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xD8, 0xD8, 0xDA), 0),
                    new GradientStop(Color.FromRgb(0xC4, 0xC4, 0xC6), 0.45),
                    new GradientStop(Color.FromRgb(0xA8, 0xA8, 0xAA), 1),
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0x6A, 0x6C)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 4, 6, 6),
            Child = root,
        };
    }

    private static Control BuildBrand()
    {
        // Hardware silk: italic FCB1010 with horizontal speed-line fill + MIDI FOOT CONTROLLER underline.
        // Use the cropped factory artwork — system fonts cannot reproduce the striated logo.
        Bitmap? logo = null;
        try
        {
            logo = new Bitmap(AssetLoader.Open(new Uri("avares://FCB1010Studio/Assets/ref/fcb1010-brand.png")));
        }
        catch { /* fall through to text */ }

        if (logo is not null)
        {
            return new Image
            {
                Source = logo,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = 52,
                Margin = new Thickness(4, 4, 8, 2),
            };
        }

        // Fallback if asset missing
        var brand = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 4, 0),
        };
        brand.Children.Add(new TextBlock
        {
            Text = "FCB1010",
            FontSize = 28,
            FontWeight = FontWeight.Black,
            FontStyle = FontStyle.Italic,
            FontFamily = new FontFamily("Arial Black, Impact, Segoe UI Black, Arial"),
            LetterSpacing = -1.2,
            Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
        });
        brand.Children.Add(new TextBlock
        {
            Text = "MIDI FOOT CONTROLLER",
            FontSize = 7,
            FontWeight = FontWeight.Bold,
            FontStyle = FontStyle.Italic,
            FontFamily = new FontFamily("Arial Narrow, Segoe UI, Arial"),
            LetterSpacing = 1.1,
            Margin = new Thickness(1, -3, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
        });
        brand.Children.Add(new Border
        {
            Height = 2,
            Margin = new Thickness(1, 1, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1C)),
        });
        return brand;
    }

    private static Control BuildExpHeaderStrip()
    {
        var g = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("*,*"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(4, 0, 2, 0),
        };

        // Pedal A: title + PARAMETER | LED | VALUE pills
        var a = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
        a.Children.Add(MakeExpTitle("EXPRESSION PEDAL A"));
        var aRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4,
        };
        aRow.Children.Add(MakePill("PARAMETER"));
        aRow.Children.Add(MakeLedDot());
        aRow.Children.Add(MakePill("VALUE"));
        a.Children.Add(aRow);
        g.Children.Add(a);

        // Pedal B: title + single LED
        var b = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
        b.Children.Add(MakeExpTitle("EXPRESSION PEDAL B"));
        b.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = MakeLedDot(),
            Margin = new Thickness(0, 2, 0, 0),
        });
        Grid.SetColumn(b, 1);
        g.Children.Add(b);

        return g;
    }

    private static TextBlock MakeExpTitle(string text) => new()
    {
        Text = text,
        FontSize = 6.5,
        FontWeight = FontWeight.SemiBold,
        FontFamily = new FontFamily("Arial Narrow, Segoe UI, Arial"),
        LetterSpacing = 0.6,
        HorizontalAlignment = HorizontalAlignment.Center,
        Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
    };

    private static Border MakePill(string text) => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(5, 1, 5, 1),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 7,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Arial Narrow, Segoe UI, Arial"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        },
    };

    private static Control MakeLedDot() => new Ellipse
    {
        Width = 7,
        Height = 4,
        Fill = new SolidColorBrush(Color.FromRgb(0xC0, 0x18, 0x1C)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private Grid BuildSwitchRow(int a, int b, int c, int d, int e, FcbFootswitchControl nav)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*,*,*") };
        void Add(FcbFootswitchControl sw, int col)
        {
            // Module gap ≈ half paddle width on hardware; keep aspect via Center
            sw.Margin = new Thickness(2, 0, 2, 0);
            sw.HorizontalAlignment = HorizontalAlignment.Center;
            sw.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetColumn(sw, col);
            g.Children.Add(sw);
        }
        Add(_pedals[a - 1], 0);
        Add(_pedals[b - 1], 1);
        Add(_pedals[c - 1], 2);
        Add(_pedals[d - 1], 3);
        Add(_pedals[e - 1], 4);
        Add(nav, 5);
        return g;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ViewModel is not null)
            SyncFromVm();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ViewModelProperty) return;

        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnVmChanged;
            foreach (var fs in _subscribed.Footswitches)
                fs.PropertyChanged -= OnFsChanged;
        }

        _subscribed = ViewModel;
        WireCommands();
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged += OnVmChanged;
            foreach (var fs in _subscribed.Footswitches)
                fs.PropertyChanged += OnFsChanged;
            SyncFromVm();
        }
    }

    private void WireCommands()
    {
        var vm = ViewModel;
        if (vm is null) return;
        for (var i = 0; i < 10; i++)
        {
            var n = i + 1;
            _pedals[i].Command = vm.SelectFootswitchCommand;
            _pedals[i].CommandParameter = n;
        }
        _up.Command = vm.BankUpCommand;
        _down.Command = vm.BankDownCommand;
        _display.ToggleRelayCommand = vm.ToggleRelayCommand;
    }

    private void OnVmChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SyncFromVm();
    private void OnFsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SyncFromVm();

    private void SyncFromVm()
    {
        var vm = ViewModel;
        if (vm is null) return;

        _display.DisplayText = $"{vm.Bank % 10}{(vm.Footswitch == 10 ? 0 : vm.Footswitch) % 10}";

        _display.Switch1Led = vm.Switch1Closed;
        _display.Switch2Led = vm.Switch2Closed;
        _display.ToggleRelayCommand = vm.ToggleRelayCommand;

        _display.SelectLed = vm.SurfaceFocus == SurfaceFocus.Pedal;
        _display.NumberLed = vm.SurfaceFocus == SurfaceFocus.Pedal;
        _display.Value1Led = vm.SurfaceFocus == SurfaceFocus.ExpressionA;
        _display.Value2Led = vm.SurfaceFocus == SurfaceFocus.ExpressionB;

        _display.DirectSelectLed = vm.DirectSelect;
        _display.MidiFunctionLed = vm.MidiMerge;
        _display.MidiChannelLed = vm.IsRoutingWorkspace;
        _display.ConfigLed = vm.HasChanges;
        _display.InvalidateVisual();

        for (var i = 0; i < 10; i++)
        {
            var fs = vm.Footswitches[i];
            var sw = _pedals[i];
            sw.IsSelected = fs.Selected && vm.SurfaceFocus == SurfaceFocus.Pedal;
            sw.IsPressed = fs.Pressed;
            sw.LedState = fs.Role == "STOMP" && fs.LedOn
                ? FootswitchLedState.ActiveStomp
                : fs.Selected && vm.SurfaceFocus == SurfaceFocus.Pedal
                    ? FootswitchLedState.ActivePreset
                    : fs.LedOn ? FootswitchLedState.On : FootswitchLedState.Off;
        }

        _expA.Value = vm.ExpressionAValue;
        _expB.Value = vm.ExpressionBValue;
        _expA.Minimum = vm.ExpressionAMin;
        _expA.Maximum = vm.ExpressionAMax;
        _expB.Minimum = vm.ExpressionBMin;
        _expB.Maximum = vm.ExpressionBMax;
        _expA.IsEnabledPedal = vm.ExpressionAEnabled;
        _expB.IsEnabledPedal = vm.ExpressionBEnabled;
        _expA.IsSelected = vm.SurfaceFocus == SurfaceFocus.ExpressionA;
        _expB.IsSelected = vm.SurfaceFocus == SurfaceFocus.ExpressionB;
    }
}
