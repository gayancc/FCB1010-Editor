using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCB1010.App.Services;
using FCB1010.Core;
using FCB1010.Midi;

namespace FCB1010.App.ViewModels;

public enum WorkspaceKind { Twin, Map, Routing, Diagnostics }
public enum EditorMode { Edit, Live }
public enum SurfaceFocus { Pedal, ExpressionA, ExpressionB }

public sealed partial class FootswitchItemViewModel : ObservableObject
{
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _midiLine = "";
    [ObservableProperty] private string _expressionLine = "";
    [ObservableProperty] private string _role = "PRESET";
    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _ledOn;
    [ObservableProperty] private bool _modified;
    [ObservableProperty] private bool _pressed;
}

public sealed partial class ChannelRouteViewModel : ObservableObject
{
    public required string Label { get; init; }
    public required string Kind { get; init; }
    public required int Index { get; init; }
    public Action<ChannelRouteViewModel>? Commit { get; set; }
    [ObservableProperty] private int _channel;

    public string KindTitle => Kind switch
    {
        "pc" => "PROGRAM",
        "cc" => "CONTROL",
        "exp" => "EXPRESS",
        "note" => "NOTE",
        _ => Kind.ToUpperInvariant(),
    };

    public string AccentHex => Kind switch
    {
        "pc" => "#2EC4B6",
        "cc" => "#FF9F1C",
        "exp" => "#6A9EAA",
        "note" => "#E8D5A3",
        _ => "#8B949C",
    };

    partial void OnChannelChanged(int value) => Commit?.Invoke(this);
}

public sealed partial class MidiActionItemViewModel : ObservableObject
{
    public required string Kind { get; init; }
    public required string SlotLabel { get; init; }
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private int _channel;
    [ObservableProperty] private int _primary;
    [ObservableProperty] private int _secondary;
    [ObservableProperty] private bool _hasSecondary;
    public Action<MidiActionItemViewModel>? Commit { get; set; }

    /// <summary>Compact channel badge e.g. CH01.</summary>
    public string ChannelLabel => $"CH{Channel:00}";

    partial void OnEnabledChanged(bool value) => Commit?.Invoke(this);
    partial void OnChannelChanged(int value) => OnPropertyChanged(nameof(ChannelLabel));
    partial void OnPrimaryChanged(int value) => Commit?.Invoke(this);
    partial void OnSecondaryChanged(int value) => Commit?.Invoke(this);
}

public sealed partial class HexRowViewModel : ObservableObject
{
    public required string Offset { get; init; }
    public required string Hex { get; init; }
    public required string Meaning { get; init; }
    public required string Changed { get; init; }
    [ObservableProperty] private bool _isChanged;
}

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IAppDialogs _dialogs;
    private readonly DryWetMidiTransport _transport = new();
    private readonly FcbDeviceService _device;
    private readonly EditorMetadataStore _metadata;
    private FcbConfiguration _config = CreateDemo();
    private FcbConfiguration? _deviceSnapshot;
    private FcbConfiguration _savedSnapshot = CreateDemo();
    private readonly Stack<FcbConfiguration> _undo = new();
    private readonly Stack<FcbConfiguration> _redo = new();
    private bool _loading;
    private string? _currentPath;
    private FirmwareProbeResult? _firmwareProbe;

    public ObservableCollection<MidiPortInfo> InputPorts { get; } = [];
    public ObservableCollection<MidiPortInfo> OutputPorts { get; } = [];
    public ObservableCollection<FootswitchItemViewModel> Footswitches { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> MidiActions { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ProgramActions { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ProgramActionsActive { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ProgramActionsIdle { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ControlActions { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ControlActionsActive { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> ControlActionsIdle { get; } = [];
    public ObservableCollection<MidiActionItemViewModel> NoteActions { get; } = [];
    public ObservableCollection<ChannelRouteViewModel> ChannelRoutes { get; } = [];
    public ObservableCollection<ChannelRouteViewModel> ProgramRoutes { get; } = [];
    public ObservableCollection<ChannelRouteViewModel> AuxRoutes { get; } = [];
    public ObservableCollection<MidiDiagnostic> Diagnostics { get; } = [];
    public ObservableCollection<ConfigChange> PendingChanges { get; } = [];
    public ObservableCollection<HexRowViewModel> HexRows { get; } = [];
    public ObservableCollection<MapCellViewModel> MapCells { get; } = [];
    public ObservableCollection<MapBankRowViewModel> MapBanks { get; } = [];
    public ObservableCollection<int> Banks { get; } = new(Enumerable.Range(0, 10));

    [ObservableProperty] private MidiPortInfo? _selectedInput;
    [ObservableProperty] private MidiPortInfo? _selectedOutput;
    [ObservableProperty] private bool _connected;
    [ObservableProperty] private string _connectionLabel = "OFFLINE";
    [ObservableProperty] private string _firmwareLabel = "Unknown";
    [ObservableProperty] private FirmwareFamily _selectedFirmware = FirmwareFamily.Unknown;
    public FirmwareFamily[] FirmwareChoices { get; } = [FirmwareFamily.Unknown, FirmwareFamily.Stock, FirmwareFamily.UnO];
    [ObservableProperty] private string _inputPortLabel = "—";
    [ObservableProperty] private string _outputPortLabel = "—";
    [ObservableProperty] private bool _midiRx;
    [ObservableProperty] private bool _midiTx;

    /// <summary>Primary link control caption — LINK when offline, UNLINK when online.</summary>
    public string LinkButtonLabel => Connected ? "UNLINK" : "LINK";
    public bool CanChangePorts => !Connected;
    public bool CanDeviceIo => Connected;
    public bool CanLink => Connected || (SelectedInput is not null && SelectedOutput is not null);
    [ObservableProperty] private int _bank;
    [ObservableProperty] private string _bankName = "";
    [ObservableProperty] private int _footswitch = 1;
    [ObservableProperty] private string _presetName = "";
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private string _deviceSync = "NO SNAPSHOT";
    [ObservableProperty] private string _editorSync = "CLEAN";
    [ObservableProperty] private string _projectSync = "LOCAL";
    [ObservableProperty] private WorkspaceKind _workspace = WorkspaceKind.Twin;
    [ObservableProperty] private EditorMode _mode = EditorMode.Edit;
    [ObservableProperty] private bool _isTwinWorkspace = true;
    [ObservableProperty] private bool _isMapWorkspace;
    [ObservableProperty] private bool _isRoutingWorkspace;
    [ObservableProperty] private bool _isDiagnosticsWorkspace;
    [ObservableProperty] private bool _isLiveMode;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private int _expressionAValue = 82;
    [ObservableProperty] private int _expressionBValue = 110;
    [ObservableProperty] private int _expressionAController = 27;
    [ObservableProperty] private int _expressionBController = 7;
    [ObservableProperty] private int _expressionAMin;
    [ObservableProperty] private int _expressionAMax = 127;
    [ObservableProperty] private int _expressionBMin;
    [ObservableProperty] private int _expressionBMax = 127;
    [ObservableProperty] private bool _expressionAEnabled = true;
    [ObservableProperty] private bool _expressionBEnabled = true;
    [ObservableProperty] private bool _switch1Closed;
    [ObservableProperty] private bool _switch2Closed;
    [ObservableProperty] private string _roleLabel = "PRESET";
    [ObservableProperty] private string _actionSummary = "";
    [ObservableProperty] private string _liveTxText = "";
    [ObservableProperty] private string _flowText = "";
    [ObservableProperty] private bool _directSelect;
    [ObservableProperty] private bool _midiMerge = true;
    [ObservableProperty] private bool _runningStatus = true;
    [ObservableProperty] private string _rawHex = "";
    [ObservableProperty] private bool _flowPulse;
    [ObservableProperty] private bool _showHitRegions;
    [ObservableProperty] private SurfaceFocus _surfaceFocus = SurfaceFocus.Pedal;
    [ObservableProperty] private bool _canPastePreset;

    private FcbPreset? _clipboardPreset;
    private string? _clipboardName;

    public MainViewModel() : this(new NullDialogs()) { }

    public MainViewModel(IAppDialogs dialogs)
    {
        _dialogs = dialogs;
        _device = new FcbDeviceService(_transport, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FCB1010 Studio", "Backups"));
        _metadata = new EditorMetadataStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FCB1010 Studio", "Names"));
        _transport.StateChanged += (_, state) => Dispatcher.UIThread.Post(() => ApplyTransportState(state));
        _transport.Diagnostic += (_, item) => Dispatcher.UIThread.Post(() =>
        {
            Diagnostics.Insert(0, item);
            while (Diagnostics.Count > 500) Diagnostics.RemoveAt(Diagnostics.Count - 1);
            if (item.Direction is "IN" or "RX") PulseMidi(rx: true);
            if (item.Direction is "OUT" or "TX") PulseMidi(rx: false);
        });
        for (var i = 1; i <= 10; i++) Footswitches.Add(new FootswitchItemViewModel { Number = i });
        var labels = new[] { "PC1", "PC2", "PC3", "PC4", "PC5", "CC1", "CC2", "EXP A", "EXP B", "NOTE" };
        var kinds = new[] { "pc", "pc", "pc", "pc", "pc", "cc", "cc", "exp", "exp", "note" };
        for (var i = 0; i < 10; i++)
        {
            var route = new ChannelRouteViewModel { Label = labels[i], Kind = kinds[i], Index = i, Channel = 1 };
            route.Commit = CommitChannel;
            ChannelRoutes.Add(route);
            if (i < 5) ProgramRoutes.Add(route);
            else AuxRoutes.Add(route);
        }
        for (var i = 0; i < 100; i++) MapCells.Add(new MapCellViewModel { Index = i });
        for (var b = 0; b < 10; b++)
        {
            var row = new MapBankRowViewModel { Bank = b };
            for (var s = 0; s < 5; s++) row.Lower.Add(MapCells[b * 10 + s]);
            for (var s = 5; s < 10; s++) row.Upper.Add(MapCells[b * 10 + s]);
            MapBanks.Add(row);
        }
        RefreshPorts();
        RefreshAll();
    }

    public FcbConfiguration Config => _config;
    public int SelectedIndex => Bank * 10 + Footswitch - 1;
    public FcbPreset SelectedPreset => _config.GetPreset(Bank, Footswitch);

    [RelayCommand] private void RefreshPorts()
    {
        try
        {
            InputPorts.Clear(); OutputPorts.Clear();
            foreach (var p in _transport.GetInputPorts()) InputPorts.Add(p);
            foreach (var p in _transport.GetOutputPorts()) OutputPorts.Add(p);
            SelectedInput ??= InputPorts.FirstOrDefault();
            // Prefer the matching hardware endpoint; Windows often lists the GS synth first.
            SelectedOutput ??= OutputPorts.FirstOrDefault(p =>
                p.Name.Contains("FCB1010", StringComparison.OrdinalIgnoreCase))
                ?? OutputPorts.FirstOrDefault(p =>
                    !p.Name.Contains("Microsoft GS Wavetable", StringComparison.OrdinalIgnoreCase))
                ?? OutputPorts.FirstOrDefault();
            StatusMessage = $"Found {InputPorts.Count} MIDI input(s) and {OutputPorts.Count} MIDI output(s).";
        }
        catch (Exception ex) { StatusMessage = "MIDI discovery failed: " + ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanLink))]
    private async Task ToggleLinkAsync()
    {
        if (Connected) await DisconnectAsync();
        else await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        try
        {
            if (SelectedInput is null || SelectedOutput is null)
                throw new InvalidOperationException("Choose both MIDI IN and OUT ports.");
            _device.ResetSession();
            await _transport.ConnectAsync(SelectedInput.Id, SelectedOutput.Id);
            InputPortLabel = ShortPort(SelectedInput.Name);
            OutputPortLabel = ShortPort(SelectedOutput.Name);
            _firmwareProbe = await _device.ProbeFirmwareAsync(TimeSpan.FromSeconds(3));
            if (_firmwareProbe.Completed)
            {
                SelectedFirmware = _firmwareProbe.Family;
                _config.Firmware = _firmwareProbe.Family;
                _config.FirmwareVersion = _firmwareProbe.Signature;
                FirmwareLabel = $"{_config.Firmware} {_config.FirmwareVersion}";
            }
            StatusMessage = _firmwareProbe.Message;
            RefreshAll();
        }
        catch (Exception ex) { await _dialogs.AlertAsync("Connection failed", ex.Message); }
    }

    private async Task DisconnectAsync()
    {
        await _transport.DisconnectAsync();
        _device.ResetSession();
        if (Mode == EditorMode.Live)
        {
            Mode = EditorMode.Edit;
            IsLiveMode = false;
            RefreshFootswitches();
        }
        StatusMessage = "MIDI link closed.";
    }

    [RelayCommand(CanExecute = nameof(CanDeviceIo))]
    private async Task ReadDeviceAsync()
    {
        StatusMessage = "Requesting dump…";
        PulseMidi(rx: true);
        var family = SelectedFirmware != FirmwareFamily.Unknown
            ? SelectedFirmware
            : _firmwareProbe?.Family ?? FirmwareFamily.Unknown;
        var result = await _device.ReceiveDumpAsync(TimeSpan.FromSeconds(30), family);
        StatusMessage = result.Message;
        if (!result.Completed || result.Data is null) { await _dialogs.AlertAsync("Read", result.Message); return; }
        Snapshot();
        _config = FcbSysExCodec.Parse(result.Data, family);
        _config.FirmwareVersion = _firmwareProbe?.Signature;
        try
        {
            if (await _metadata.LoadAsync(result.Data, _config))
                StatusMessage = result.Message + " Local preset and bank names restored.";
        }
        catch (Exception ex)
        {
            StatusMessage = result.Message + " Local names could not be loaded: " + ex.Message;
        }
        SelectedFirmware = family == FirmwareFamily.Unknown ? SelectedFirmware : family;
        _deviceSnapshot = Clone(_config);
        _savedSnapshot = Clone(_config);
        _currentPath = null;
        _undo.Clear(); _redo.Clear();
        RefreshAll();
    }

    [RelayCommand(CanExecute = nameof(CanDeviceIo))]
    private async Task WriteDeviceAsync()
    {
        var errors = ConfigurationValidator.Validate(_config).Where(i => i.IsError).ToArray();
        if (errors.Length > 0)
        {
            await _dialogs.AlertAsync("Validation failed", string.Join("\n", errors.Take(15).Select(i => $"{i.Path}: {i.Message}")));
            return;
        }
        RefreshChanges();
        var labelsOnly = _deviceSnapshot is not null && PendingChanges.Count > 0 &&
            FcbSysExCodec.Serialize(_config).AsSpan().SequenceEqual(FcbSysExCodec.Serialize(_deviceSnapshot));
        var summary = labelsOnly
            ? $"{PendingChanges.Count} editor-only name/note change(s). The FCB1010 cannot store text labels. Save them locally for this exact device dump? No SysEx write will be sent."
            : PendingChanges.Count == 0
            ? "Editor matches baseline. Write the full dump anyway?"
            : $"Write MIDI settings to the FCB1010? {PendingChanges.Count} editor change(s):\n\n" + string.Join("\n", PendingChanges.Take(8).Select(c => $"{c.Scope}: {c.Field} {c.From} → {c.To}"));
        var footer = labelsOnly
            ? "For portable names, also save a .fcbproject file."
            : "A timestamped backup is created first. Read-back must match byte-for-byte. Names/notes remain local, not on the pedal.";
        if (!await _dialogs.ConfirmAsync(labelsOnly ? "SAVE LOCAL NAMES" : "WRITE TO FCB1010", summary + "\n\n" + footer)) return;
        if (labelsOnly)
        {
            try
            {
                var current = SelectedFirmware == FirmwareFamily.Stock
                    ? _device.LastReceived
                    : (await _device.ReceiveDumpAsync(TimeSpan.FromSeconds(12), SelectedFirmware)).Data;
                if (current is null || !current.AsSpan().SequenceEqual(FcbSysExCodec.Serialize(_config)))
                {
                    await _dialogs.AlertAsync("Names not saved", "The current device dump could not be confirmed to match the editor. Read the pedal again before associating names with it.");
                    return;
                }
                await _metadata.SaveAsync(current, _config);
                _deviceSnapshot = Clone(_config);
                StatusMessage = "Names saved locally for the verified pedal dump; no MIDI settings were sent.";
                await _dialogs.AlertAsync("Local names saved", StatusMessage + " Save a .fcbproject for a portable copy.");
                RefreshSync();
            }
            catch (Exception ex) { await _dialogs.AlertAsync("Names not saved", ex.Message); }
            return;
        }
        PulseMidi(rx: false);
        var result = await _device.WriteDumpAsync(_config);
        StatusMessage = result.Message;
        var report = result.Message;
        if (result.Completed)
        {
            _deviceSnapshot = Clone(_config);
            try
            {
                await _metadata.SaveAsync(result.Data!, _config);
                report += " Preset and bank names were saved locally; the FCB1010 cannot store text names.";
            }
            catch (Exception ex)
            {
                report += " MIDI write verified, but local names could not be saved: " + ex.Message;
            }
        }
        StatusMessage = report;
        await _dialogs.AlertAsync(result.Completed ? "Transmission complete" : "Write failed", report);
        RefreshSync();
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _dialogs.OpenFileAsync("Open", FileTypes.FcbFiles, FileTypes.SysEx, FileTypes.Project);
        if (path is null) return;
        try
        {
            Snapshot();
            _config = path.EndsWith(".syx", StringComparison.OrdinalIgnoreCase)
                ? await ProjectPersistence.LoadSysExAsync(path)
                : await ProjectPersistence.LoadProjectAsync(path);
            _currentPath = path;
            _savedSnapshot = Clone(_config);
            StatusMessage = "Opened " + path;
            RefreshAll();
        }
        catch (Exception ex) { await _dialogs.AlertAsync("Open failed", ex.Message); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_currentPath is null) { await SaveAsAsync(); return; }
        await SaveToAsync(_currentPath);
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var path = await _dialogs.SaveFileAsync("Save As", "project.fcbproject", FileTypes.Project, FileTypes.SysEx);
        if (path is null) return;
        await SaveToAsync(path);
    }

    private async Task SaveToAsync(string path)
    {
        try
        {
            if (path.EndsWith(".syx", StringComparison.OrdinalIgnoreCase)) await ProjectPersistence.SaveSysExAsync(path, _config);
            else await ProjectPersistence.SaveProjectAsync(path, _config);
            _currentPath = path;
            _savedSnapshot = Clone(_config);
            StatusMessage = "Saved " + path;
            RefreshSync();
        }
        catch (Exception ex) { await _dialogs.AlertAsync("Save failed", ex.Message); }
    }

    [RelayCommand] private void Undo() { if (_undo.Count == 0) return; _redo.Push(Clone(_config)); _config = _undo.Pop(); RefreshAll(); }
    [RelayCommand] private void Redo() { if (_redo.Count == 0) return; _undo.Push(Clone(_config)); _config = _redo.Pop(); RefreshAll(); }
    [RelayCommand(CanExecute = nameof(CanDeviceIo))]
    private void ToggleLive()
    {
        Mode = Mode == EditorMode.Live ? EditorMode.Edit : EditorMode.Live;
        IsLiveMode = Mode == EditorMode.Live;
        RefreshFootswitches();
        StatusMessage = IsLiveMode ? "LIVE — pedals send MIDI preview." : "EDIT — local editor only.";
    }

    [RelayCommand]
    private void SelectWorkspace(WorkspaceKind kind)
    {
        Workspace = kind;
        IsTwinWorkspace = kind == WorkspaceKind.Twin;
        IsMapWorkspace = kind == WorkspaceKind.Map;
        IsRoutingWorkspace = kind == WorkspaceKind.Routing;
        IsDiagnosticsWorkspace = kind == WorkspaceKind.Diagnostics;
    }

    [RelayCommand]
    private void SelectFootswitch(int number)
    {
        SurfaceFocus = SurfaceFocus.Pedal;
        Footswitch = number;
        foreach (var fs in Footswitches)
        {
            fs.Pressed = fs.Number == number;
            fs.Selected = fs.Number == number;
            fs.LedOn = Mode == EditorMode.Live && fs.Selected || fs.Role == "STOMP" && fs.LedOn;
        }
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(90);
            foreach (var fs in Footswitches) fs.Pressed = false;
        });
        if (Mode == EditorMode.Live)
        {
            _ = SendLivePreviewAsync();
            PulseMidi(rx: false);
            FlowPulse = true;
            LiveTxText = BuildLiveTx();
            StatusMessage = $"TEST TX · {PresetName} (not writing configuration)";
            Dispatcher.UIThread.Post(async () => { await Task.Delay(400); FlowPulse = false; });
        }
        RefreshInspector();
    }

    [RelayCommand]
    private void SelectExpression(bool expressionA)
    {
        SurfaceFocus = expressionA ? SurfaceFocus.ExpressionA : SurfaceFocus.ExpressionB;
        StatusMessage = expressionA ? "Expression A selected." : "Expression B selected.";
    }

    /// <summary>Toggle preset relay SW1 (1) or SW2 (2) — synced to display SWITCH LEDs.</summary>
    [RelayCommand]
    private void ToggleRelay(int switchNumber)
    {
        if (switchNumber == 1) Switch1Closed = !Switch1Closed;
        else if (switchNumber == 2) Switch2Closed = !Switch2Closed;
        StatusMessage = switchNumber == 1
            ? $"Relay SW1 {(Switch1Closed ? "closed" : "open")}."
            : $"Relay SW2 {(Switch2Closed ? "closed" : "open")}.";
    }

    [RelayCommand]
    private void ToggleHitRegions() => ShowHitRegions = !ShowHitRegions;

    [RelayCommand]
    private void CopyPreset()
    {
        _clipboardPreset = ClonePreset(SelectedPreset);
        _clipboardName = PresetName;
        CanPastePreset = true;
        StatusMessage = $"Copied preset {Bank:00}/{(Footswitch == 10 ? 0 : Footswitch):00}.";
    }

    [RelayCommand]
    private void PastePreset()
    {
        if (_clipboardPreset is null) return;
        Snapshot();
        CopyPresetInto(SelectedPreset, _clipboardPreset);
        if (_clipboardName is not null)
            _config.PresetNames[SelectedIndex] = _clipboardName;
        RefreshAll();
        StatusMessage = $"Pasted into preset {Bank:00}/{(Footswitch == 10 ? 0 : Footswitch):00}.";
    }

    [RelayCommand]
    private void ClearPresetMidi()
    {
        Snapshot();
        var p = SelectedPreset;
        foreach (var pc in p.ProgramChanges) { pc.Enabled = false; pc.Program = 0; }
        foreach (var cc in p.ControlChanges) { cc.Enabled = false; cc.Controller = 0; cc.Value = 0; }
        p.Note.Enabled = false;
        p.Note.Note = 0;
        p.ExpressionA.Enabled = false;
        p.ExpressionB.Enabled = false;
        p.Switch1Closed = false;
        p.Switch2Closed = false;
        RefreshAll();
        StatusMessage = "Cleared MIDI on selected preset.";
    }

    private async Task SendLivePreviewAsync()
    {
        if (!Connected) { StatusMessage = "TEST mode: connect MIDI OUT to preview pedal messages."; return; }
        try
        {
            foreach (var ev in LiveMidiPreview.BuildPressEvents(_config, SelectedPreset))
                await _transport.SendMidiEventAsync(ev);
            await Task.Delay(30);
            var noteOff = LiveMidiPreview.BuildNoteOff(_config, SelectedPreset);
            if (noteOff is not null) await _transport.SendMidiEventAsync(noteOff);
        }
        catch (Exception ex) { StatusMessage = "TEST TX failed: " + ex.Message; }
    }

    [RelayCommand] private void BankUp() { Bank = Math.Min(9, Bank + 1); RefreshAll(); }
    [RelayCommand] private void BankDown() { Bank = Math.Max(0, Bank - 1); RefreshAll(); }
    [RelayCommand] private void SelectBank(int bank) { Bank = bank; RefreshAll(); }

    [RelayCommand]
    private void SelectMapCell(int index)
    {
        Bank = index / 10;
        Footswitch = index % 10 + 1;
        SelectWorkspace(WorkspaceKind.Twin);
        RefreshAll();
    }

    partial void OnPresetNameChanged(string value)
    {
        if (_loading) return;
        Snapshot();
        _config.PresetNames[SelectedIndex] = value;
        RefreshFootswitches();
        RefreshSync();
    }

    partial void OnBankNameChanged(string value)
    {
        if (_loading) return;
        Snapshot();
        if (string.IsNullOrWhiteSpace(value)) _config.BankNames.Remove(Bank);
        else _config.BankNames[Bank] = value;
        RefreshMap();
        RefreshSync();
    }

    partial void OnSwitch1ClosedChanged(bool value) { if (_loading) return; Snapshot(); SelectedPreset.Switch1Closed = value; RefreshFlow(); RefreshSync(); }
    partial void OnSwitch2ClosedChanged(bool value) { if (_loading) return; Snapshot(); SelectedPreset.Switch2Closed = value; RefreshFlow(); RefreshSync(); }

    partial void OnExpressionAControllerChanged(int value) => PatchExpression(true);
    partial void OnExpressionAMinChanged(int value) => PatchExpression(true);
    partial void OnExpressionAMaxChanged(int value) => PatchExpression(true);
    partial void OnExpressionAEnabledChanged(bool value) => PatchExpression(true);
    partial void OnExpressionBControllerChanged(int value) => PatchExpression(false);
    partial void OnExpressionBMinChanged(int value) => PatchExpression(false);
    partial void OnExpressionBMaxChanged(int value) => PatchExpression(false);
    partial void OnExpressionBEnabledChanged(bool value) => PatchExpression(false);

    private void PatchExpression(bool a)
    {
        if (_loading) return;
        Snapshot();
        if (a)
        {
            SelectedPreset.ExpressionA.Controller = ExpressionAController;
            SelectedPreset.ExpressionA.Minimum = ExpressionAMin;
            SelectedPreset.ExpressionA.Maximum = ExpressionAMax;
            SelectedPreset.ExpressionA.Enabled = ExpressionAEnabled;
        }
        else
        {
            SelectedPreset.ExpressionB.Controller = ExpressionBController;
            SelectedPreset.ExpressionB.Minimum = ExpressionBMin;
            SelectedPreset.ExpressionB.Maximum = ExpressionBMax;
            SelectedPreset.ExpressionB.Enabled = ExpressionBEnabled;
        }
        RefreshFootswitches();
        RefreshFlow();
        RefreshSync();
    }

    public void CommitMidiAction(MidiActionItemViewModel item)
    {
        Snapshot();
        var p = SelectedPreset;
        if (item.Kind == "pc")
        {
            var slot = int.Parse(item.SlotLabel[2..]) - 1;
            p.ProgramChanges[slot].Enabled = item.Enabled;
            p.ProgramChanges[slot].Program = item.Primary;
        }
        else if (item.Kind == "cc")
        {
            var slot = int.Parse(item.SlotLabel[2..]) - 1;
            p.ControlChanges[slot].Enabled = item.Enabled;
            p.ControlChanges[slot].Controller = item.Primary;
            p.ControlChanges[slot].Value = item.Secondary;
        }
        else if (item.Kind == "note")
        {
            p.Note.Enabled = item.Enabled;
            p.Note.Note = item.Primary;
        }

        // Only reshuffle active/idle lists when membership changes — rebucketing on every
        // Primary/Secondary edit steals focus from scrub fields while typing.
        if (NeedsRebucket(item))
            RebucketMidiActions();

        RefreshFootswitches();
        RefreshFlow();
        RefreshSync();
        ActionSummary = $"{MidiActions.Count(a => a.Enabled)} ACT";
    }

    private bool NeedsRebucket(MidiActionItemViewModel item) => item.Kind switch
    {
        "pc" => item.Enabled != ProgramActionsActive.Contains(item),
        "cc" => item.Enabled != ControlActionsActive.Contains(item),
        _ => false,
    };

    private void RebucketMidiActions()
    {
        ProgramActionsActive.Clear();
        ProgramActionsIdle.Clear();
        foreach (var a in ProgramActions)
        {
            if (a.Enabled) ProgramActionsActive.Add(a);
            else ProgramActionsIdle.Add(a);
        }
        ControlActionsActive.Clear();
        ControlActionsIdle.Clear();
        foreach (var a in ControlActions)
        {
            if (a.Enabled) ControlActionsActive.Add(a);
            else ControlActionsIdle.Add(a);
        }
    }

    [RelayCommand]
    private void EnableMidiSlot(MidiActionItemViewModel item)
    {
        if (!item.Enabled)
            item.Enabled = true;
    }

    public void CommitChannel(ChannelRouteViewModel route)
    {
        Snapshot();
        _config.Global.MidiChannels[route.Index] = Math.Clamp(route.Channel, 1, 16);
        RefreshInspector();
        RefreshSync();
    }

    partial void OnDirectSelectChanged(bool value) { if (_loading) return; Snapshot(); _config.Global.DirectSelect = value; RefreshSync(); }
    partial void OnMidiMergeChanged(bool value) { if (_loading) return; Snapshot(); _config.Global.MidiMerge = value; RefreshSync(); }
    partial void OnRunningStatusChanged(bool value) { if (_loading) return; Snapshot(); _config.Global.RunningStatus = value; RefreshSync(); }

    partial void OnExpressionAValueChanged(int value)
    {
        if (Mode != EditorMode.Live || !Connected) return;
        PulseMidi(rx: false);
        FlowPulse = true;
        _ = SendExpressionAsync(true, value);
        Dispatcher.UIThread.Post(async () => { await Task.Delay(300); FlowPulse = false; });
    }

    partial void OnExpressionBValueChanged(int value)
    {
        if (Mode != EditorMode.Live || !Connected) return;
        _ = SendExpressionAsync(false, value);
    }

    private async Task SendExpressionAsync(bool a, int value)
    {
        try
        {
            var exp = a ? SelectedPreset.ExpressionA : SelectedPreset.ExpressionB;
            var channelIndex = a ? 7 : 8;
            var ev = LiveMidiPreview.BuildExpressionEvent(_config, exp, channelIndex, value);
            if (ev is not null) await _transport.SendMidiEventAsync(ev);
        }
        catch (Exception ex) { StatusMessage = "TEST expression TX failed: " + ex.Message; }
    }

    private void RefreshAll()
    {
        _loading = true;
        FirmwareLabel = $"{_config.Firmware}{(_config.FirmwareVersion is null ? "" : " " + _config.FirmwareVersion)}";
        BankName = _config.BankNames.GetValueOrDefault(Bank, "");
        PresetName = _config.PresetNames.GetValueOrDefault(SelectedIndex, $"Preset {Bank:00}{Footswitch % 10}");
        var p = SelectedPreset;
        ExpressionAController = p.ExpressionA.Controller;
        ExpressionAMin = p.ExpressionA.Minimum;
        ExpressionAMax = p.ExpressionA.Maximum;
        ExpressionAEnabled = p.ExpressionA.Enabled;
        ExpressionBController = p.ExpressionB.Controller;
        ExpressionBMin = p.ExpressionB.Minimum;
        ExpressionBMax = p.ExpressionB.Maximum;
        ExpressionBEnabled = p.ExpressionB.Enabled;
        Switch1Closed = p.Switch1Closed;
        Switch2Closed = p.Switch2Closed;
        DirectSelect = _config.Global.DirectSelect;
        MidiMerge = _config.Global.MidiMerge;
        RunningStatus = _config.Global.RunningStatus;
        for (var i = 0; i < 10; i++) ChannelRoutes[i].Channel = _config.Global.MidiChannels[i];
        RefreshFootswitches();
        RefreshInspector();
        RefreshMap();
        RefreshHex();
        RefreshSync();
        _loading = false;
        CanUndo = _undo.Count > 0;
        CanRedo = _redo.Count > 0;
    }

    private void RefreshFootswitches()
    {
        var baseline = _deviceSnapshot ?? _savedSnapshot;
        for (var i = 0; i < 10; i++)
        {
            var number = i + 1;
            var preset = _config.GetPreset(Bank, number);
            var name = _config.PresetNames.GetValueOrDefault(preset.Index, $"Preset {Bank:00}{(number == 10 ? 0 : number)}");
            var fs = Footswitches[i];
            fs.Number = number;
            fs.Name = name;
            fs.Selected = number == Footswitch;
            fs.MidiLine = PrimaryMidi(preset);
            fs.ExpressionLine = ExprLine(preset);
            fs.Role = RoleOf(preset);
            fs.LedOn = Mode == EditorMode.Live && fs.Selected || (fs.Role == "STOMP" && preset.ControlChanges.Any(c => c.Enabled && c.Value > 63));
            fs.Modified = !PresetEqual(preset, baseline.Presets[preset.Index], name, baseline.PresetNames.GetValueOrDefault(preset.Index, ""));
        }
    }

    private void RefreshInspector()
    {
        var p = SelectedPreset;
        RoleLabel = RoleOf(p);
        MidiActions.Clear();
        ProgramActions.Clear();
        ProgramActionsActive.Clear();
        ProgramActionsIdle.Clear();
        ControlActions.Clear();
        ControlActionsActive.Clear();
        ControlActionsIdle.Clear();
        NoteActions.Clear();
        for (var i = 0; i < 5; i++)
        {
            var pc = new MidiActionItemViewModel
            {
                Kind = "pc",
                SlotLabel = $"PC{i + 1}",
                Enabled = p.ProgramChanges[i].Enabled,
                Channel = _config.Global.MidiChannels[i],
                Primary = p.ProgramChanges[i].Program,
                HasSecondary = false,
            };
            pc.Commit = CommitMidiAction;
            MidiActions.Add(pc);
            ProgramActions.Add(pc);
            if (pc.Enabled) ProgramActionsActive.Add(pc);
            else ProgramActionsIdle.Add(pc);
        }
        for (var i = 0; i < 2; i++)
        {
            var cc = new MidiActionItemViewModel
            {
                Kind = "cc",
                SlotLabel = $"CC{i + 1}",
                Enabled = p.ControlChanges[i].Enabled,
                Channel = _config.Global.MidiChannels[5 + i],
                Primary = p.ControlChanges[i].Controller,
                Secondary = p.ControlChanges[i].Value,
                HasSecondary = true,
            };
            cc.Commit = CommitMidiAction;
            MidiActions.Add(cc);
            ControlActions.Add(cc);
            if (cc.Enabled) ControlActionsActive.Add(cc);
            else ControlActionsIdle.Add(cc);
        }
        var note = new MidiActionItemViewModel
        {
            Kind = "note",
            SlotLabel = "NOTE",
            Enabled = p.Note.Enabled,
            Channel = _config.Global.MidiChannels[9],
            Primary = p.Note.Note,
            HasSecondary = false,
        };
        note.Commit = CommitMidiAction;
        MidiActions.Add(note);
        NoteActions.Add(note);
        ActionSummary = $"{MidiActions.Count(a => a.Enabled)} ACT";
        RefreshFlow();
        LiveTxText = BuildLiveTx();
        RefreshChanges();
    }

    private void RefreshFlow()
    {
        var p = SelectedPreset;
        var parts = new List<string> { $"SW{(Footswitch == 10 ? 0 : Footswitch)} · {PresetName}" };
        for (var i = 0; i < 5; i++)
            if (p.ProgramChanges[i].Enabled)
                parts.Add($"PC{i + 1} CH{_config.Global.MidiChannels[i]}:{p.ProgramChanges[i].Program:000}");
        for (var i = 0; i < 2; i++)
            if (p.ControlChanges[i].Enabled)
                parts.Add($"CC{i + 1} CH{_config.Global.MidiChannels[5 + i]}:{p.ControlChanges[i].Controller:00}={p.ControlChanges[i].Value}");
        if (p.Note.Enabled)
            parts.Add($"NOTE CH{_config.Global.MidiChannels[9]}:{p.Note.Note}");
        if (p.ExpressionA.Enabled)
            parts.Add($"EXP-A CC{p.ExpressionA.Controller:00} {p.ExpressionA.Minimum}–{p.ExpressionA.Maximum}");
        if (p.ExpressionB.Enabled)
            parts.Add($"EXP-B CC{p.ExpressionB.Controller:00} {p.ExpressionB.Minimum}–{p.ExpressionB.Maximum}");
        FlowText = string.Join("  ·  ", parts);
    }

    private void RefreshChanges()
    {
        PendingChanges.Clear();
        var baseline = _deviceSnapshot ?? _savedSnapshot;
        foreach (var c in ConfigDiff.Diff(_config, baseline)) PendingChanges.Add(c);
        HasChanges = PendingChanges.Count > 0;
    }

    private void RefreshMap()
    {
        for (var i = 0; i < 100; i++)
        {
            var p = _config.Presets[i];
            var cell = MapCells[i];
            cell.Name = _config.PresetNames.GetValueOrDefault(i, "");
            var label = cell.Name;
            if (label.StartsWith("Preset ", StringComparison.OrdinalIgnoreCase))
                label = label["Preset ".Length..].Trim();
            cell.ShortName = string.IsNullOrWhiteSpace(label)
                ? cell.PedalCode
                : (label.Length <= 8 ? label : label[..7] + "…");
            cell.Selected = i == SelectedIndex;
            cell.HasMessages = p.ProgramChanges.Any(x => x.Enabled) || p.ControlChanges.Any(x => x.Enabled) || p.Note.Enabled;
            cell.IsStomp = IsStomp(p);
            cell.HasExpression = p.ExpressionA.Enabled || p.ExpressionB.Enabled;
        }
        foreach (var row in MapBanks)
        {
            row.BankName = _config.BankNames.GetValueOrDefault(row.Bank, "");
            row.IsActiveBank = row.Bank == Bank;
        }
    }

    private void RefreshHex()
    {
        HexRows.Clear();
        if (_config.SourceSysEx is null)
        {
            RawHex = "No SysEx dump in memory. Read the device or open a .syx to inspect bytes.";
            return;
        }

        byte[] bytes;
        byte[] decoded;
        try { bytes = FcbSysExCodec.Serialize(_config); decoded = FcbSysExCodec.Decode(bytes); }
        catch (Exception ex) { RawHex = "Decode failed: " + ex.Message; return; }

        var compareDecoded = _deviceSnapshot?.SourceSysEx is not null
            ? FcbSysExCodec.Decode(FcbSysExCodec.Serialize(_deviceSnapshot))
            : null;

        // Show selected preset (16 bytes) + globals + any reserved diffs.
        var start = SelectedIndex * 16;
        RawHex = $"Decoded preset @{start:X4} + globals. Transport length={bytes.Length}.";
        void AddDecoded(int offset)
        {
            if (offset < 0 || offset >= decoded.Length) return;
            var changed = compareDecoded is not null && compareDecoded[offset] != decoded[offset];
            HexRows.Add(new HexRowViewModel
            {
                Offset = offset.ToString("X4"),
                Hex = decoded[offset].ToString("X2"),
                Meaning = DecodedMemoryLab.DescribeDecodedOffset(offset, decoded[offset]),
                Changed = changed ? $"{compareDecoded![offset]:X2} → {decoded[offset]:X2}" : "—",
                IsChanged = changed,
            });
        }

        for (var i = 0; i < 16; i++) AddDecoded(start + i);
        for (var i = 2016; i <= 2047; i++) AddDecoded(i);

        if (compareDecoded is not null)
        {
            foreach (var d in DecodedMemoryLab.Diff(compareDecoded, decoded))
            {
                if (d.Offset >= start && d.Offset < start + 16) continue;
                if (d.Offset is >= 2016 and <= 2047) continue;
                AddDecoded(d.Offset);
            }
        }
    }

    private string BuildLiveTx()
    {
        var p = SelectedPreset;
        var lines = new List<string> { $"SWITCH {(Footswitch == 10 ? 0 : Footswitch)}", PresetName, "TX" };
        for (var i = 0; i < 5; i++) if (p.ProgramChanges[i].Enabled) lines.Add($"CH{_config.Global.MidiChannels[i]} PC{p.ProgramChanges[i].Program:00}");
        for (var i = 0; i < 2; i++) if (p.ControlChanges[i].Enabled) lines.Add($"CH{_config.Global.MidiChannels[5 + i]} CC{p.ControlChanges[i].Controller:00} {p.ControlChanges[i].Value}");
        return string.Join('\n', lines);
    }

    private long _lastSnapshotTick;
    private long _lastHeavySyncTick;
    private int _syncGate;

    private void Snapshot()
    {
        if (_loading) return;
        // Coalesce rapid scrub/nudge into one undo frame.
        var now = Environment.TickCount64;
        if (now - _lastSnapshotTick < 350 && _undo.Count > 0)
            return;
        _lastSnapshotTick = now;
        _undo.Push(Clone(_config));
        _redo.Clear();
        CanUndo = true;
        CanRedo = false;
    }

    private void RefreshSync()
    {
        var now = Environment.TickCount64;
        if (now - _lastHeavySyncTick < 120)
        {
            CanUndo = _undo.Count > 0;
            CanRedo = _redo.Count > 0;
            var gate = Interlocked.Increment(ref _syncGate);
            _ = FlushSyncSoonAsync(gate);
            return;
        }
        FlushRefreshSync();
    }

    private async Task FlushSyncSoonAsync(int gate)
    {
        try
        {
            await Task.Delay(160);
            if (gate != Volatile.Read(ref _syncGate)) return;
            FlushRefreshSync();
        }
        catch
        {
            // ignore shutdown races
        }
    }

    private void FlushRefreshSync()
    {
        _lastHeavySyncTick = Environment.TickCount64;
        RefreshChanges();
        var baseline = _deviceSnapshot;
        if (baseline is null) DeviceSync = "NO SNAPSHOT";
        else DeviceSync = FcbSysExCodec.Serialize(_config).AsSpan().SequenceEqual(FcbSysExCodec.Serialize(baseline))
            ? "SYNCED" : "OUT OF SYNC";
        EditorSync = PendingChanges.Count == 0 ? "CLEAN" : $"{PendingChanges.Count} CHANGES";
        var projectDirty = JsonSerializer.Serialize(_config) != JsonSerializer.Serialize(_savedSnapshot);
        ProjectSync = projectDirty ? "NOT SAVED" : (_currentPath is null ? "LOCAL" : "SAVED");
        CanUndo = _undo.Count > 0;
        CanRedo = _redo.Count > 0;
    }

    private void ApplyTransportState(TransportState state)
    {
        var wasConnected = Connected;
        Connected = state is TransportState.Connected or TransportState.Receiving or TransportState.Sending;
        ConnectionLabel = Connected ? "ONLINE" : state == TransportState.Error ? "ERROR" : "OFFLINE";
        if (wasConnected && !Connected && Mode == EditorMode.Live)
        {
            Mode = EditorMode.Edit;
            IsLiveMode = false;
            RefreshFootswitches();
        }
        NotifyLinkState();
    }

    private void NotifyLinkState()
    {
        OnPropertyChanged(nameof(LinkButtonLabel));
        OnPropertyChanged(nameof(CanChangePorts));
        OnPropertyChanged(nameof(CanDeviceIo));
        OnPropertyChanged(nameof(CanLink));
        ToggleLinkCommand.NotifyCanExecuteChanged();
        ReadDeviceCommand.NotifyCanExecuteChanged();
        WriteDeviceCommand.NotifyCanExecuteChanged();
        ToggleLiveCommand.NotifyCanExecuteChanged();
    }

    partial void OnConnectedChanged(bool value) => NotifyLinkState();
    partial void OnSelectedInputChanged(MidiPortInfo? value) => NotifyLinkState();
    partial void OnSelectedOutputChanged(MidiPortInfo? value) => NotifyLinkState();

    private void PulseMidi(bool rx)
    {
        if (rx) { MidiRx = true; Dispatcher.UIThread.Post(async () => { await Task.Delay(350); MidiRx = false; }); }
        else { MidiTx = true; Dispatcher.UIThread.Post(async () => { await Task.Delay(350); MidiTx = false; }); }
    }

    private string PrimaryMidi(FcbPreset p)
    {
        for (var i = 0; i < 5; i++) if (p.ProgramChanges[i].Enabled) return $"CH{_config.Global.MidiChannels[i]}·PC{p.ProgramChanges[i].Program:000}";
        for (var i = 0; i < 2; i++) if (p.ControlChanges[i].Enabled) return $"CH{_config.Global.MidiChannels[5 + i]}·CC{p.ControlChanges[i].Controller:00}";
        return "—";
    }

    private static string ExprLine(FcbPreset p)
    {
        var parts = new List<string>();
        if (p.ExpressionA.Enabled) parts.Add($"A·CC{p.ExpressionA.Controller:00}");
        if (p.ExpressionB.Enabled) parts.Add($"B·CC{p.ExpressionB.Controller:00}");
        return string.Join(" ", parts);
    }

    private static string RoleOf(FcbPreset p)
    {
        if (IsStomp(p)) return "STOMP";
        if (!p.ProgramChanges.Any(x => x.Enabled) && !p.ControlChanges.Any(x => x.Enabled) && !p.Note.Enabled) return "EMPTY";
        return "PRESET";
    }

    private static bool IsStomp(FcbPreset p)
    {
        if (p.ProgramChanges.Any(x => x.Enabled)) return false;
        var ccs = p.ControlChanges.Where(c => c.Enabled).ToArray();
        return ccs.Length > 0 && ccs.All(c => c.Value is 0 or 127);
    }

    private static bool PresetEqual(FcbPreset a, FcbPreset b, string nameA, string nameB)
    {
        if (nameA != nameB) return false;
        return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    }

    private static string ShortPort(string name)
    {
        if (name.Contains("WIDI", StringComparison.OrdinalIgnoreCase)) return "WIDI";
        if (name.Contains("FCB1010", StringComparison.OrdinalIgnoreCase)) return "FCB1010";
        return name.Length > 12 ? name[..10] + "…" : name;
    }

    private static FcbConfiguration Clone(FcbConfiguration c) => c.DeepClone();

    private static FcbPreset ClonePreset(FcbPreset p) =>
        JsonSerializer.Deserialize<FcbPreset>(JsonSerializer.Serialize(p))!;

    private static void CopyPresetInto(FcbPreset target, FcbPreset source)
    {
        var copy = ClonePreset(source);
        target.ProgramChanges = copy.ProgramChanges;
        target.ControlChanges = copy.ControlChanges;
        target.Note = copy.Note;
        target.ExpressionA = copy.ExpressionA;
        target.ExpressionB = copy.ExpressionB;
        target.Switch1Closed = copy.Switch1Closed;
        target.Switch2Closed = copy.Switch2Closed;
    }

    partial void OnSelectedFirmwareChanged(FirmwareFamily value)
    {
        if (_loading) return;
        _config.Firmware = value;
        FirmwareLabel = $"{value}{(_config.FirmwareVersion is null ? "" : " " + _config.FirmwareVersion)}";
        RefreshSync();
    }

    private static FcbConfiguration CreateDemo()
    {
        // Empty editor memory — not a fake “working” device config.
        return new FcbConfiguration
        {
            Firmware = FirmwareFamily.Unknown,
            SourceDescription = "Empty editor — read device or open a .syx / .fcbproject"
        };
    }

    public void Dispose()
    {
        _device.Dispose();
        _transport.Dispose();
    }
}

internal sealed class NullDialogs : IAppDialogs
{
    public Task AlertAsync(string title, string message) => Task.CompletedTask;
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    public Task<string?> OpenFileAsync(string title, params Avalonia.Platform.Storage.FilePickerFileType[] types) => Task.FromResult<string?>(null);
    public Task<string?> SaveFileAsync(string title, string? suggestedName, params Avalonia.Platform.Storage.FilePickerFileType[] types) => Task.FromResult<string?>(null);
}

public sealed partial class MapBankRowViewModel : ObservableObject
{
    public required int Bank { get; init; }
    public string BankLabel => Bank.ToString("00");
    [ObservableProperty] private string _bankName = "";
    public ObservableCollection<MapCellViewModel> Lower { get; } = []; // switches 1–5
    public ObservableCollection<MapCellViewModel> Upper { get; } = []; // switches 6–10
    [ObservableProperty] private bool _isActiveBank;
}

public partial class MapCellViewModel : ObservableObject
{
    public int Index { get; init; }
    public int Bank => Index / 10;
    public int SwitchNumber => Index % 10 + 1;
    public string PedalCode => $"{Bank}{(SwitchNumber == 10 ? 0 : SwitchNumber)}";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _shortName = "";
    [ObservableProperty] private bool _selected;
    [ObservableProperty] private bool _hasMessages;
    [ObservableProperty] private bool _isStomp;
    [ObservableProperty] private bool _hasExpression;
}
